CREATE TABLE IF NOT EXISTS coordination.agent_session_states (
    id INTEGER PRIMARY KEY,
    key TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_agent_session_states_key
    ON coordination.agent_session_states (key);

INSERT INTO coordination.agent_session_states (id, key, name, description)
VALUES
    (0, 'created', 'Created', 'Session record exists but the agent has not started work yet.'),
    (1, 'active', 'Active', 'Agent is actively participating in the workspace.'),
    (2, 'idle', 'Idle', 'Agent is connected but not currently executing work.'),
    (3, 'blocked', 'Blocked', 'Agent cannot continue until an external dependency is resolved.'),
    (4, 'closing', 'Closing', 'Agent is finishing work and preparing to end the session.'),
    (5, 'closed', 'Closed', 'Session has ended and should be treated as immutable history.'),
    (99, 'error', 'Error', 'Session encountered an unrecoverable failure and requires inspection.')
ON CONFLICT (id) DO NOTHING;
