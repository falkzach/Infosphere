# Infosphere Postgresql Database Migrations

This project applies Postgres migrations with `DbUp`.

Unlike the Foretell backend pattern that used one database per domain, Infosphere
uses one database with multiple schemas. Each subfolder in `Scripts/` maps to a
schema and gets its own migration journal table.

This project also owns schema validation and generated Core models. Database
objects should not be hand-maintained in `Infosphere.Core`; instead, generate
`.gen.cs` files from the migrated database shape.

## Schema folders

- `Scripts/catalog`: root metadata such as brain profiles and workspaces
- `Scripts/coordination`: hot workflow state such as tasks, sessions, leases, and state-machine lookups
- `Scripts/memory`: larger context, workspace messages, and durable memory records

`coordination.agent_session_heartbeats` is intentionally append-only and narrow.
Use the `coordination.prune_agent_session_heartbeats(...)` function from a job
runner or scheduler to keep retention bounded.

## Adding a migration

Add a new SQL script under the relevant schema folder:

- leading index
- action and entity
- concise description

Example: `002-create_table_tasks.sql`

Keep mutable operational data in narrow tables. Put variable, agent-specific, or
heavy context payloads in separate tables with `jsonb` columns.

## Running locally

Start Postgres:

```bash
docker compose up -d db
```

Run migrations:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project src/Infosphere.Postgresql.Db -- migrate
```

Validate migrations in a temporary database:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project src/Infosphere.Postgresql.Db -- validate
```

Generate `.gen.cs` models from the configured database:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project src/Infosphere.Postgresql.Db -- generate
```

Validate migrations and then generate models from the validated schema:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project src/Infosphere.Postgresql.Db -- sync-models
```
