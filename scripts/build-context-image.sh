#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash scripts/build-context-image.sh \
    --role <role> \
    --runtime <runtime> \
    --repo-root <path> \
    --bootstrap-path <path> \
    --output-markdown <path> \
    --output-manifest <path> \
    [--workspace-id <id>]
EOF
}

role=""
runtime=""
repo_root=""
bootstrap_path=""
output_markdown=""
output_manifest=""
workspace_id=""

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
    --repo-root)
      repo_root="${2:-}"
      shift 2
      ;;
    --bootstrap-path)
      bootstrap_path="${2:-}"
      shift 2
      ;;
    --output-markdown)
      output_markdown="${2:-}"
      shift 2
      ;;
    --output-manifest)
      output_manifest="${2:-}"
      shift 2
      ;;
    --workspace-id)
      workspace_id="${2:-}"
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

if [[ -z "$role" || -z "$runtime" || -z "$repo_root" || -z "$bootstrap_path" || -z "$output_markdown" || -z "$output_manifest" ]]; then
  usage >&2
  exit 1
fi

runtime_file=""
case "$runtime" in
  codex)
    runtime_file="CODEX.md"
    ;;
  claude)
    runtime_file="CLAUDE.md"
    ;;
  gemini)
    runtime_file="GEMINI.md"
    ;;
  antigravity)
    runtime_file="ANTIGRAVITY.md"
    ;;
  *)
    echo "Unsupported runtime: $runtime" >&2
    exit 1
    ;;
esac

runtime_path="$repo_root/$runtime_file"
readme_path="$repo_root/README.md"
role_prompt="$repo_root/agents/roles/$role/prompt.md"
role_context="$repo_root/agents/roles/$role/context.md"
shared_principles="$repo_root/agents/shared/principles.md"
shared_workflow="$repo_root/agents/shared/workflow.md"
shared_terminology="$repo_root/agents/shared/terminology.md"

for required_file in \
  "$bootstrap_path" \
  "$runtime_path" \
  "$readme_path" \
  "$role_prompt" \
  "$role_context" \
  "$shared_principles" \
  "$shared_workflow" \
  "$shared_terminology"; do
  if [[ ! -f "$required_file" ]]; then
    echo "Missing required file: $required_file" >&2
    exit 1
  fi
done

mkdir -p "$(dirname "$output_markdown")"
mkdir -p "$(dirname "$output_manifest")"

python3 - "$role" "$runtime" "$repo_root" "$bootstrap_path" "$output_markdown" "$output_manifest" "$workspace_id" "$runtime_path" "$readme_path" "$role_prompt" "$role_context" "$shared_principles" "$shared_workflow" "$shared_terminology" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

(
    role,
    runtime,
    repo_root,
    bootstrap_path,
    output_markdown,
    output_manifest,
    workspace_id,
    runtime_path,
    readme_path,
    role_prompt,
    role_context,
    shared_principles,
    shared_workflow,
    shared_terminology,
) = sys.argv[1:]

repo_root_path = Path(repo_root)
output_markdown_path = Path(output_markdown)
output_manifest_path = Path(output_manifest)

source_files = {
    "bootstrap_packet": Path(bootstrap_path),
    "runtime_overlay": Path(runtime_path),
    "readme": Path(readme_path),
    "role_prompt": Path(role_prompt),
    "role_context": Path(role_context),
    "shared_principles": Path(shared_principles),
    "shared_workflow": Path(shared_workflow),
    "shared_terminology": Path(shared_terminology),
}


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8").strip()


def first_nonempty_lines(text: str, count: int) -> str:
    lines = [line for line in text.splitlines() if line.strip()]
    return "\n".join(lines[:count]).strip()


git_head = subprocess.check_output(
    ["git", "-C", str(repo_root_path), "rev-parse", "HEAD"],
    text=True,
).strip()

sources = {name: {"path": str(path), "sha256": sha256_file(path)} for name, path in source_files.items()}

manifest = {
    "role": role,
    "runtime": runtime,
    "workspaceId": workspace_id or None,
    "repoRoot": str(repo_root_path),
    "gitHead": git_head,
    "sources": sources,
}

if output_manifest_path.exists():
    existing = json.loads(output_manifest_path.read_text(encoding="utf-8"))
    if existing == manifest and output_markdown_path.exists():
        print("up-to-date")
        raise SystemExit(0)

readme_summary = first_nonempty_lines(read_text(source_files["readme"]), 40)

sections = [
    "# Context Image",
    "",
    "This is a cached startup baseline for a bootstrapped Infosphere agent.",
    "Load this once before MCP calls, then use MCP only for live deltas.",
    "",
    "## Metadata",
    "",
    f"- role: {role}",
    f"- runtime: {runtime}",
    f"- workspaceId: {workspace_id or 'unresolved'}",
    f"- repoRoot: {repo_root}",
    f"- gitHead: {git_head}",
    f"- generatedUtc: {datetime.now(timezone.utc).isoformat()}",
    "",
    "## Operating Model",
    "",
    "- Treat this file as the cached startup image for the role.",
    "- Use Infosphere MCP only for current workspace deltas after this baseline is loaded.",
    "- Prefer the role prompt and shared guidance captured below over re-scanning the repo.",
    "- Read additional files only when the current task requires it.",
    "",
    "## Repo Snapshot",
    "",
    readme_summary,
    "",
]

section_order = [
    ("Runtime Overlay", "runtime_overlay"),
    ("Shared Principles", "shared_principles"),
    ("Shared Workflow", "shared_workflow"),
    ("Shared Terminology", "shared_terminology"),
    ("Role Prompt", "role_prompt"),
    ("Role Context", "role_context"),
]

for title, key in section_order:
    sections.extend(["## " + title, "", read_text(source_files[key]), ""])

sections.extend(
    [
        "## Bootstrap Packet",
        "",
        read_text(source_files["bootstrap_packet"]),
        "",
    ]
)

output_markdown_path.write_text("\n".join(sections).rstrip() + "\n", encoding="utf-8")
output_manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print("rebuilt")
PY
