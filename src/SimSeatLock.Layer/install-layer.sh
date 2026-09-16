#!/usr/bin/env bash
# Git Bash entry. Do not call the .cmd with a src/... path — cmd.exe
# treats 'src' as the program name.
set -e
cd "$(dirname "$0")"
cmd.exe //c install-layer.cmd
