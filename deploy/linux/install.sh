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

# 0. 交互式硬件选择和端口配置
echo "[0/6] 硬件组件选择（直接回车=启用）..."
echo ""

read -r -p "启用 PCSC 读卡器支持？ [Y/n] " REPLY_PCSC
REPLY_PCSC=${REPLY_PCSC:-Y}
if [[ "$REPLY_PCSC" =~ ^[Yy] ]]; then
  PCSC_ENABLED="true"
  echo "  → PCSC 读卡器: 启用"
else
  PCSC_ENABLED="false"
  echo "  → PCSC 读卡器: 禁用"
fi

echo ""

# 端口检查
DEFAULT_PORT=5000
HTTP_PORT=$DEFAULT_PORT

check_port() {
  local port=$1
  if command -v ss &> /dev/null; then
    ss -tlnp | grep -q ":${port} "
  elif command -v netstat &> /dev/null; then
    netstat -tlnp | grep -q ":${port} "
  else
    return 1
  fi
}

if check_port $DEFAULT_PORT; then
  echo "默认端口 $DEFAULT_PORT 已被占用"
  read -r -p "请输入新的 HTTP 端口 [$((DEFAULT_PORT + 1))]: " PORT_INPUT
  if [ -n "$PORT_INPUT" ]; then
    HTTP_PORT=$PORT_INPUT
  else
    HTTP_PORT=$((DEFAULT_PORT + 1))
  fi
  echo "  → 使用端口: $HTTP_PORT"
else
  echo "  → HTTP 端口: $DEFAULT_PORT (可用)"
fi

echo ""

# 1. 创建目录并复制文件
echo "[1/6] 复制文件到 $APP_DIR ..."
mkdir -p "$APP_DIR"
cp -r "$SCRIPT_DIR"/* "$APP_DIR/"
rm -f "$APP_DIR/install.sh"
chmod +x "$APP_DIR/DeviceHub.Service.Api"

# 2. 写入硬件配置
echo "[2/6] 写入硬件配置到 appsettings.json ..."
CONFIG_FILE="$APP_DIR/appsettings.json"
if [ -f "$CONFIG_FILE" ]; then
  sed -i "s/\"Enabled\": true/\"Enabled\": $PCSC_ENABLED/" "$CONFIG_FILE"
fi

# 3. 写入端口配置
echo "[3/6] 配置 HTTP 端口: $HTTP_PORT ..."
if [ -f "$CONFIG_FILE" ] && [ "$HTTP_PORT" != "$DEFAULT_PORT" ]; then
  sed -i "s/\"HttpPort\": $DEFAULT_PORT/\"HttpPort\": $HTTP_PORT/" "$CONFIG_FILE"
fi

# 4. 安装 systemd 服务
echo "[4/6] 注册 systemd 服务 ..."
cp "$SCRIPT_DIR/$SERVICE_FILE" /etc/systemd/system/
systemctl daemon-reload

# 5. 启用服务
echo "[5/6] 启用服务（开机自启）..."
systemctl enable "$APP_NAME"

# 6. 启动服务
echo "[6/6] 启动服务 ..."
systemctl start "$APP_NAME"

sleep 2
if systemctl is-active --quiet "$APP_NAME"; then
  echo "  → 服务已成功启动"
else
  echo "  ! 服务启动失败，请重启系统后重试"
fi

echo ""
echo "=== 安装完成 ==="
echo ""
echo "服务地址:  http://localhost:$HTTP_PORT"
echo "状态查看:  systemctl status $APP_NAME"
echo "日志查看:   journalctl -u $APP_NAME -f"
echo "配置编辑:  sudo nano $APP_DIR/appsettings.json"
echo "重启服务:  systemctl restart $APP_NAME"
echo "停止服务:  systemctl stop $APP_NAME"
echo "卸载:      systemctl disable $APP_NAME && rm -rf $APP_DIR /etc/systemd/system/$SERVICE_FILE && systemctl daemon-reload"
