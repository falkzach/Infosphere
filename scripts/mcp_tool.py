#!/usr/bin/env python3
import argparse
import json
import os
import subprocess
import sys


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tool", required=True)
    parser.add_argument("--arguments", default="{}")
    parser.add_argument("--api-base-url", default=os.environ.get("INFOSPHERE_API_BASE_URL", "http://localhost:5080"))
    parser.add_argument("--project", default="src/Infosphere.Mcp/Infosphere.Mcp.csproj")
    args = parser.parse_args()

    try:
        arguments = json.loads(args.arguments)
    except json.JSONDecodeError as exc:
        print(f"Invalid JSON for --arguments: {exc}", file=sys.stderr)
        return 2

    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": {
            "name": args.tool,
            "arguments": arguments,
        },
    }

    payload = json.dumps(request, separators=(",", ":"))
    framed = f"Content-Length: {len(payload.encode('utf-8'))}\r\n\r\n{payload}".encode("utf-8")

    env = os.environ.copy()
    env["INFOSPHERE_API_BASE_URL"] = args.api_base_url
    env.setdefault("DOTNET_CLI_HOME", "/tmp")

    result = subprocess.run(
        ["dotnet", "run", "--project", args.project],
        input=framed,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env,
        check=False,
    )

    if result.returncode != 0:
        sys.stderr.write(result.stderr.decode("utf-8", errors="replace"))
        return result.returncode

    output = result.stdout.decode("utf-8", errors="replace")
    separator = "\r\n\r\n" if "\r\n\r\n" in output else "\n\n"
    if separator not in output:
        print(output, end="")
        return 0

    body = output.split(separator, 1)[1]
    response = json.loads(body)
    print(json.dumps(response, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
