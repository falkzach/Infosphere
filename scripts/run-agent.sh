#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash scripts/run-agent.sh \
    --role <role> \
    --runtime <runtime> \
    --session-name <name> \
    --bootstrap-path <path> \
    [--workspace-key <key>] \
    [--api-base-url <url>]

Currently automated runtime support:
  codex
  claude
EOF
}

role=""
runtime=""
session_name=""
bootstrap_path=""
workspace_key="${INFOSPHERE_WORKSPACE_KEY:-mvp}"
api_base_url="${INFOSPHERE_API_BASE_URL:-http://localhost:5080}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --role)
      role="${2:-}"
      shift 2
      ;;
    --runtime)
      runtime="${2:-}"
      shift 2
      ;;
    --session-name)
      session_name="${2:-}"
      shift 2
      ;;
    --bootstrap-path)
      bootstrap_path="${2:-}"
      shift 2
      ;;
    --workspace-key)
      workspace_key="${2:-}"
      shift 2
      ;;
    --api-base-url)
      api_base_url="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$role" || -z "$runtime" || -z "$session_name" || -z "$bootstrap_path" ]]; then
  usage >&2
  exit 1
fi

if [[ ! -f "$bootstrap_path" ]]; then
  echo "Missing bootstrap packet: $bootstrap_path" >&2
  exit 1
fi

case "$runtime" in
  codex|claude)
    ;;
  *)
    echo "Automated runtime bootstrap is currently implemented for codex and claude. Requested: $runtime" >&2
    exit 1
    ;;
esac

repo_root="$(git rev-parse --show-toplevel)"

if [[ ! -f "$repo_root/scripts/mcp_tool.py" ]]; then
  echo "Missing helper script: $repo_root/scripts/mcp_tool.py" >&2
  exit 1
fi

workspace_response="$(python3 "$repo_root/scripts/mcp_tool.py" --tool list_workspaces --api-base-url "$api_base_url")"
workspace_id="$(WORKSPACE_RESPONSE="$workspace_response" python3 - "$workspace_key" <<'PY'
import json
import os
import sys

workspace_key = sys.argv[1]
response = json.loads(os.environ["WORKSPACE_RESPONSE"])
workspaces = response["result"]["structuredContent"]["workspaces"]
selected = None
for workspace in workspaces:
    if workspace.get("key") == workspace_key:
        selected = workspace
        break
if selected is None and workspaces:
    selected = workspaces[0]
if selected is None:
    raise SystemExit("No workspaces available for agent bootstrap.")
print(selected["id"])
PY
)"

agent_id="${session_name}"
display_name="$(python3 - "$session_name" "$role" <<'PY'
import sys
session_name, role = sys.argv[1:]
label = session_name
if label.startswith("infosphere-"):
    label = label[len("infosphere-"):]
label = label.replace("-", " ").title().strip()
if label:
    print(label)
else:
    print(role.replace("-", " ").title())
PY
)"

register_args="$(python3 - "$workspace_id" "$agent_id" "$runtime" "$display_name" <<'PY'
import json
import sys

workspace_id, agent_id, runtime, display_name = sys.argv[1:]
payload = {
    "workspaceId": workspace_id,
    "agentId": agent_id,
    "agentKind": runtime,
    "displayName": display_name,
}
print(json.dumps(payload))
PY
)"

register_response="$(
  INFOSPHERE_MCP_ARGS="$register_args" \
    python3 "$repo_root/scripts/mcp_tool.py" \
      --tool register_agent_session \
      --arguments-env INFOSPHERE_MCP_ARGS \
      --api-base-url "$api_base_url"
)"
session_id="$(REGISTER_RESPONSE="$register_response" python3 - <<'PY'
import json
import os
import sys

response = json.loads(os.environ["REGISTER_RESPONSE"])
print(response["result"]["structuredContent"]["session"]["id"])
PY
)"

runtime_prompt="$(mktemp "/tmp/${session_name}-runtime-XXXX.md")"
mcp_name="infosphere-${session_name}"
state_dir="/tmp/infosphere-agent-state"
state_file="$state_dir/${session_name}.env"
context_image_path="$state_dir/${session_name}-context-image.md"
context_manifest_path="$state_dir/${session_name}-context-image.json"
codex_home="$state_dir/${session_name}-codex-home"
mcp_config_path="$state_dir/${session_name}-mcp-config.json"
heartbeat_pid=""

