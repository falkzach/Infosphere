#!/usr/bin/env bash
set -euo pipefail

# Quick health check: tmux session state, open DB sessions, and task summary.

container_name="${INFOSPHERE_DB_CONTAINER:-infosphere_postgresql_db}"
db_name="${INFOSPHERE_DB_NAME:-infosphere}"
db_user="${INFOSPHERE_DB_USER:-master}"

known_sessions=(
  infosphere-coordinator
  infosphere-implementor-1
  infosphere-implementor-2
  infosphere-implementor-3
  infosphere-ux
)

echo "=== tmux sessions ==="
for session_name in "${known_sessions[@]}"; do
  if tmux has-session -t "$session_name" 2>/dev/null; then
    echo "  running: $session_name"
  else
    echo "  stopped: $session_name"
  fi
done

echo
echo "=== agent sessions (DB) ==="
# agent_session_states: 5=closed
docker exec "$container_name" psql -U "$db_user" -d "$db_name" -t -A -F'  ' -c "
SELECT
  s.display_name,
  ss.key AS state,
  to_char(s.heartbeat_utc, 'YYYY-MM-DD HH24:MI:SS') AS last_heartbeat
FROM coordination.agent_sessions s
JOIN coordination.agent_session_states ss ON ss.id = s.state_id
ORDER BY s.started_utc DESC
LIMIT 10;"

echo
echo "=== task summary ==="
# task_states: 0=created, 1=ready, 2=in_progress, 3=blocked, 4=completed, 5=cancelled, 99=error
docker exec "$container_name" psql -U "$db_user" -d "$db_name" -t -A -F'  ' -c "
SELECT
  ts.key AS state,
  COUNT(*) AS count
FROM coordination.tasks t
JOIN coordination.task_states ts ON ts.id = t.state_id
GROUP BY ts.id, ts.key
ORDER BY ts.id;"
