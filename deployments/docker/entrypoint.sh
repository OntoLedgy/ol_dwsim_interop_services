#!/bin/sh
set -eu

mkdir -p /app/cases /app/logs

if [ -z "${DWSIM_CASE_STORAGE_ROOTS:-}" ]; then
  export DWSIM_CASE_STORAGE_ROOTS=/app/cases
fi

if [ -z "${DWSIM_LOG_FILE:-}" ]; then
  export DWSIM_LOG_FILE=/app/logs/dwsim-mcp.log
fi

if [ "${DWSIM_RUN_DOCTOR:-false}" = "true" ] || [ "${DWSIM_RUN_DOCTOR:-0}" = "1" ]; then
  dwsim-mcp doctor --quiet
fi

if [ "$#" -eq 0 ]; then
  set -- dwsim-mcp
fi

exec "$@"
