#!/usr/bin/env bash
# Checks the game without Unity:
#   1. compiles every game and editor script against the stand-ins (Check)
#   2. runs the simulation suites (Sim)
# Usage: tools/headless/run.sh [part of a suite name] [-v]
# Needs the .NET 8 SDK (dotnet). Exit code 0 = all good.
set -e
here="$(cd "$(dirname "$0")" && pwd)"
echo "== compile check (game + editor scripts)"
dotnet build "$here/Check/Check.csproj" -nologo -v q 2>&1 | grep -E " error |Build succeeded|error\(s\)" | sort -u || true
if ! dotnet build "$here/Check/Check.csproj" -nologo -v q > /dev/null 2>&1; then echo "Compile check FAILED"; exit 1; fi
echo "== simulation"
dotnet build "$here/Sim/Sim.csproj" -nologo -v q > /dev/null 2>&1 || { dotnet build "$here/Sim/Sim.csproj" -nologo -v q 2>&1 | grep -E " error " | sort -u; exit 1; }
dotnet run --project "$here/Sim/Sim.csproj" --no-build -- "$@"
