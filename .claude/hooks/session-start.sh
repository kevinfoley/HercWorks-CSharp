#!/bin/bash
# Installs the .NET 8 SDK and restores NuGet packages so Claude Code on the web can build and test.
# The SDK comes from Ubuntu's archive: the network policy blocks Microsoft's own download host.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export DEBIAN_FRONTEND=noninteractive
if ! command -v dotnet >/dev/null 2>&1; then
  # Third-party PPAs in the base image are unreachable; their fetch warnings are harmless.
  apt-get update -qq || true
  apt-get install -y -qq dotnet-sdk-8.0
fi

{
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
  echo 'export DOTNET_NOLOGO=1'
} >> "${CLAUDE_ENV_FILE:-/dev/null}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# HercWorksMDK.sln includes HercWorks.UI (net8.0-windows), which does not restore on Linux.
dotnet restore "$CLAUDE_PROJECT_DIR/Herculan/HerculanEngine.sln"
