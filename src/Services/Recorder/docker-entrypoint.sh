#!/bin/sh
set -e

# PulseAudio needs a runtime directory and runs for the whole life of the container
export XDG_RUNTIME_DIR="/tmp/runtime-$(id -u)"
mkdir -p "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"

pulseaudio --start --exit-idle-time=-1 --log-target=stderr || {
  echo "PulseAudio could not be started" >&2
  exit 1
}

exec node dist/index.js
