# Infosphere

Infosphere is a shared coordination and memory backend for agentic coding workflows.

The current shape is:
- `Infosphere.Api` as the system-of-record HTTP API
- `Infosphere.Postgresql.Db` as the migration and schema-generation project
- `Infosphere.Mcp` as the stdio MCP adapter for agents
- `Infosphere.Web` as the browser UI for observing live work

The design goal is agent-agnostic collaboration. Agents should not coordinate through ad hoc markdown files when the workflow needs durable state, concurrent writes, shared task ownership, and queryable memory.

## Runtime Model

Infosphere now supports a lightweight multi-agent runtime loop built around:
- dedicated git worktrees per agent
- a stdio MCP adapter over the API
- supervisor-driven bootstrapping for local Codex agents
- cached context images to avoid rebuilding startup context on every wake
- low-token polling so agents sleep until there is actionable work

The intended local agent topology is:
- 1 coordinator
- 3 implementors
- 1 user experience manager

Each active agent runs in its own worktree and its own `tmux` session.

## Architecture

### Data

Postgres is the backing store.

The database is split into schemas by workload:
- `catalog`
  - durable metadata such as brain profiles and workspaces
- `coordination`
  - hot operational state such as tasks, task states, task checklist items, task updates, task artifacts, agent sessions, agent session states, and heartbeat history
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
- task execution details
  - checklist items
  - structured task updates
  - task artifacts
- agent sessions
- workspace messages
- health and readiness probes

The MCP currently exposes:
- `list_workspaces`
- `register_agent_session`
- `heartbeat_agent_session`
- `list_tasks`
- `list_available_tasks`
- `create_task`
- `claim_task`
- `transition_task_state`
- `close_agent_session`
- `get_task_execution`
- `add_task_checklist_item`
- `complete_task_checklist_item`
- `post_task_update`
- `add_task_artifact`
- `list_workspace_messages`
- `post_workspace_message`

## Local Development

### Full stack with Docker

Start the full stack:

```bash
docker compose up -d --build
```

Reset bootstrapped agent runtime state if needed:

```bash
bash scripts/close-agent-sessions.sh --tmux
```

Services:
- API: `http://localhost:5080`
- Web: `http://localhost:5081`
- Postgres: `localhost:15432`

### Agent bootstrap

Bootstrapped local agents are launched through supervisor scripts rather than by hand-assembling prompts each time.

Important scripts:
- [scripts/bootstrap-agent.sh](/home/falkzach/code/Infosphere/scripts/bootstrap-agent.sh)
  - generates a role-specific bootstrap packet
- [scripts/build-context-image.sh](/home/falkzach/code/Infosphere/scripts/build-context-image.sh)
  - builds a cached context image and manifest for startup reuse
- [scripts/launch-agents.sh](/home/falkzach/code/Infosphere/scripts/launch-agents.sh)
  - launches the standard local agent set in `tmux`
- [scripts/close-agent-sessions.sh](/home/falkzach/code/Infosphere/scripts/close-agent-sessions.sh)
  - closes live agent sessions and optionally kills the standard `tmux` sessions

Launch the standard local set:

```bash
bash scripts/launch-agents.sh --runtime codex
```

Current standard sessions:
- `infosphere-coordinator`
- `infosphere-implementor-1`
- `infosphere-implementor-2`
- `infosphere-implementor-3`
- `infosphere-ux`

### Context image caching

Agent startup uses a cached `context image` so the runtime does not have to reconstruct its full prompt from disk on every wake.

The cached image includes:
- runtime overlay
- shared agent guidance
- role prompt and role context
- bootstrap metadata
- a compact repo snapshot

The cache is refreshed before wake-ups and then reused as the baseline prompt context. This reduces:
- repeated prompt assembly
- repeated file scanning
- unnecessary startup tokens
- wasted work before the first MCP call

### Token minimization and sleep strategy

The local Codex supervisor is designed to keep agents cheap while still “alive”.

Behavior:
- registers one Infosphere session per agent
- heartbeats every 30 seconds
- polls cheaply through MCP
- keeps the session open while idle
- only launches the LLM when there is actionable work

Wake conditions:
- coordinator wakes for new workspace messages, available tasks, or assigned tasks
- implementors wake only for assigned tasks
- UX manager wakes only for assigned tasks

After a run:
- the wrapper keeps polling instead of shutting down immediately
- the agent should return to idle rather than closing its session
- the session is only closed when the wrapper is explicitly shut down

This is the current low-token strategy: keep the operational presence alive, but keep the expensive reasoning layer asleep until there is work.

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
- [agents/roles/implementor/prompt.md](/home/falkzach/code/Infosphere/agents/roles/implementor/prompt.md)
- [agents/roles/user-experience-manager/prompt.md](/home/falkzach/code/Infosphere/agents/roles/user-experience-manager/prompt.md)

Recommended local worktrees:
- [/home/falkzach/code/Infosphere-coordinator](/home/falkzach/code/Infosphere-coordinator)
- [/home/falkzach/code/Infosphere-implementor-1](/home/falkzach/code/Infosphere-implementor-1)
- [/home/falkzach/code/Infosphere-implementor-2](/home/falkzach/code/Infosphere-implementor-2)
- [/home/falkzach/code/Infosphere-implementor-3](/home/falkzach/code/Infosphere-implementor-3)
- [/home/falkzach/code/Infosphere-ux](/home/falkzach/code/Infosphere-ux)

## Near-Term Work

- add context entry API and MCP tools
- harden the agent execution protocol around task completion and PR lifecycle
- improve supervisor behavior and wake heuristics
- add scripted multi-agent end-to-end scenarios
