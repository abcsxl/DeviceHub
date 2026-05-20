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
echo "[0/7] 硬件组件选择（直接回车=启用）..."
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
IS_UPGRADE=false

# 检查是否为覆盖安装（读取现有配置中的端口）
if [ -f "$APP_DIR/appsettings.json" ]; then
  EXISTING_PORT=$(grep -o '"HttpPort": *[0-9]*' "$APP_DIR/appsettings.json" | grep -o '[0-9]*')
  if [ -n "$EXISTING_PORT" ]; then
    HTTP_PORT=$EXISTING_PORT
    IS_UPGRADE=true
    echo "检测到已安装版本，保留现有端口: $HTTP_PORT"
  fi
fi

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

# 覆盖安装且使用原端口时跳过检查（步骤 1 会先停止旧服务）
if [ "$IS_UPGRADE" = true ]; then
  echo "  → HTTP 端口: $HTTP_PORT (安装时将释放)"
else
  if check_port $HTTP_PORT; then
    echo "端口 $HTTP_PORT 已被占用"
    read -r -p "请输入新的 HTTP 端口 [$((HTTP_PORT + 1))]: " PORT_INPUT
    if [ -n "$PORT_INPUT" ]; then
      HTTP_PORT=$PORT_INPUT
    else
      HTTP_PORT=$((HTTP_PORT + 1))
    fi
    echo "  → 使用端口: $HTTP_PORT"
  else
    echo "  → HTTP 端口: $HTTP_PORT (可用)"
  fi
fi

echo ""

# 1. 停止并移除旧服务 (增强版：验证停止 + 强杀选项)
echo "[1/7] 检查并停止旧服务 ..."
MUST_RESTART=false
FORCE_KILLED=false

if systemctl is-active --quiet "$APP_NAME"; then
  systemctl stop "$APP_NAME"
  sleep 2
  
  # 验证是否真的停止
  if systemctl is-active --quiet "$APP_NAME"; then
    echo "! 服务未能自动停止。"
    read -r -p "是否强制结束进程以完成安装？ [Y/n] " REPLY_FORCE
    REPLY_FORCE=${REPLY_FORCE:-Y}
    if [[ "$REPLY_FORCE" =~ ^[Yy] ]]; then
      echo "  → 正在强制结束进程..."
      pkill -9 -f "DeviceHub.Service.Api" 2>/dev/null || true
      sleep 1
      FORCE_KILLED=true
    else
      echo "  → 已跳过强制停止。安装完成后必须重启系统以应用更新。"
      MUST_RESTART=true
    fi
  fi
fi

if [ "$MUST_RESTART" = false ]; then
  systemctl disable "$APP_NAME" 2>/dev/null || true
  rm -f /etc/systemd/system/$SERVICE_FILE
  systemctl daemon-reload
fi

# 2. 创建目录并复制文件
echo "[2/7] 复制文件到 $APP_DIR ..."
mkdir -p "$APP_DIR"
cp -r "$SCRIPT_DIR"/* "$APP_DIR/"
rm -f "$APP_DIR/install.sh"
chmod +x "$APP_DIR/DeviceHub.Service.Api"

# 3. 写入硬件配置
echo "[3/7] 写入硬件配置到 appsettings.json ..."
CONFIG_FILE="$APP_DIR/appsettings.json"
if [ -f "$CONFIG_FILE" ]; then
  awk -v enabled="$PCSC_ENABLED" '
    /"Pcsc"/ { in_pcsc=1 }
    in_pcsc && /"Enabled"/ {
      sub(/"Enabled": [a-z]+/, "\"Enabled\": " enabled)
      in_pcsc=0
    }
    { print }
  ' "$CONFIG_FILE" > "$CONFIG_FILE.tmp" && mv "$CONFIG_FILE.tmp" "$CONFIG_FILE"
fi

# 4. 写入端口配置
echo "[4/7] 配置 HTTP 端口: $HTTP_PORT ..."
if [ -f "$CONFIG_FILE" ]; then
  sed -i "s/\"HttpPort\": *[0-9]*/\"HttpPort\": $HTTP_PORT/" "$CONFIG_FILE"
fi

# 5. 安装 systemd 服务 (条件执行)
if [ "$MUST_RESTART" = false ]; then
  echo "[5/7] 注册 systemd 服务 ..."
  cp "$SCRIPT_DIR/$SERVICE_FILE" /etc/systemd/system/
  systemctl daemon-reload

  echo "[6/7] 启用服务（开机自启）..."
  systemctl enable "$APP_NAME"

  echo "[7/7] 启动服务 ..."
  systemctl start "$APP_NAME"
  
  sleep 2
  if systemctl is-active --quiet "$APP_NAME"; then
    echo "  → 服务已成功启动"
  else
    echo "  ! 服务启动失败，请重启系统后重试"
    MUST_RESTART=true
  fi
else
  echo "[5/7] 跳过服务注册（待重启后生效）"
  echo "[6/7] 跳过服务启用"
  echo "[7/7] 跳过服务启动"
fi

echo ""
if [ "$MUST_RESTART" = true ]; then
  echo "=== 安装完成 ==="
  echo "! 必须重启系统以应用所有更新。"
  read -r -p "是否立即重启？ [y/N] " REPLY_REBOOT
  REPLY_REBOOT=${REPLY_REBOOT:-N}
  if [[ "$REPLY_REBOOT" =~ ^[Yy] ]]; then
    reboot
  fi
else
  echo "=== 安装完成 ==="
  echo ""
  echo "服务地址:  http://localhost:$HTTP_PORT"
  echo "状态查看:  systemctl status $APP_NAME"
  echo "日志查看:   journalctl -u $APP_NAME -f"
  echo "配置编辑:  sudo nano $APP_DIR/appsettings.json"
  echo "重启服务:  systemctl restart $APP_NAME"
  echo "停止服务:  systemctl stop $APP_NAME"
  echo "卸载:      systemctl disable $APP_NAME && rm -rf $APP_DIR /etc/systemd/system/$SERVICE_FILE && systemctl daemon-reload"
  
  if [ "$FORCE_KILLED" = true ]; then
    echo ""
    echo "! 建议重启系统以确保所有更新生效。"
  fi
fi
