CREATE TABLE IF NOT EXISTS coordination.task_checklist_items
(
    id UUID PRIMARY KEY,
    task_id UUID NOT NULL,
    ordinal INTEGER NOT NULL,
    title TEXT NOT NULL,
    is_required BOOLEAN NOT NULL DEFAULT TRUE,
    is_completed BOOLEAN NOT NULL DEFAULT FALSE,
    completed_utc TIMESTAMPTZ NULL,
    completed_by_agent_session_id UUID NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_task_checklist_items_task
        FOREIGN KEY (task_id)
        REFERENCES coordination.tasks (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_task_checklist_items_completed_by_session
        FOREIGN KEY (completed_by_agent_session_id)
        REFERENCES coordination.agent_sessions (id)
        ON DELETE SET NULL,
    CONSTRAINT ux_task_checklist_items_task_ordinal UNIQUE (task_id, ordinal)
);

CREATE INDEX IF NOT EXISTS ix_task_checklist_items_task_id
    ON coordination.task_checklist_items (task_id, ordinal);
