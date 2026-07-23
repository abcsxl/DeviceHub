#!/bin/bash
set -e

case "$1" in
  configure)
    systemctl daemon-reload
    systemctl enable devicehub 2>/dev/null || true
    systemctl start devicehub 2>/dev/null || true
    ;;
  abort-upgrade|abort-remove|abort-deconfigure)
    exit 0
    ;;
esac
