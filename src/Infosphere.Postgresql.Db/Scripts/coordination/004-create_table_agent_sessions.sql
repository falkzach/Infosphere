CREATE TABLE IF NOT EXISTS coordination.agent_sessions (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    agent_id TEXT NOT NULL,
    agent_kind TEXT NOT NULL,
    state_id INTEGER NOT NULL,
    display_name TEXT NOT NULL,
    current_task_id UUID NULL,
    capabilities JSONB NOT NULL DEFAULT '{}'::jsonb,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    started_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    heartbeat_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_utc TIMESTAMPTZ NULL,
    CONSTRAINT fk_agent_sessions_workspace
        FOREIGN KEY (workspace_id)
        REFERENCES catalog.workspaces (id),
    CONSTRAINT fk_agent_sessions_state
        FOREIGN KEY (state_id)
        REFERENCES coordination.agent_session_states (id),
    CONSTRAINT fk_agent_sessions_task
        FOREIGN KEY (current_task_id)
        REFERENCES coordination.tasks (id)
);

CREATE INDEX IF NOT EXISTS ix_agent_sessions_workspace_state
    ON coordination.agent_sessions (workspace_id, state_id);

CREATE INDEX IF NOT EXISTS ix_agent_sessions_agent
    ON coordination.agent_sessions (agent_id);

CREATE INDEX IF NOT EXISTS ix_agent_sessions_heartbeat
    ON coordination.agent_sessions (heartbeat_utc);
