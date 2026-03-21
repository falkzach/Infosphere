# Infosphere.Mcp

`Infosphere.Mcp` is a thin MCP server that exposes the Infosphere coordination API to agents over stdio.

It is intentionally not a second backend. Persistence, workflow rules, and concurrency enforcement stay in [Infosphere.Api](/home/falkzach/code/Infosphere/src/Infosphere.Api).

## Purpose

Use this project when an agent runtime wants MCP tools instead of calling the Infosphere HTTP API directly.

Current responsibilities:
- expose MCP `tools/list` and `tools/call`
- translate MCP tool calls into `Infosphere.Api` HTTP requests
- return structured MCP tool results

Current non-goals:
- owning database access
- duplicating business rules from the API
- acting as a long-lived HTTP service

## Transport

The server uses stdio with `Content-Length` framed JSON-RPC messages.

That means it is usually launched by an MCP-capable client process, not as a normal web server.

## Configuration

The MCP server needs the base URL for `Infosphere.Api`.

Supported configuration:
- environment variable: `INFOSPHERE_API_BASE_URL`
- command line: `--api-base-url http://localhost:5080`

Default:

```text
http://localhost:5080
```

## Current Tools

- `list_workspaces`
- `register_agent_session`
- `heartbeat_agent_session`
- `list_tasks`
- `claim_task`
- `transition_task_state`
- `list_workspace_messages`
- `post_workspace_message`

## Local Run

Build:

```bash
DOTNET_CLI_HOME=/tmp dotnet build src/Infosphere.Mcp/Infosphere.Mcp.csproj
```

Run:

```bash
INFOSPHERE_API_BASE_URL=http://localhost:5080 \
DOTNET_CLI_HOME=/tmp \
dotnet run --project src/Infosphere.Mcp/Infosphere.Mcp.csproj --no-build
```

## Example MCP Request

List tools:

```text
Content-Length: 45

{"jsonrpc":"2.0","id":1,"method":"tools/list"}
```

Call `list_workspaces`:

```text
Content-Length: 95

{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_workspaces","arguments":{}}}
```

## Docker

A runtime image is defined in [Dockerfile](/home/falkzach/code/Infosphere/src/Infosphere.Mcp/Dockerfile).

This project is not currently included in `docker-compose.yml` because stdio MCP servers are typically launched by the client runtime rather than run as a standalone background container.

## Verification

Unit tests:

```bash
DOTNET_CLI_HOME=/tmp dotnet test tests/Infosphere.Mcp.Tests/Infosphere.Mcp.Tests.csproj
```

Manual smoke test:
- start `Infosphere.Api`
- launch `Infosphere.Mcp`
- send framed `tools/list` or `tools/call` requests over stdio

## Next Work

- add task creation and available-task tools
- add context entry tools
- tighten agent/session-oriented claim semantics
- add a scripted end-to-end multi-agent scenario
