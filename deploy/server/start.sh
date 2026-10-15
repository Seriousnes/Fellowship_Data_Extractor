#!/usr/bin/env bash
set -euo pipefail

tag="$1"
dir=/opt/codex

printf 'CODEX_TAG=%s\n' "$tag" > "${dir}/.env"
cd "$dir"
docker compose up -d --remove-orphans
