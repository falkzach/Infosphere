#!/usr/bin/env bash
set -euo pipefail

# Runs the database migration validation and model generation workflow.
# Any extra arguments are forwarded to the dotnet project.

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DOTNET_CLI_HOME=/tmp \
DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  dotnet run --project "$repo_root/src/Infosphere.Postgresql.Db" -- sync-models "$@"
