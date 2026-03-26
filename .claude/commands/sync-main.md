Pull main and reset all agent worktrees to the new HEAD.

Run these steps in order:

1. Pull main in the primary repo:
```bash
git checkout main && git pull
```

2. Reset each agent worktree's branch to the new main HEAD. For each worktree in the list below, check out its `agent/*` branch and hard-reset it to `origin/main`. If a worktree is mid-rebase or has a detached HEAD, abort the rebase first before resetting.

Worktrees and their branches:
- /home/falkzach/code/Infosphere-coordinator → agent/coordinator
- /home/falkzach/code/Infosphere-implementor-1 → agent/implementor-1
- /home/falkzach/code/Infosphere-implementor-2 → agent/implementor-2
- /home/falkzach/code/Infosphere-implementor-3 → agent/implementor-3
- /home/falkzach/code/Infosphere-ux → agent/ux

For each worktree:
```bash
git -C <path> rebase --abort 2>/dev/null || true
git -C <path> checkout <branch>
git -C <path> reset --hard origin/main
```

3. Report the final HEAD SHA and confirm all worktrees match main.
