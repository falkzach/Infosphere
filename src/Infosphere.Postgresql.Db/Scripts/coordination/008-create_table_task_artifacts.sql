CREATE TABLE IF NOT EXISTS coordination.task_artifacts
(
    id UUID PRIMARY KEY,
    task_id UUID NOT NULL,
    agent_session_id UUID NULL,
    artifact_kind TEXT NOT NULL,
    value TEXT NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_task_artifacts_task
        FOREIGN KEY (task_id)
        REFERENCES coordination.tasks (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_task_artifacts_agent_session
        FOREIGN KEY (agent_session_id)
        REFERENCES coordination.agent_sessions (id)
        ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_task_artifacts_task_id_created_utc
    ON coordination.task_artifacts (task_id, created_utc DESC);
