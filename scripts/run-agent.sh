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

register_response="$(python3 "$repo_root/scripts/mcp_tool.py" --tool register_agent_session --arguments "$register_args" --api-base-url "$api_base_url")"
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

cleanup() {
  set +e
  if [[ -n "${session_id:-}" ]]; then
    close_args="$(python3 - "$session_id" <<'PY'
import json
import sys
print(json.dumps({"sessionId": sys.argv[1]}))
PY
)"
    python3 "$repo_root/scripts/mcp_tool.py" --tool close_agent_session --arguments "$close_args" --api-base-url "$api_base_url" >/dev/null 2>&1 || true
  fi
  codex mcp remove "$mcp_name" >/dev/null 2>&1 || true
  rm -f "$runtime_prompt"
}

trap cleanup EXIT INT TERM

cat > "$runtime_prompt" <<EOF
# Runtime Session Context

You are already registered in Infosphere.

- role: $role
- runtime: $runtime
- workspaceId: $workspace_id
- sessionId: $session_id
- agentId: $agent_id
- displayName: $display_name

Operational rules for this live session:
- use the existing Infosphere session above
- do not register a second session
- use heartbeat_agent_session with sessionId \`$session_id\` while you are active
- before ending work, call close_agent_session with sessionId \`$session_id\`
- if there are no available tasks or actionable messages, stay idle and do not invent work

Repo bootstrap packet follows.

EOF

cat "$bootstrap_path" >> "$runtime_prompt"

codex mcp remove "$mcp_name" >/dev/null 2>&1 || true
codex mcp add "$mcp_name" \
  --env "INFOSPHERE_API_BASE_URL=$api_base_url" \
  --env "DOTNET_CLI_HOME=/tmp" \
  -- \
  dotnet run --project "$repo_root/src/Infosphere.Mcp/Infosphere.Mcp.csproj" >/dev/null

clear
echo "Bootstrapped agent session."
echo "Role: $role"
echo "Runtime: $runtime"
echo "Workspace ID: $workspace_id"
echo "Session ID: $session_id"
echo "Bootstrap packet: $bootstrap_path"
echo "Runtime prompt: $runtime_prompt"
echo
echo "Launching Codex with Infosphere MCP attached..."
echo

codex --full-auto "$(cat "$runtime_prompt")"
