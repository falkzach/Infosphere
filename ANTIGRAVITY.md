# Antigravity Instructions

Read and follow [AGENT.md](/home/falkzach/code/Infosphere/AGENT.md) first.

This file exists for agent runtimes that proxy Claude or Gemini through Antigravity.

## Expectations

- treat this repository as multi-agent infrastructure, not a single-agent app
- prefer interacting through `Infosphere.Mcp` when testing agent workflows
- if a feature is intended for agents, verify whether it belongs in both the API and MCP
- preserve compatibility with Claude-oriented and Gemini-oriented tool use by keeping contracts explicit and transport-agnostic

## Practical Notes

- the browser UI is observational, not the primary coordination interface
- the MCP server is stdio-based and normally launched by a client runtime rather than Compose
- stale sessions should be closed rather than left active indefinitely
