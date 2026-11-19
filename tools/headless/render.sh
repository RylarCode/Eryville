#!/usr/bin/env bash
# Draws the game's UI as pictures without Unity (see render/README.md).
# Usage: tools/headless/render.sh [output folder]     (default: tools/headless/render/out)
set -e
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-$here/render/out}"
mkdir -p "$out"
python3 "$here/render/scene_ui.py" "$here/../../Assets/_Project/Scenes/GameScene.unity" "$out/scene_ui.json"
dotnet build "$here/Sim/Sim.csproj" -nologo -v q > /dev/null 2>&1 || { dotnet build "$here/Sim/Sim.csproj" -nologo -v q 2>&1 | grep -E " error " | sort -u; exit 1; }
dotnet run --project "$here/Sim/Sim.csproj" --no-build -- --render "$out" "$out/scene_ui.json"
python3 "$here/render/render_ui.py" "$out"
