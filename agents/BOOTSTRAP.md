# Agent Bootstrap

The easiest way to bootstrap agents onto Infosphere is:

1. start the stack
2. create a dedicated git worktree for each agent
3. generate a role-specific startup packet
4. launch the agent runtime from that worktree with the packet and the local MCP command

This keeps the role definition file-based and predictable while using Infosphere for live shared state.

The intended local mode is unattended execution inside a dedicated worktree. Agents should not be blocked on approvals for ordinary development actions like editing files, building, testing, or restarting local containers.

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
git worktree add ../Infosphere-implementor-1 -b agent/implementor-1 main
git worktree add ../Infosphere-implementor-2 -b agent/implementor-2 main
git worktree add ../Infosphere-implementor-3 -b agent/implementor-3 main
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
  --role implementor \
  --runtime claude \
  --write /tmp/implementor-agent-bootstrap.md
```

The packet includes:
- repo-wide base guidance
- runtime-specific overlay
- shared agent principles, workflow, and terminology
- role prompt
- role context
- MCP connection details

## Context Images

Bootstrapped agents now use a cached `context image` as their startup baseline.

The context image is a generated file that bundles:
- runtime overlay
- shared agent guidance
- role prompt and role context
- a compact repo snapshot
- bootstrap metadata

The supervisor refreshes this cache before agent wake-ups and then points the runtime at the
cached image first. The intent is:
- less repeated prompt assembly
- less repeated disk scanning
- smaller wake-time token usage
- MCP used mainly for live deltas rather than startup reconstruction

The current builder is [scripts/build-context-image.sh](/home/falkzach/code/Infosphere/scripts/build-context-image.sh).

Use [scripts/launch-agents.sh](/home/falkzach/code/Infosphere/scripts/launch-agents.sh) to create all four bootstrap packets and start named `tmux` sessions in the prepared worktrees.

Example:

```bash
./scripts/launch-agents.sh --runtime codex
```

If you need to reset the agent runtime state before relaunching, use
[scripts/close-agent-sessions.sh](/home/falkzach/code/Infosphere/scripts/close-agent-sessions.sh):

```bash
bash scripts/close-agent-sessions.sh --tmux
```

That will:
- close any non-closed agent sessions in Postgres
- kill the standard Infosphere `tmux` sessions when `--tmux` is passed

Use `--dry-run` if you want to inspect what would be closed first.

## Supported Runtimes

- `codex`
- `claude`

## Supported Roles

- `coordinator`
- `implementor`
- `user-experience-manager`

## Recommended Launch Pattern

### Codex

- generate the startup packet
- let the supervisor generate and refresh the cached context image
- provide it as the initial repo/task context from that agent's worktree
- configure the MCP command to run `Infosphere.Mcp`
- run with approvals and sandbox bypassed for normal local development work in that dedicated worktree

### Claude

- generate the startup packet
- let the supervisor generate and refresh the cached context image
- configure the MCP command to run `Infosphere.Mcp`
- run with `--dangerously-skip-permissions` for normal local development work in the dedicated worktree

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
3. Start one or more Implementors in their own worktrees as needed
4. Start User Experience Manager in its own worktree when product behavior or usability direction needs decisions

## Future Improvement

The next useful step would be a runtime-specific launcher layer that:
- creates an agent session automatically
- writes a bootstrap workspace message
- records the selected role in session metadata
- launches the runtime with the generated packet and cached context image

This document and script are the lowest-friction first step.
