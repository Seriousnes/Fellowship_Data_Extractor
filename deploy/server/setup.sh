#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"

sudo install -m 0755 -d /etc/apt/keyrings
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

arch="$(dpkg --print-architecture)"
codename="$(. /etc/os-release && echo "$VERSION_CODENAME")"
echo "deb [arch=${arch} signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${codename} stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

sudo groupadd -f docker
sudo usermod -aG docker ubuntu

sudo install -m 0755 -d /etc/docker
sudo tee /etc/docker/daemon.json > /dev/null <<'JSON'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
JSON
sudo systemctl enable docker
sudo systemctl restart docker

sudo install -d -o ubuntu -g ubuntu /opt/codex

sudo install -d /etc/caddy/sites
sudo install -m 0644 "${script_dir}/codex.caddy" /etc/caddy/sites/codex.caddy

if ! sudo caddy validate --adapter caddyfile --config /etc/caddy/Caddyfile; then
  sudo rm -f /etc/caddy/sites/codex.caddy
  exit 1
fi

sudo systemctl reload caddy
