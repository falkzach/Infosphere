#!/usr/bin/env bash
set -euo pipefail

# Full reset: closes DB sessions, kills tmux sessions, clears local agent state.
# Equivalent to running close-agent-sessions.sh --tmux then clearing state files.

usage() {
  cat <<'EOF'
Usage:
  bash scripts/reset.sh [--dry-run]

Options:
  --dry-run
    Show what would be reset without making any changes.
EOF
}

dry_run="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run)
      dry_run="true"
      shift
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

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
state_dir="/tmp/infosphere-agent-state"

if [[ "$dry_run" == "true" ]]; then
  echo "--- dry run: DB sessions ---"
  bash "$repo_root/scripts/close-agent-sessions.sh" --dry-run
  echo
  echo "--- dry run: state directory ---"
  if [[ -d "$state_dir" ]]; then
    echo "Would remove: $state_dir"
    ls "$state_dir" | sed 's/^/  /'
  else
    echo "State directory does not exist: $state_dir"
  fi
  exit 0
fi

echo "--- Closing DB sessions and killing tmux sessions ---"
bash "$repo_root/scripts/close-agent-sessions.sh" --tmux

echo
echo "--- Clearing agent state directory ---"
if [[ -d "$state_dir" ]]; then
  rm -rf "$state_dir"
  echo "Removed: $state_dir"
else
  echo "State directory already absent: $state_dir"
fi

echo
echo "Reset complete."
