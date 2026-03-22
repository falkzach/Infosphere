#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash scripts/close-agent-sessions.sh [--tmux] [--dry-run]

Options:
  --tmux
    Also kill the standard Infosphere tmux sessions after closing database sessions.

  --dry-run
    Show the currently non-closed sessions without updating them.
EOF
}

close_tmux="false"
dry_run="false"
container_name="${INFOSPHERE_DB_CONTAINER:-infosphere_postgresql_db}"
db_name="${INFOSPHERE_DB_NAME:-infosphere}"
db_user="${INFOSPHERE_DB_USER:-master}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tmux)
      close_tmux="true"
      shift
      ;;
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

query_open_sessions="
select id, display_name, state_id, heartbeat_utc, ended_utc
from coordination.agent_sessions
where state_id <> 5
order by started_utc desc;
"

echo "Inspecting non-closed agent sessions in container '$container_name'..."
docker exec "$container_name" psql -v ON_ERROR_STOP=1 -U "$db_user" -d "$db_name" -c "$query_open_sessions"

if [[ "$dry_run" == "true" ]]; then
  echo
  echo "Dry run only. No sessions were updated."
  exit 0
fi

close_sessions_query="
update coordination.agent_sessions
set state_id = 5,
    ended_utc = now(),
    heartbeat_utc = now(),
    current_task_id = null
where state_id <> 5
returning id, display_name, state_id, ended_utc;
"

echo
echo "Closing non-closed agent sessions..."
docker exec "$container_name" psql -v ON_ERROR_STOP=1 -U "$db_user" -d "$db_name" -c "$close_sessions_query"

if [[ "$close_tmux" == "true" ]]; then
  echo
  echo "Killing standard Infosphere tmux sessions..."
  for session_name in \
    infosphere-coordinator \
    infosphere-implementor-1 \
    infosphere-implementor-2 \
    infosphere-implementor-3 \
    infosphere-ux
  do
    if tmux has-session -t "$session_name" 2>/dev/null; then
      tmux kill-session -t "$session_name"
      echo "  killed $session_name"
    else
      echo "  not running: $session_name"
    fi
  done
fi

echo
echo "Remaining non-closed sessions:"
docker exec "$container_name" psql -v ON_ERROR_STOP=1 -U "$db_user" -d "$db_name" -c "$query_open_sessions"
