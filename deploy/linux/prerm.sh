#!/bin/bash
set -e

case "$1" in
  remove|upgrade|deconfigure)
    systemctl stop devicehub 2>/dev/null || true
    systemctl disable devicehub 2>/dev/null || true
    ;;
  failed-upgrade)
    exit 0
    ;;
esac
