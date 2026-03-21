# C# Backend Implementor Context

Primary surfaces in this repository:
- `src/Infosphere.Api`
- `src/Infosphere.Mcp`
- `src/Infosphere.Postgresql.Db`
- `src/Infosphere.Core`

Pay special attention to:
- API contract quality
- task/session coordination semantics
- migration safety
- generated model workflow
- Postgres query behavior
- health/readiness behavior
- security of exposed operations

Default validation approach:
- build the touched projects
- run relevant tests
- exercise critical flows when possible
- note any gaps that should become fresh-context tasks
