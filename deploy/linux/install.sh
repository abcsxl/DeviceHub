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

# 0. 交互式硬件选择
echo "[0/5] 硬件组件选择（直接回车=启用）..."
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

# 1. 创建目录并复制文件
echo "[1/5] 复制文件到 $APP_DIR ..."
mkdir -p "$APP_DIR"
cp -r "$SCRIPT_DIR"/* "$APP_DIR/"
rm -f "$APP_DIR/install.sh"
chmod +x "$APP_DIR/DeviceHub.Service.Api"

# 2. 写入硬件配置
echo "[2/5] 写入硬件配置到 appsettings.json ..."
CONFIG_FILE="$APP_DIR/appsettings.json"
if [ -f "$CONFIG_FILE" ]; then
  # 替换 Hardware.PcscReader.Enabled 的值
  sed -i "s/\"Enabled\": true/\"Enabled\": $PCSC_ENABLED/" "$CONFIG_FILE"
fi

# 3. 安装 systemd 服务
echo "[3/5] 注册 systemd 服务 ..."
cp "$SCRIPT_DIR/$SERVICE_FILE" /etc/systemd/system/
systemctl daemon-reload

# 4. 启用服务
echo "[4/5] 启用服务（开机自启）..."
systemctl enable "$APP_NAME"

# 5. 启动服务
echo "[5/5] 启动服务 ..."
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
echo "状态查看:  systemctl status $APP_NAME"
echo "日志查看:   journalctl -u $APP_NAME -f"
echo "配置编辑:  sudo nano $APP_DIR/appsettings.json"
echo "重启服务:  systemctl restart $APP_NAME"
echo "停止服务:  systemctl stop $APP_NAME"
echo "卸载:      systemctl disable $APP_NAME && rm -rf $APP_DIR /etc/systemd/system/$SERVICE_FILE && systemctl daemon-reload"
