#!/usr/bin/env bash
set -euo pipefail

# Creates the five standard agent worktrees from main.
# Safe to re-run: skips directories that already exist as worktrees.

usage() {
  cat <<'EOF'
Usage:
  bash scripts/setup-worktrees.sh [--base-dir <path>]

Options:
  --base-dir <path>
    Parent directory for worktrees. Defaults to the parent of the repo root.
    Worktrees will be created as <base-dir>/Infosphere-<role>.
EOF
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
base_dir="$(dirname "$repo_root")"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-dir)
      base_dir="${2:-}"
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

# worktree-dir → branch
declare -A worktrees=(
  ["Infosphere-coordinator"]="agent/coordinator"
  ["Infosphere-implementor-1"]="agent/implementor-1"
  ["Infosphere-implementor-2"]="agent/implementor-2"
  ["Infosphere-implementor-3"]="agent/implementor-3"
  ["Infosphere-ux"]="agent/ux"
)

# Ordered for consistent output
worktree_order=(
  Infosphere-coordinator
  Infosphere-implementor-1
  Infosphere-implementor-2
  Infosphere-implementor-3
  Infosphere-ux
)

echo "Setting up worktrees under: $base_dir"
echo

for dir in "${worktree_order[@]}"; do
  branch="${worktrees[$dir]}"
  path="$base_dir/$dir"
  if git -C "$repo_root" worktree list --porcelain | grep -q "worktree $path$"; then
    echo "  exists:  $path"
  elif [[ -d "$path" ]]; then
    echo "  warning: $path exists but is not a registered worktree — skipping" >&2
  else
    git -C "$repo_root" worktree add "$path" -b "$branch" main
    echo "  created: $path (branch: $branch)"
  fi
done

echo
echo "Registered worktrees:"
git -C "$repo_root" worktree list