mkdir -p "$state_dir"

maybe_clear() {
  if [[ -n "${TERM:-}" ]] && command -v clear >/dev/null 2>&1; then
    clear || true
  fi
}

call_mcp() {
  local tool="$1"
  local arguments_json="${2:-'{}'}"
  INFOSPHERE_MCP_ARGS="$arguments_json" \
    python3 "$repo_root/scripts/mcp_tool.py" \
      --tool "$tool" \
      --arguments-env INFOSPHERE_MCP_ARGS \
      --api-base-url "$api_base_url"
}

json_arg() {
  python3 - "$@" <<'PY'
import json
import sys

pairs = sys.argv[1:]
payload = {}
for pair in pairs:
    key, value = pair.split("=", 1)
    if value == "__NULL__":
        payload[key] = None
    elif value.startswith("int:"):
        payload[key] = int(value[4:])
    else:
        payload[key] = value
print(json.dumps(payload))
PY
}

assigned_tasks_summary() {
  local response
  response="$(call_mcp "list_tasks" "$(json_arg "workspaceId=$workspace_id")")"
  RESPONSE="$response" AGENT_ID="$agent_id" python3 - <<'PY'
import json
import os

response = json.loads(os.environ["RESPONSE"])
agent_id = os.environ["AGENT_ID"]
tasks = response["result"]["structuredContent"]["tasks"]
relevant = [
    {
        "id": task["id"],
        "title": task["title"],
        "stateId": task["state"]["id"],
        "stateKey": task["state"]["key"],
    }
    for task in tasks
    # exclude terminal states: 4=completed, 5=cancelled, 99=error
    if task.get("assignedAgentId") == agent_id and task["state"]["id"] not in (4, 5, 99)
]
print(json.dumps({"count": len(relevant), "tasks": relevant}))
PY
}

available_tasks_count() {
  local response
  response="$(call_mcp "list_available_tasks" "$(json_arg "workspaceId=$workspace_id")")"
  RESPONSE="$response" python3 - <<'PY'
import json
import os

response = json.loads(os.environ["RESPONSE"])
tasks = response["result"]["structuredContent"]["tasks"]
print(len(tasks))
PY
}

latest_workspace_message_id() {
  local response
  response="$(call_mcp "list_workspace_messages" "$(json_arg "workspaceId=$workspace_id")")"
  RESPONSE="$response" python3 - <<'PY'
import json
import os

response = json.loads(os.environ["RESPONSE"])
messages = response["result"]["structuredContent"]["messages"]
print(messages[0]["id"] if messages else "")
PY
}

start_heartbeat_loop() {
  (
    while true; do
      call_mcp "heartbeat_agent_session" "$(json_arg "sessionId=$session_id")" >/dev/null 2>&1 || true
      sleep 30
    done
  ) &
  heartbeat_pid="$!"
}

refresh_context_image() {
  bash "$repo_root/scripts/build-context-image.sh" \
    --role "$role" \
    --runtime "$runtime" \
    --repo-root "$repo_root" \
    --bootstrap-path "$bootstrap_path" \
    --workspace-id "$workspace_id" \
    --output-markdown "$context_image_path" \
    --output-manifest "$context_manifest_path" >/dev/null
}

