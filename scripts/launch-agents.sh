#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./scripts/launch-agents.sh [--runtime <runtime>] [--attach <session>]

Supported runtimes:
  codex
  claude
  gemini
  antigravity

Named tmux sessions created:
  infosphere-coordinator
  infosphere-backend
  infosphere-frontend
  infosphere-ux
EOF
}

runtime="codex"
attach_session=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --runtime)
      runtime="${2:-}"
      shift 2
      ;;
    --attach)
      attach_session="${2:-}"
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

case "$runtime" in
  codex|claude|gemini|antigravity)
    ;;
  *)
    echo "Unsupported runtime: $runtime" >&2
    exit 1
    ;;
esac

if ! command -v tmux >/dev/null 2>&1; then
  echo "tmux is required but was not found in PATH." >&2
  exit 1
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

launch_one() {
  local role="$1"
  local session_name="$2"
  local worktree_path="$3"
  local bootstrap_path="$4"

  if [[ ! -d "$worktree_path" ]]; then
    echo "Missing worktree: $worktree_path" >&2
    exit 1
  fi

  "$repo_root/scripts/bootstrap-agent.sh" \
    --role "$role" \
    --runtime "$runtime" \
    --write "$bootstrap_path" >/dev/null

  if tmux has-session -t "$session_name" 2>/dev/null; then
    tmux kill-session -t "$session_name"
  fi

  tmux new-session -d -s "$session_name" -n bootstrap -c "$worktree_path" \
    "bash -lc 'clear; echo Session: $session_name; echo Role: $role; echo Runtime: $runtime; echo Bootstrap packet: $bootstrap_path; echo Worktree: $worktree_path; echo; sed -n \"1,220p\" \"$bootstrap_path\"; echo; echo \"Press q if you open this again with less.\"; exec bash'"

  tmux new-window -t "$session_name" -n agent -c "$worktree_path" \
    "bash -lc 'bash scripts/run-agent.sh --role \"$role\" --runtime \"$runtime\" --session-name \"$session_name\" --bootstrap-path \"$bootstrap_path\"; exit_code=\$?; echo; echo Agent process exited with code \$exit_code; exec bash'"

  tmux select-window -t "$session_name:agent"
}

launch_one "coordinator" "infosphere-coordinator" "/home/falkzach/code/Infosphere-coordinator" "/tmp/infosphere-coordinator.md"
launch_one "csharp-backend-implementor" "infosphere-backend" "/home/falkzach/code/Infosphere-backend" "/tmp/infosphere-backend.md"
launch_one "vite-react-frontend-implementor" "infosphere-frontend" "/home/falkzach/code/Infosphere-frontend" "/tmp/infosphere-frontend.md"
launch_one "user-experience-manager" "infosphere-ux" "/home/falkzach/code/Infosphere-ux" "/tmp/infosphere-ux.md"

cat <<EOF
Launched tmux sessions:
  infosphere-coordinator
  infosphere-backend
  infosphere-frontend
  infosphere-ux

Bootstrap packets:
  /tmp/infosphere-coordinator.md
  /tmp/infosphere-backend.md
  /tmp/infosphere-frontend.md
  /tmp/infosphere-ux.md

Attach with:
  tmux attach -t infosphere-coordinator
  tmux attach -t infosphere-backend
  tmux attach -t infosphere-frontend
  tmux attach -t infosphere-ux
EOF

if [[ -n "$attach_session" ]]; then
  exec tmux attach -t "$attach_session"
fi
