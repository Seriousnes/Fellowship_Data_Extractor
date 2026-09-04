#!/usr/bin/env bash
set -euo pipefail

image="$1"
deployed="$2"

tags=$(docker images "$image" --format '{{.CreatedAt}}\t{{.Tag}}' | sort -r | cut -f2 | grep -vx '<none>' || true)
keep=$(printf '%s\n' "$tags" | awk 'NF && !seen[$0]++' | head -3)

for tag in $tags; do
  if [ "$tag" = "$deployed" ]; then continue; fi
  if printf '%s\n' "$keep" | grep -qx "$tag"; then continue; fi
  echo "Removing ${image}:${tag}."
  docker rmi "${image}:${tag}" || true
done