build_runtime_prompt() {
  local trigger_reason="$1"
  local assigned_summary_json="$2"
  local latest_message_id="$3"

  ASSIGNED_SUMMARY="$assigned_summary_json" python3 - "$runtime_prompt" "$role" "$runtime" "$workspace_id" "$session_id" "$agent_id" "$display_name" "$context_image_path" "$trigger_reason" "$latest_message_id" <<'PY'
import json
import os
import sys

(
    runtime_prompt,
    role,
    runtime,
    workspace_id,
    session_id,
    agent_id,
    display_name,
    context_image_path,
    trigger_reason,
    latest_message_id,
) = sys.argv[1:]

summary = json.loads(os.environ["ASSIGNED_SUMMARY"])
lines = [
    "# Live Agent Session",
    "",
    "You are already registered in Infosphere.",
    "",
    f"- role: {role}",
    f"- runtime: {runtime}",
    f"- workspaceId: {workspace_id}",
    f"- sessionId: {session_id}",
    f"- agentId: {agent_id}",
    f"- displayName: {display_name}",
    "",
    "Operational rules:",
    f"- use the existing Infosphere session `{session_id}`",
    "- do not register a second session",
    f"- use `heartbeat_agent_session` with sessionId `{session_id}` while active",
    f"- before ending work, call `close_agent_session` with sessionId `{session_id}` only if this wrapper is explicitly being shut down",
    "- you are intentionally running with local approvals and sandbox bypassed",
    "- use minimal tokens and do not invent work",
    "",
    f"Trigger reason: {trigger_reason}",
]

if summary["tasks"]:
    lines.extend(["", "Assigned tasks:"])
    for task in summary["tasks"]:
        lines.append(f"- {task['id']}: {task['title']} ({task['stateKey']})")

if latest_message_id:
    lines.extend(["", f"Latest workspace message id: {latest_message_id}"])

lines.extend(
    [
        "",
        "Context image:",
        f"- cached startup baseline: {context_image_path}",
        "",
        "Startup behavior:",
        "- first read the cached startup baseline file above",
        "- then inspect only the relevant workspace messages and your assigned tasks",
        "- if you do not have actionable work, exit quickly and let the supervisor keep polling",
        "- prefer the cached startup baseline over re-reading many repo files",
        "- use MCP for live deltas, not for reconstructing startup context",
    ]
)

with open(runtime_prompt, "w", encoding="utf-8") as handle:
    handle.write("\n".join(lines) + "\n")
PY
}

cleanup() {
  set +e
  if [[ -n "${heartbeat_pid:-}" ]]; then
    kill "$heartbeat_pid" >/dev/null 2>&1 || true
  fi
  if [[ -n "${session_id:-}" ]]; then
    close_args="$(python3 - "$session_id" <<'PY'
import json
import sys
print(json.dumps({"sessionId": sys.argv[1]}))
PY
)"
    INFOSPHERE_MCP_ARGS="$close_args" \
      python3 "$repo_root/scripts/mcp_tool.py" \
        --tool close_agent_session \
        --arguments-env INFOSPHERE_MCP_ARGS \
        --api-base-url "$api_base_url" >/dev/null 2>&1 || true
  fi
  rm -f "$runtime_prompt"
}

trap cleanup EXIT INT TERM

if [[ "$runtime" == "codex" ]]; then
  mkdir -p "$codex_home/.codex"
  cp /home/falkzach/.codex/auth.json "$codex_home/.codex/auth.json"
  cat > "$codex_home/.codex/config.toml" <<EOF
personality = "pragmatic"
model = "gpt-5.4"

[projects."$repo_root"]
trust_level = "trusted"

[mcp_servers.$mcp_name]
command = "dotnet"
args = ["run", "--project", "$repo_root/src/Infosphere.Mcp/Infosphere.Mcp.csproj"]

[mcp_servers.$mcp_name.env]
DOTNET_CLI_HOME = "/tmp"
INFOSPHERE_API_BASE_URL = "$api_base_url"
EOF
elif [[ "$runtime" == "claude" ]]; then
  python3 - "$mcp_config_path" "$mcp_name" "$repo_root" "$api_base_url" "$role" <<'PY'
import json
import sys

mcp_config_path, mcp_name, repo_root, api_base_url, role = sys.argv[1:]
config = {
    "mcpServers": {
        mcp_name: {
            "command": "dotnet",
            "args": ["run", "--project", f"{repo_root}/src/Infosphere.Mcp/Infosphere.Mcp.csproj"],
            "env": {
                "DOTNET_CLI_HOME": "/tmp",
                "INFOSPHERE_API_BASE_URL": api_base_url,
            },
        }
    }
}
if role == "user-experience-manager":
    config["mcpServers"]["playwright"] = {
        "command": "npx",
        "args": ["@playwright/mcp", "--browser", "chrome"],
    }
with open(mcp_config_path, "w", encoding="utf-8") as f:
    json.dump(config, f, indent=2)
PY
fi

refresh_context_image

