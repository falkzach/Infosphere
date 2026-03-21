CREATE TABLE IF NOT EXISTS memory.workspace_messages (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    author_type TEXT NOT NULL,
    author_id TEXT NULL,
    message_kind TEXT NOT NULL,
    content TEXT NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE memory.workspace_messages
    DROP CONSTRAINT IF EXISTS fk_workspace_messages_workspace;

ALTER TABLE memory.workspace_messages
    ADD CONSTRAINT fk_workspace_messages_workspace
    FOREIGN KEY (workspace_id)
    REFERENCES catalog.workspaces (id);

CREATE INDEX IF NOT EXISTS ix_workspace_messages_workspace_created
    ON memory.workspace_messages (workspace_id, created_utc DESC);

CREATE INDEX IF NOT EXISTS ix_workspace_messages_workspace_kind_created
    ON memory.workspace_messages (workspace_id, message_kind, created_utc DESC);
