CREATE TABLE IF NOT EXISTS coordination.task_states (
    id INTEGER PRIMARY KEY,
    key TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_task_states_key
    ON coordination.task_states (key);

INSERT INTO coordination.task_states (id, key, name, description)
VALUES
    (0, 'created', 'Created', 'Task exists but has not yet been accepted by an agent.'),
    (1, 'ready', 'Ready', 'Task is ready to be picked up.'),
    (2, 'in_progress', 'In Progress', 'Task is actively being worked.'),
    (3, 'blocked', 'Blocked', 'Task cannot proceed until a dependency is resolved.'),
    (4, 'completed', 'Completed', 'Task finished successfully.'),
    (5, 'cancelled', 'Cancelled', 'Task was intentionally stopped and will not continue.'),
    (99, 'error', 'Error', 'Task encountered an unrecoverable failure and requires inspection.')
ON CONFLICT (id) DO NOTHING;