maybe_clear
echo "Bootstrapped agent supervisor."
echo "Role: $role"
echo "Runtime: $runtime"
echo "Workspace ID: $workspace_id"
echo "Session ID: $session_id"
echo "Bootstrap packet: $bootstrap_path"
echo "Context image: $context_image_path"
echo "State file: $state_file"
echo
echo "Supervisor behavior:"
echo "- keeps the session active"
echo "- heartbeats every 30 seconds"
echo "- polls Infosphere for work"
echo "- only launches $runtime when there is actionable work"
echo

last_seen_message_id="$(latest_workspace_message_id)"
{
  printf 'LAST_SEEN_MESSAGE_ID=%q\n' "$last_seen_message_id"
  printf 'LAST_SEEN_AVAILABLE_COUNT=0\n'
  printf 'LAST_SEEN_ASSIGNED_COUNT=0\n'
} > "$state_file"

start_heartbeat_loop

consecutive_wakes=0

while true; do
  source "$state_file"

  assigned_summary="$(assigned_tasks_summary)"
  assigned_count="$(ASSIGNED_SUMMARY="$assigned_summary" python3 - <<'PY'
import json
import os
print(json.loads(os.environ["ASSIGNED_SUMMARY"])["count"])
PY
)"

  trigger_reason=""
  latest_message_id=""
  current_latest_message_id="${LAST_SEEN_MESSAGE_ID:-}"
  current_available_count=0

  if [[ "$role" == "coordinator" ]]; then
    current_available_count="$(available_tasks_count)"
    current_latest_message_id="$(latest_workspace_message_id)"
    if [[ -n "$current_latest_message_id" && "$current_latest_message_id" != "${LAST_SEEN_MESSAGE_ID:-}" ]]; then
      trigger_reason="new workspace message"
      latest_message_id="$current_latest_message_id"
    elif [[ "$current_available_count" -gt "${LAST_SEEN_AVAILABLE_COUNT:-0}" ]]; then
      trigger_reason="new available tasks"
    elif [[ "$assigned_count" -gt "${LAST_SEEN_ASSIGNED_COUNT:-0}" ]]; then
      trigger_reason="new assigned tasks"
    fi
  else
    # Implementors and UX wake whenever they have assigned work — they need to
    # persist on a task until completion, not just on the first appearance.
    if [[ "$assigned_count" -gt 0 ]]; then
      trigger_reason="assigned tasks exist"
    fi
  fi

  # Update coordinator cursors unconditionally; implementor/UX assigned count is not cursor-gated
  {
    printf 'LAST_SEEN_MESSAGE_ID=%q\n' "$current_latest_message_id"
    printf 'LAST_SEEN_AVAILABLE_COUNT=%d\n' "$current_available_count"
    printf 'LAST_SEEN_ASSIGNED_COUNT=%d\n' "$assigned_count"
  } > "$state_file"

  if [[ -n "$trigger_reason" ]]; then
    consecutive_wakes=$((consecutive_wakes + 1))
    refresh_context_image
    build_runtime_prompt "$trigger_reason" "$assigned_summary" "$latest_message_id"
    maybe_clear
    echo "Launching $runtime for $session_name"
    echo "Reason: $trigger_reason"
    echo "Session ID: $session_id"
    echo "Runtime prompt: $runtime_prompt"
    echo "Context image: $context_image_path"
    echo
    if [[ "$runtime" == "codex" ]]; then
      HOME="$codex_home" codex exec --dangerously-bypass-approvals-and-sandbox < "$runtime_prompt" || true
    elif [[ "$runtime" == "claude" ]]; then
      claude --dangerously-skip-permissions --print \
        --mcp-config "$mcp_config_path" \
        --strict-mcp-config \
        < "$runtime_prompt" || true
    fi
    echo
    echo "$runtime exited. Supervisor will continue polling."
    # Back off on repeated consecutive wakes to avoid thrashing on stuck tasks.
    backoff=5
    if [[ $consecutive_wakes -gt 3 ]]; then
      backoff=$((5 * consecutive_wakes))
      if [[ $backoff -gt 120 ]]; then
        backoff=120
      fi
    fi
    sleep "$backoff"
  else
    consecutive_wakes=0
    sleep 15
  fi
done
