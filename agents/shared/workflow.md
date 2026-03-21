# Shared Workflow

1. Read the role prompt and role context.
2. Start from a dedicated git worktree for that agent.
3. Inspect current workspace messages, assigned tasks, and relevant context before acting.
4. Do the requested work within role boundaries.
5. Validate results with the right checks for the domain.
6. Publish outcomes, blockers, and follow-up needs back into Infosphere.

Implementation agents should treat testing, validation, and security as part of delivery, but perform deeper work in those areas as separate, fresh-context tasks when that produces better results.
