CREATE TABLE IF NOT EXISTS coordination.task_updates
(
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    task_id UUID NOT NULL,
    agent_session_id UUID NULL,
    update_kind TEXT NOT NULL,
    summary TEXT NOT NULL,
    details JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_task_updates_task
        FOREIGN KEY (task_id)
        REFERENCES coordination.tasks (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_task_updates_agent_session
        FOREIGN KEY (agent_session_id)
        REFERENCES coordination.agent_sessions (id)
        ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_task_updates_task_id_created_utc
    ON coordination.task_updates (task_id, created_utc DESC);
