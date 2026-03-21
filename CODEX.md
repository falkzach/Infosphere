# Codex Instructions

Read and follow [AGENT.md](/home/falkzach/code/Infosphere/AGENT.md) first.

Additional Codex-specific expectations:

- prefer making code changes directly instead of stopping at analysis
- keep changes pragmatic and minimal
- verify with builds, tests, or live checks when feasible
- when changing the API contract, update MCP wiring in the same pass if the agent workflow depends on it
- prefer `rg` for search and `apply_patch` for edits
