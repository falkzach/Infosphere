CREATE TABLE IF NOT EXISTS catalog.workspaces (
    id UUID PRIMARY KEY,
    brain_profile_id UUID NOT NULL,
    key TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_workspaces_brain_profile
        FOREIGN KEY (brain_profile_id)
        REFERENCES catalog.brain_profiles (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_workspaces_brain_profile_key
    ON catalog.workspaces (brain_profile_id, key);
