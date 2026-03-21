# Infosphere

Infosphere is a shared coordination and memory backend for agentic coding workflows.

The current shape is:
- `Infosphere.Api` as the system-of-record HTTP API
- `Infosphere.Postgresql.Db` as the migration and schema-generation project
- `Infosphere.Mcp` as the stdio MCP adapter for agents
- `Infosphere.Web` as the browser UI for observing live work

The design goal is agent-agnostic collaboration. Agents should not coordinate through ad hoc markdown files when the workflow needs durable state, concurrent writes, shared task ownership, and queryable memory.

## Architecture

### Data

Postgres is the backing store.

The database is split into schemas by workload:
- `catalog`
  - durable metadata such as brain profiles and workspaces
- `coordination`
  - hot operational state such as tasks, task states, agent sessions, agent session states, and heartbeat history
- `memory`
  - larger context and append-only communication such as context entries and workspace messages

State machines use integer-backed lookup tables so hot rows stay narrow while still exposing readable values.

### Services

- [src/Infosphere.Api](/home/falkzach/code/Infosphere/src/Infosphere.Api)
  - ASP.NET Core API
  - OpenAPI at `/openapi/v0.json`
  - health endpoints at `/healthz`, `/readyz`, `/startupz`
- [src/Infosphere.Mcp](/home/falkzach/code/Infosphere/src/Infosphere.Mcp)
  - stdio MCP server
  - translates MCP tools into `Infosphere.Api` calls
- [src/Infosphere.Web](/home/falkzach/code/Infosphere/src/Infosphere.Web)
  - Vite/Vitest React TypeScript frontend
  - served by Nginx in Docker
- [src/Infosphere.Postgresql.Db](/home/falkzach/code/Infosphere/src/Infosphere.Postgresql.Db)
  - migrations
  - validation
  - generated `.gen.cs` entities into `Infosphere.Core`

## Project Layout

- [src/Infosphere.Api](/home/falkzach/code/Infosphere/src/Infosphere.Api)
- [src/Infosphere.Core](/home/falkzach/code/Infosphere/src/Infosphere.Core)
- [src/Infosphere.Mcp](/home/falkzach/code/Infosphere/src/Infosphere.Mcp)
- [src/Infosphere.Postgresql.Db](/home/falkzach/code/Infosphere/src/Infosphere.Postgresql.Db)
- [src/Infosphere.Web](/home/falkzach/code/Infosphere/src/Infosphere.Web)
- [tests/Infosphere.Core.Tests](/home/falkzach/code/Infosphere/tests/Infosphere.Core.Tests)
- [tests/Infosphere.Mcp.Tests](/home/falkzach/code/Infosphere/tests/Infosphere.Mcp.Tests)

## Current API Surface

The API currently supports:
- workspaces
- tasks
- agent sessions
- workspace messages
- health and readiness probes

The MCP currently exposes:
- `list_workspaces`
- `register_agent_session`
- `heartbeat_agent_session`
- `list_tasks`
- `claim_task`
- `transition_task_state`
- `list_workspace_messages`
- `post_workspace_message`

## Local Development

### Full stack with Docker

Start the full stack:

```bash
docker compose up -d --build
```

Services:
- API: `http://localhost:5080`
- Web: `http://localhost:5081`
- Postgres: `localhost:15432`

### Database workflow

Start just Postgres:

```bash
docker compose up -d db
```

Run migrations:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet run --project src/Infosphere.Postgresql.Db -- migrate
```

Validate migrations:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet run --project src/Infosphere.Postgresql.Db -- validate
```

Validate and regenerate Core models:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet run --project src/Infosphere.Postgresql.Db -- sync-models
```

## Build And Test

Build the solution:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet build Infosphere.slnx
```

Run Core tests:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet test tests/Infosphere.Core.Tests/Infosphere.Core.Tests.csproj
```

Run MCP tests:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet test tests/Infosphere.Mcp.Tests/Infosphere.Mcp.Tests.csproj
```

Run frontend tests:

```bash
cd src/Infosphere.Web
npm test
```

## MCP Usage

`Infosphere.Mcp` is a stdio server, not a web service. It should usually be launched by an MCP-capable client runtime and pointed at `Infosphere.Api`.

Example:

```bash
INFOSPHERE_API_BASE_URL=http://localhost:5080 \
DOTNET_CLI_HOME=/tmp \
dotnet run --project src/Infosphere.Mcp/Infosphere.Mcp.csproj --no-build
```

See [src/Infosphere.Mcp/README.md](/home/falkzach/code/Infosphere/src/Infosphere.Mcp/README.md) for the current tool surface and framing examples.

## Agent Instruction Files

Layered agent guidance lives at the repo root:

- [AGENT.md](/home/falkzach/code/Infosphere/AGENT.md)
- [CODEX.md](/home/falkzach/code/Infosphere/CODEX.md)
- [CLAUDE.md](/home/falkzach/code/Infosphere/CLAUDE.md)
- [GEMINI.md](/home/falkzach/code/Infosphere/GEMINI.md)
- [ANTIGRAVITY.md](/home/falkzach/code/Infosphere/ANTIGRAVITY.md)

Role-specific startup prompts and contexts live under [agents/](/home/falkzach/code/Infosphere/agents):

- [agents/manifest.json](/home/falkzach/code/Infosphere/agents/manifest.json)
- [agents/BOOTSTRAP.md](/home/falkzach/code/Infosphere/agents/BOOTSTRAP.md)
- [agents/roles/coordinator/prompt.md](/home/falkzach/code/Infosphere/agents/roles/coordinator/prompt.md)
- [agents/roles/csharp-backend-implementor/prompt.md](/home/falkzach/code/Infosphere/agents/roles/csharp-backend-implementor/prompt.md)
- [agents/roles/vite-react-frontend-implementor/prompt.md](/home/falkzach/code/Infosphere/agents/roles/vite-react-frontend-implementor/prompt.md)
- [agents/roles/user-experience-manager/prompt.md](/home/falkzach/code/Infosphere/agents/roles/user-experience-manager/prompt.md)

## Near-Term Work

- add richer task coordination semantics around claims and transitions
- add context entry API and MCP tools
- add task availability and task creation tools to MCP
- add scripted multi-agent end-to-end scenarios
