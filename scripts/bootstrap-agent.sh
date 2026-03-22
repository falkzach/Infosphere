#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./scripts/bootstrap-agent.sh --role <role> --runtime <runtime> [--write <path>]

Supported roles:
  coordinator
  implementor
  user-experience-manager

Supported runtimes:
  codex
  claude
EOF
}

role=""
runtime=""
write_path=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --role)
      role="${2:-}"
      shift 2
      ;;
    --runtime)
      runtime="${2:-}"
      shift 2
      ;;
    --write)
      write_path="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$role" || -z "$runtime" ]]; then
  usage >&2
  exit 1
fi

case "$role" in
  coordinator|implementor|user-experience-manager)
    ;;
  *)
    echo "Unsupported role: $role" >&2
    exit 1
    ;;
esac

case "$runtime" in
  codex)
    runtime_file="CODEX.md"
    ;;
  claude)
    runtime_file="CLAUDE.md"
    ;;
  *)
    echo "Unsupported runtime: $runtime" >&2
    exit 1
    ;;
esac

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
role_prompt="$repo_root/agents/roles/$role/prompt.md"
role_context="$repo_root/agents/roles/$role/context.md"
runtime_path="$repo_root/$runtime_file"

for required_file in \
  "$runtime_path" \
  "$repo_root/agents/shared/principles.md" \
  "$repo_root/agents/shared/workflow.md" \
  "$repo_root/agents/shared/terminology.md" \
  "$role_prompt" \
  "$role_context"; do
  if [[ ! -f "$required_file" ]]; then
    echo "Missing required file: $required_file" >&2
    exit 1
  fi
done

packet="$(mktemp)"
trap 'rm -f "$packet"' EXIT

cat > "$packet" <<EOF
# Agent Bootstrap Packet

Role: $role
Runtime: $runtime
Repository: $repo_root
Role prompt: $role_prompt
Role context: $role_context
Runtime overlay: $runtime_path
Shared principles: $repo_root/agents/shared/principles.md
Shared workflow: $repo_root/agents/shared/workflow.md
Shared terminology: $repo_root/agents/shared/terminology.md

## MCP Command

\`\`\`bash
INFOSPHERE_API_BASE_URL=http://localhost:5080 \\
DOTNET_CLI_HOME=/tmp \\
dotnet run --project src/Infosphere.Mcp/Infosphere.Mcp.csproj
\`\`\`

## Runtime Overlay

EOF

cat "$runtime_path" >> "$packet"

cat >> "$packet" <<EOF

## Shared Principles

EOF

cat "$repo_root/agents/shared/principles.md" >> "$packet"

cat >> "$packet" <<EOF

## Shared Workflow

EOF

cat "$repo_root/agents/shared/workflow.md" >> "$packet"

cat >> "$packet" <<EOF

## Shared Terminology

EOF

cat "$repo_root/agents/shared/terminology.md" >> "$packet"

cat >> "$packet" <<EOF

## Role Prompt

EOF

cat "$role_prompt" >> "$packet"

cat >> "$packet" <<EOF

## Role Context

EOF

cat "$role_context" >> "$packet"

cat >> "$packet" <<EOF

## Launch Checklist

1. Start the local stack with \`docker compose up -d --build\`.
2. Read the role prompt and role context files listed above.
3. Launch the runtime with a compact startup prompt that references those files.
4. Point the runtime at the MCP command above.
5. Use the existing registered session rather than creating a second one.

EOF

if [[ -n "$write_path" ]]; then
  cp "$packet" "$write_path"
  echo "Wrote bootstrap packet to $write_path"
else
  cat "$packet"
fi
