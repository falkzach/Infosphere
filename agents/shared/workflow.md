# Shared Workflow

1. Read the role prompt and role context.
2. Start from a dedicated git worktree for that agent.
3. Inspect current workspace messages, assigned tasks, and relevant context before acting.
4. Do the requested work within role boundaries.
5. Track execution explicitly inside Infosphere while the work is happening.
6. Validate results with the right checks for the domain.
7. Publish outcomes, blockers, artifacts, and follow-up needs back into Infosphere before sleeping.

## Task Execution Protocol

When working a task, do not rely on a private implicit plan.

Use the task execution tools and records:
- read task execution state, including checklist items, updates, and artifacts
- add checklist items when the task needs clearer success criteria
- complete required checklist items as work is verified
- post structured task updates for progress, validation, blockers, and review outcomes
- attach artifacts like branch names, commit SHAs, PR URLs, and test results

Before returning to idle:
- ensure required checklist items are complete or explicitly blocked
- attach the important branch/commit/PR/test artifacts
- transition the task to the correct final state
- post a concise workspace message only when other agents or humans need to know something beyond the task record

Implementation agents should treat testing, validation, and security as part of delivery, but perform deeper work in those areas as separate, fresh-context tasks when that produces better results.
