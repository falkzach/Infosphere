CREATE TABLE IF NOT EXISTS memory.context_entries (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    task_id UUID NULL,
    entry_type TEXT NOT NULL,
    summary TEXT NOT NULL,
    payload JSONB NOT NULL,
    created_by TEXT NOT NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE memory.context_entries
    DROP CONSTRAINT IF EXISTS fk_context_entries_workspace;

ALTER TABLE memory.context_entries
    ADD CONSTRAINT fk_context_entries_workspace
    FOREIGN KEY (workspace_id)
    REFERENCES catalog.workspaces (id);

CREATE INDEX IF NOT EXISTS ix_context_entries_workspace_type
    ON memory.context_entries (workspace_id, entry_type);

CREATE INDEX IF NOT EXISTS ix_context_entries_payload_gin
    ON memory.context_entries
    USING GIN (payload);
