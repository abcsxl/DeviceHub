#!/bin/bash
set -e

APP_NAME="DeviceHub"
APP_DIR="/usr/local/bin/devicehub"
SERVICE_FILE="devicehub.service"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=== DeviceHub Linux 安装脚本 ==="
echo ""

# 检查 root
if [ "$EUID" -ne 0 ]; then
  echo "请以 root 身份运行: sudo ./install.sh"
  exit 1
fi

# 1. 创建目录并复制文件
echo "[1/4] 复制文件到 $APP_DIR ..."
mkdir -p "$APP_DIR"
cp -r "$SCRIPT_DIR"/* "$APP_DIR/"
rm -f "$APP_DIR/install.sh"
chmod +x "$APP_DIR/DeviceHub.Service.Api"

# 2. 安装 systemd 服务
echo "[2/4] 注册 systemd 服务 ..."
cp "$SCRIPT_DIR/$SERVICE_FILE" /etc/systemd/system/
systemctl daemon-reload

# 3. 启用服务
echo "[3/4] 启用服务（开机自启）..."
systemctl enable "$APP_NAME"

# 4. 启动服务
echo "[4/4] 启动服务 ..."
systemctl start "$APP_NAME"

echo ""
echo "=== 安装完成 ==="
echo ""
echo "状态查看:  systemctl status $APP_NAME"
echo "日志查看:   journalctl -u $APP_NAME -f"
echo "停止服务:   systemctl stop $APP_NAME"
echo "卸载服务:   systemctl disable $APP_NAME && rm /etc/systemd/system/$SERVICE_FILE"
