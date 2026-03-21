CREATE TABLE IF NOT EXISTS coordination.agent_session_heartbeats (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    agent_session_id UUID NOT NULL,
    observed_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_agent_session_heartbeats_session
        FOREIGN KEY (agent_session_id)
        REFERENCES coordination.agent_sessions (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_agent_session_heartbeats_session_observed
    ON coordination.agent_session_heartbeats (agent_session_id, observed_utc DESC);

CREATE INDEX IF NOT EXISTS ix_agent_session_heartbeats_observed_brin
    ON coordination.agent_session_heartbeats
    USING BRIN (observed_utc);

CREATE OR REPLACE FUNCTION coordination.prune_agent_session_heartbeats(
    retention_window INTERVAL DEFAULT INTERVAL '7 days',
    batch_size INTEGER DEFAULT 10000)
RETURNS INTEGER
LANGUAGE plpgsql
AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    WITH expired AS (
        SELECT id
        FROM coordination.agent_session_heartbeats
        WHERE observed_utc < NOW() - retention_window
        ORDER BY observed_utc
        LIMIT batch_size
    )
    DELETE FROM coordination.agent_session_heartbeats heartbeats
    USING expired
    WHERE heartbeats.id = expired.id;

    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$;
