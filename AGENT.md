# Agent Base Instructions

This repository is a shared coordination backend for agentic coding workflows.

All agents working here should follow these baseline rules:

## Role

- treat `Infosphere.Api` as the system of record
- treat `Infosphere.Mcp` as the agent-facing adapter layer
- treat Postgres schema and generated Core models as authoritative for persisted shapes

## Engineering Rules

- do not hand-maintain database-shaped entities in `Infosphere.Core`
- when database shape changes, run the migration validation and model generation workflow
- prefer extending the API and MCP together when adding agent-facing functionality
- keep mutable workflow state narrow and relational
- keep heavier or variable context in dedicated tables rather than hot coordination rows

## Coordination Rules

- use agent sessions for work ownership
- prefer session-based task claims over freeform agent identifiers
- close stale or finished sessions explicitly
- write durable workspace communication as workspace messages or context entries, not hidden local notes
- each participating agent should work from its own git worktree
- do not share one checked-out working directory across multiple active agents

## Worktree Rule

Each active agent should get:
- its own git worktree
- its own branch unless there is a strong reason not to
- its own local runtime/process context

## Authority Model

Bootstrapped local agents are expected to run with enough authority to do normal development work in their dedicated worktrees.

That includes:
- editing code
- running builds and tests
- running migration and generation workflows
- restarting local containers and services
- performing other ordinary local development operations required to complete assigned tasks

Do not bootstrap agents into an approval mode that blocks routine work unless there is a specific reason to constrain that session.

A simple pattern is:

```bash
git worktree add ../Infosphere-coordinator -b agent/coordinator main
git worktree add ../Infosphere-backend -b agent/backend main
git worktree add ../Infosphere-frontend -b agent/frontend main
git worktree add ../Infosphere-ux -b agent/ux main
```

This reduces conflicting edits, confused file state, and accidental cross-agent interference.

## Project Map

- `src/Infosphere.Api`
- `src/Infosphere.Core`
- `src/Infosphere.Mcp`
- `src/Infosphere.Postgresql.Db`
- `src/Infosphere.Web`

## Common Commands

Build:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet build Infosphere.slnx
```

Database validation and model sync:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet run --project src/Infosphere.Postgresql.Db -- sync-models
```

Full stack:

```bash
docker compose up -d --build
```
