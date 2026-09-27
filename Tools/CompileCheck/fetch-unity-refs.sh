#!/usr/bin/env bash
# Lädt die Unity-Referenz-Assemblies (nur Schnittstellen, keine Engine) von NuGet,
# damit die Unity-Skripte ohne installierten Editor auf Compilerfehler geprüft werden können.
set -euo pipefail
DEST="${UNITY_REF_DIR:-/opt/unityref}"
mkdir -p "$DEST"
cd "$DEST"
if [ ! -d mods ]; then
  curl -sSL -o mods.nupkg https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  unzip -q -o mods.nupkg -d mods
fi
if [ ! -d sdk ]; then
  curl -sSL -o sdk.nupkg https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg
  unzip -q -o sdk.nupkg -d sdk
fi
echo "Unity-Referenzen bereit in $DEST"
