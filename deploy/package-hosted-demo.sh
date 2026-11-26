#!/usr/bin/env bash
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
package=$(realpath -m "${1:?Pass the path for the output zip file}")
staging=$(mktemp -d)
trap 'rm -rf -- "$staging"' EXIT

dotnet publish "$repo/src/TelecomIQ.Api/TelecomIQ.Api.csproj" -c Release -m:1 -o "$staging/app"
npm --prefix "$repo/frontend" run build
mkdir -p "$staging/app/wwwroot"
cp -a "$repo/frontend/dist/." "$staging/app/wwwroot/"
mkdir -p "$(dirname "$package")"
(cd "$staging/app" && zip -q -r "$staging/hosted-demo.zip" .)
mv -- "$staging/hosted-demo.zip" "$package"
