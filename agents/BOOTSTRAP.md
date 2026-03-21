# Agent Bootstrap

The easiest way to bootstrap agents onto Infosphere is:

1. start the stack
2. create a dedicated git worktree for each agent
3. generate a role-specific startup packet
4. launch the agent runtime from that worktree with the packet and the local MCP command

This keeps the role definition file-based and predictable while using Infosphere for live shared state.

## Prerequisites

Start the local stack:

```bash
docker compose up -d --build
```

The normal local endpoints are:
- API: `http://localhost:5080`
- Web: `http://localhost:5081`

## Per-Agent Worktrees

Each active agent should run in its own git worktree.

Recommended pattern:

```bash
git worktree add ../Infosphere-coordinator -b agent/coordinator main
git worktree add ../Infosphere-backend -b agent/backend main
git worktree add ../Infosphere-frontend -b agent/frontend main
git worktree add ../Infosphere-ux -b agent/ux main
```

Why:
- avoids overlapping edits in one working directory
- makes branch ownership clearer
- reduces accidental context leakage between agents
- makes it easier to inspect or discard one agent's work independently

## Startup Packet Script

Use [scripts/bootstrap-agent.sh](/home/falkzach/code/Infosphere/scripts/bootstrap-agent.sh) to generate a startup packet for a role and runtime.

Example:

```bash
./scripts/bootstrap-agent.sh --role coordinator --runtime codex
```

Write the packet to a file:

```bash
./scripts/bootstrap-agent.sh \
  --role csharp-backend-implementor \
  --runtime claude \
  --write /tmp/backend-agent-bootstrap.md
```

The packet includes:
- repo-wide base guidance
- runtime-specific overlay
- shared agent principles, workflow, and terminology
- role prompt
- role context
- MCP connection details

Use [scripts/launch-agents.sh](/home/falkzach/code/Infosphere/scripts/launch-agents.sh) to create all four bootstrap packets and start named `tmux` sessions in the prepared worktrees.

Example:

```bash
./scripts/launch-agents.sh --runtime codex
```

## Supported Runtimes

- `codex`
- `claude`
- `gemini`
- `antigravity`

`antigravity` is intended for Claude or Gemini launched through Antigravity.

## Supported Roles

- `coordinator`
- `csharp-backend-implementor`
- `vite-react-frontend-implementor`
- `user-experience-manager`

## Recommended Launch Pattern

### Codex

- generate the startup packet
- provide it as the initial repo/task context from that agent's worktree
- configure the MCP command to run `Infosphere.Mcp`

### Claude / Gemini / Antigravity

- generate the startup packet
- use it as the system or startup context for the role from that agent's worktree
- wire the runtime to the local MCP command

## MCP Command

Use this as the local MCP launch command:

```bash
INFOSPHERE_API_BASE_URL=http://localhost:5080 \
DOTNET_CLI_HOME=/tmp \
dotnet run --project src/Infosphere.Mcp/Infosphere.Mcp.csproj --no-build
```

If the project has not been built yet:

```bash
DOTNET_CLI_HOME=/tmp dotnet build src/Infosphere.Mcp/Infosphere.Mcp.csproj
```

## Operational Guidance

- the Coordinator should usually be the first agent started
- start specialized implementors after the Coordinator is active
- use the User Experience Manager when behavior or dashboard direction is unclear
- live coordination should happen through Infosphere tasks, messages, sessions, and context entries rather than editing the role files

## Suggested First Sequence

1. Start Coordinator
2. Coordinator reviews workspace messages and available tasks
3. Start C# Backend Implementor and/or Vite React Frontend Implementor in their own worktrees as needed
4. Start User Experience Manager in its own worktree when product behavior or usability direction needs decisions

## Future Improvement

The next useful step would be a runtime-specific launcher layer that:
- creates an agent session automatically
- writes a bootstrap workspace message
- records the selected role in session metadata
- launches the runtime with the generated packet

This document and script are the lowest-friction first step.
