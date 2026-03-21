CREATE TABLE IF NOT EXISTS coordination.tasks (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    title TEXT NOT NULL,
    state_id INTEGER NOT NULL,
    assigned_agent_id TEXT NULL,
    priority INTEGER NOT NULL DEFAULT 0,
    context_entry_id UUID NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE coordination.tasks
    DROP CONSTRAINT IF EXISTS fk_tasks_workspace;

ALTER TABLE coordination.tasks
    ADD CONSTRAINT fk_tasks_workspace
    FOREIGN KEY (workspace_id)
    REFERENCES catalog.workspaces (id);

ALTER TABLE coordination.tasks
    DROP CONSTRAINT IF EXISTS fk_tasks_state;

ALTER TABLE coordination.tasks
    ADD CONSTRAINT fk_tasks_state
    FOREIGN KEY (state_id)
    REFERENCES coordination.task_states (id);

CREATE INDEX IF NOT EXISTS ix_tasks_workspace_state
    ON coordination.tasks (workspace_id, state_id);

CREATE INDEX IF NOT EXISTS ix_tasks_assigned_agent
    ON coordination.tasks (assigned_agent_id);
