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
  codex)
    ;;
  *)
    echo "Automated runtime bootstrap is currently implemented for codex only. Requested: $runtime" >&2
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
display_name="$(python3 - "$role" <<'PY'
import sys
role = sys.argv[1]
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

build_runtime_prompt() {
  local trigger_reason="$1"
  local assigned_summary_json="$2"
  local latest_message_id="$3"

  ASSIGNED_SUMMARY="$assigned_summary_json" python3 - "$runtime_prompt" "$role" "$runtime" "$workspace_id" "$session_id" "$agent_id" "$display_name" "$bootstrap_path" "$repo_root" "$trigger_reason" "$latest_message_id" <<'PY'
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
    bootstrap_path,
    repo_root,
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
        "Reference files:",
        f"- bootstrap packet: {bootstrap_path}",
        f"- role prompt: {repo_root}/agents/roles/{role}/prompt.md",
        f"- role context: {repo_root}/agents/roles/{role}/context.md",
        f"- shared principles: {repo_root}/agents/shared/principles.md",
        f"- shared workflow: {repo_root}/agents/shared/workflow.md",
        f"- shared terminology: {repo_root}/agents/shared/terminology.md",
        "",
        "Startup behavior:",
        "- first inspect relevant workspace messages and your assigned tasks",
        "- if you do not have actionable work, exit quickly and let the supervisor keep polling",
        "- read the referenced files from disk only when needed",
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
  codex mcp remove "$mcp_name" >/dev/null 2>&1 || true
  rm -f "$runtime_prompt"
}

trap cleanup EXIT INT TERM

codex mcp remove "$mcp_name" >/dev/null 2>&1 || true
codex mcp add "$mcp_name" \
  --env "INFOSPHERE_API_BASE_URL=$api_base_url" \
  --env "DOTNET_CLI_HOME=/tmp" \
  -- \
  dotnet run --project "$repo_root/src/Infosphere.Mcp/Infosphere.Mcp.csproj" >/dev/null

maybe_clear
echo "Bootstrapped agent supervisor."
echo "Role: $role"
echo "Runtime: $runtime"
echo "Workspace ID: $workspace_id"
echo "Session ID: $session_id"
echo "Bootstrap packet: $bootstrap_path"
echo "State file: $state_file"
echo
echo "Supervisor behavior:"
echo "- keeps the session active"
echo "- heartbeats every 30 seconds"
echo "- polls Infosphere for work"
echo "- only launches Codex when there is actionable work"
echo

last_seen_message_id="$(latest_workspace_message_id)"
printf 'LAST_SEEN_MESSAGE_ID=%q\n' "$last_seen_message_id" > "$state_file"

start_heartbeat_loop

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

  if [[ "$role" == "coordinator" ]]; then
    available_count="$(available_tasks_count)"
    current_latest_message_id="$(latest_workspace_message_id)"
    if [[ -n "$current_latest_message_id" && "$current_latest_message_id" != "${LAST_SEEN_MESSAGE_ID:-}" ]]; then
      trigger_reason="new workspace message"
      latest_message_id="$current_latest_message_id"
      printf 'LAST_SEEN_MESSAGE_ID=%q\n' "$current_latest_message_id" > "$state_file"
    elif [[ "$available_count" -gt 0 ]]; then
      trigger_reason="available tasks exist"
    elif [[ "$assigned_count" -gt 0 ]]; then
      trigger_reason="assigned tasks exist"
    fi
  else
    if [[ "$assigned_count" -gt 0 ]]; then
      trigger_reason="assigned tasks exist"
    fi
  fi

  if [[ -n "$trigger_reason" ]]; then
    build_runtime_prompt "$trigger_reason" "$assigned_summary" "$latest_message_id"
    maybe_clear
    echo "Launching Codex for $session_name"
    echo "Reason: $trigger_reason"
    echo "Session ID: $session_id"
    echo "Runtime prompt: $runtime_prompt"
    echo
    codex --dangerously-bypass-approvals-and-sandbox "$(cat "$runtime_prompt")" || true
    echo
    echo "Codex exited. Supervisor will continue polling."
    sleep 5
  else
    sleep 15
  fi
done
