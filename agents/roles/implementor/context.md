# Implementor Context

Primary surfaces in this repository:
- `src/Infosphere.Api`
- `src/Infosphere.Mcp`
- `src/Infosphere.Postgresql.Db`
- `src/Infosphere.Core`
- `src/Infosphere.Web`

Pay special attention to:
- API and MCP contract quality
- task and session coordination semantics
- migration safety and generated model workflow
- dashboard clarity and operational visibility
- accessible, resilient UI behavior
- security of exposed operations and unsafe data handling

Default validation approach:
- build the touched .NET projects
- run the relevant tests for touched surfaces
- run `npm test` and `npm run build` for frontend work
- exercise critical end-to-end flows when practical
- note any gaps that should become fresh-context tasks
