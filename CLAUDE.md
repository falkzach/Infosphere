# Claude Instructions

Read and follow [AGENT.md](/home/falkzach/code/Infosphere/AGENT.md) first.

Additional Claude-specific expectations:

- avoid broad speculative refactors when the requested change is localized
- keep reasoning visible in code structure and comments, not long prose
- preserve architectural boundaries:
  - API owns business rules
  - MCP owns agent protocol adaptation
  - database project owns schema validation and generated models
- when adding new coordination behavior, prefer explicit request/response contracts over hidden conventions
