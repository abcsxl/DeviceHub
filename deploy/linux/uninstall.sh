#!/bin/bash
set -e

APP_NAME="devicehub"
APP_DIR="/usr/local/bin/devicehub"
SERVICE_FILE="devicehub.service"

echo "=== DeviceHub Linux Uninstaller ==="
echo ""
echo "[1] English"
echo "[2] 中文简体"
read -r -p "Select language [1]: " LANG_CHOICE
LANG_CHOICE=${LANG_CHOICE:-1}

if [ "$LANG_CHOICE" = "2" ]; then
  MSG_TITLE="=== DeviceHub 卸载程序 ==="
  MSG_ROOT="请以 root 身份运行: sudo ./uninstall.sh"
  MSG_STOPPING="[1/4] 停止服务 ..."
  MSG_REMOVING_SERVICE="[2/4] 移除 systemd 服务 ..."
  MSG_REMOVING_FILES="[3/4] 删除安装目录 ..."
  MSG_RELOAD="[4/4] 重载 systemd ..."
  MSG_DONE="=== 卸载完成 ==="
  MSG_NOTE="配置文件和日志已保留在 $APP_DIR（如需彻底删除请手动执行 rm -rf $APP_DIR）"
else
  MSG_TITLE="=== DeviceHub Uninstaller ==="
  MSG_ROOT="Please run as root: sudo ./uninstall.sh"
  MSG_STOPPING="[1/4] Stopping service ..."
  MSG_REMOVING_SERVICE="[2/4] Removing systemd service ..."
  MSG_REMOVING_FILES="[3/4] Removing installation files ..."
  MSG_RELOAD="[4/4] Reloading systemd ..."
  MSG_DONE="=== Uninstall Complete ==="
  MSG_NOTE="Config and logs remain at $APP_DIR (run rm -rf $APP_DIR to fully remove)"
fi

echo ""

if [ "$EUID" -ne 0 ]; then
  echo "$MSG_ROOT"
  exit 1
fi

echo "$MSG_STOPPING"
systemctl stop "$APP_NAME" 2>/dev/null || true
systemctl disable "$APP_NAME" 2>/dev/null || true

echo "$MSG_REMOVING_SERVICE"
rm -f "/etc/systemd/system/$SERVICE_FILE"

echo "$MSG_REMOVING_FILES"
rm -rf "$APP_DIR"

echo "$MSG_RELOAD"
systemctl daemon-reload

echo ""
echo "$MSG_DONE"
echo "$MSG_NOTE"
