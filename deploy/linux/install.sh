#!/bin/bash
set -e

APP_NAME="DeviceHub"
APP_DIR="/usr/local/bin/devicehub"
SERVICE_FILE="devicehub.service"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# 语言选择 / Language selection
echo "=== DeviceHub Linux Installer ==="
echo ""
echo "[1] English"
echo "[2] 中文简体"
read -r -p "Select language [1]: " LANG_CHOICE
LANG_CHOICE=${LANG_CHOICE:-1}

if [ "$LANG_CHOICE" = "2" ]; then
  # 中文消息
  MSG_DETECT_UPGRADE="检测到已安装版本，保留现有端口: "
  MSG_PORT_USED="端口已被占用"
  MSG_NEW_PORT="请输入新的 HTTP 端口 "
  MSG_USE_PORT="  → 使用端口: "
  MSG_PORT_AVAILABLE="  → HTTP 端口:  (可用)"
  MSG_PORT_RELEASE=" (安装时将释放)"
  MSG_CHECK_STOP="[1/6] 检查并停止旧服务 ..."
  MSG_FORCE_STOP="! 服务未能自动停止。"
  MSG_FORCE_PROMPT="是否强制结束进程以完成安装？ [Y/n] "
  MSG_FORCE_KILL="  → 正在强制结束进程..."
  MSG_SKIP_FORCE="  → 已跳过强制停止。安装完成后必须重启系统以应用更新。"
  MSG_COPY_FILES="[2/6] 复制文件到 "
  MSG_CONFIG_PORT="[3/6] 配置 HTTP 端口: "
  MSG_REGISTER_SERVICE="[4/6] 注册 systemd 服务 ..."
  MSG_ENABLE_SERVICE="[5/6] 启用服务（开机自启）..."
  MSG_START_SERVICE="[6/6] 启动服务 ..."
  MSG_SERVICE_STARTING="  ! 服务未启动，尝试重试..."
  MSG_SERVICE_OK="  → 服务已成功启动"
  MSG_SERVICE_FAIL="  ! 服务启动失败，请重启系统后重试"
  MSG_SKIP_REGISTER="[4/6] 跳过服务注册（待重启后生效）"
  MSG_SKIP_ENABLE="[5/6] 跳过服务启用"
  MSG_SKIP_START="[6/6] 跳过服务启动"
  MSG_INSTALL_DONE="=== 安装完成 ==="
  MSG_MUST_RESTART="! 必须重启系统以应用所有更新。"
  MSG_REBOOT_PROMPT="是否立即重启？ [y/N] "
  MSG_SERVICE_URL="服务地址: "
  MSG_STATUS_CMD="状态查看: "
  MSG_LOG_CMD="日志查看:   "
  MSG_CONFIG_EDIT="配置编辑:  "
  MSG_RESTART_CMD="重启服务:  "
  MSG_STOP_CMD="停止服务:  "
  MSG_UNINSTALL="卸载:      "
  MSG_SUGGEST_RESTART="! 建议重启系统以确保所有更新生效。"
else
  # English messages
  MSG_DETECT_UPGRADE="Detected existing installation, keeping port: "
  MSG_PORT_USED="Port is already in use"
  MSG_NEW_PORT="Enter new HTTP port "
  MSG_USE_PORT="  → Using port: "
  MSG_PORT_AVAILABLE="  → HTTP port:  (available)"
  MSG_PORT_RELEASE=" (will be released during install)"
  MSG_CHECK_STOP="[1/6] Checking and stopping old service ..."
  MSG_FORCE_STOP="! Service failed to stop automatically."
  MSG_FORCE_PROMPT="Force kill process to complete installation? [Y/n] "
  MSG_FORCE_KILL="  → Force killing process..."
  MSG_SKIP_FORCE="  → Skipped force stop. Must reboot after install to apply updates."
  MSG_COPY_FILES="[2/6] Copying files to "
  MSG_CONFIG_PORT="[3/6] Configuring HTTP port: "
  MSG_REGISTER_SERVICE="[4/6] Registering systemd service ..."
  MSG_ENABLE_SERVICE="[5/6] Enabling service (auto-start on boot)..."
  MSG_START_SERVICE="[6/6] Starting service ..."
  MSG_SERVICE_STARTING="  ! Service not started, retrying..."
  MSG_SERVICE_OK="  → Service started successfully"
  MSG_SERVICE_FAIL="  ! Service failed to start, please reboot and retry"
  MSG_SKIP_REGISTER="[4/6] Skipping service registration (pending reboot)"
  MSG_SKIP_ENABLE="[5/6] Skipping service enable"
  MSG_SKIP_START="[6/6] Skipping service start"
  MSG_INSTALL_DONE="=== Installation Complete ==="
  MSG_MUST_RESTART="! Must reboot to apply all updates."
  MSG_REBOOT_PROMPT="Reboot now? [y/N] "
  MSG_SERVICE_URL="Service URL: "
  MSG_STATUS_CMD="Check status: "
  MSG_LOG_CMD="View logs:     "
  MSG_CONFIG_EDIT="Edit config:  "
  MSG_RESTART_CMD="Restart:      "
  MSG_STOP_CMD="Stop:         "
  MSG_UNINSTALL="Uninstall:    "
  MSG_SUGGEST_RESTART="! Reboot recommended to ensure all updates take effect."
fi

echo ""

# 检查 root
if [ "$EUID" -ne 0 ]; then
  if [ "$LANG_CHOICE" = "2" ]; then
    echo "请以 root 身份运行: sudo ./install.sh"
  else
    echo "Please run as root: sudo ./install.sh"
  fi
  exit 1
fi

# 0. 端口配置
DEFAULT_PORT=5000
HTTP_PORT=$DEFAULT_PORT
IS_UPGRADE=false

# 检查是否为覆盖安装（读取现有配置中的端口）
if [ -f "$APP_DIR/appsettings.json" ]; then
  EXISTING_PORT=$(grep -o '"HttpPort": *[0-9]*' "$APP_DIR/appsettings.json" | grep -o '[0-9]*')
  if [ -n "$EXISTING_PORT" ]; then
    HTTP_PORT=$EXISTING_PORT
    IS_UPGRADE=true
    echo "$MSG_DETECT_UPGRADE$HTTP_PORT"
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
  echo "$MSG_USE_PORT$HTTP_PORT$MESSAGE_PORT_RELEASE"
else
  if check_port $HTTP_PORT; then
    echo "$HTTP_PORT $MSG_PORT_USED"
    read -r -p "$MSG_NEW_PORT[$((HTTP_PORT + 1))]: " PORT_INPUT
    if [ -n "$PORT_INPUT" ]; then
      HTTP_PORT=$PORT_INPUT
    else
      HTTP_PORT=$((HTTP_PORT + 1))
    fi
    echo "$MSG_USE_PORT$HTTP_PORT"
  else
    echo "$MSG_PORT_AVAILABLE$HTTP_PORT"
  fi
fi

echo ""

# 1. 停止并移除旧服务 (增强版：验证停止 + 强杀选项)
echo "$MSG_CHECK_STOP"
MUST_RESTART=false
FORCE_KILLED=false

if systemctl is-active --quiet "$APP_NAME"; then
  systemctl stop "$APP_NAME"
  sleep 2
  
  # 验证是否真的停止
  if systemctl is-active --quiet "$APP_NAME"; then
    echo "$MSG_FORCE_STOP"
    read -r -p "$MSG_FORCE_PROMPT" REPLY_FORCE
    REPLY_FORCE=${REPLY_FORCE:-Y}
    if [[ "$REPLY_FORCE" =~ ^[Yy] ]]; then
      echo "$MSG_FORCE_KILL"
                    pkill -x "DeviceHub.Service.Api" 2>/dev/null || pkill -9 -x "DeviceHub.Service.Api" 2>/dev/null || true
      sleep 1
      FORCE_KILLED=true
    else
      echo "$MSG_SKIP_FORCE"
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
echo "$MSG_COPY_FILES$APP_DIR ..."
mkdir -p "$APP_DIR"
cp -r "$SCRIPT_DIR"/* "$APP_DIR/"
rm -f "$APP_DIR/install.sh"
chmod +x "$APP_DIR/DeviceHub.Service.Api"

# 3. 配置 HTTP 端口
echo "$MSG_CONFIG_PORT$HTTP_PORT ..."
if [ -f "$CONFIG_FILE" ]; then
  sed -i "s/\"HttpPort\": *[0-9]*/\"HttpPort\": $HTTP_PORT/" "$CONFIG_FILE"
fi

# 4. 安装 systemd 服务 (条件执行)
if [ "$MUST_RESTART" = false ]; then
  echo "$MSG_REGISTER_SERVICE"
  cp "$SCRIPT_DIR/$SERVICE_FILE" /etc/systemd/system/
  systemctl daemon-reload

  echo "$MSG_ENABLE_SERVICE"
  systemctl enable "$APP_NAME" 2>/dev/null || true

  echo "$MSG_START_SERVICE"
  systemctl start "$APP_NAME" 2>/dev/null || true
  
  sleep 2
  # 兜底重试：如果首次启动失败，再试一次
  if ! systemctl is-active --quiet "$APP_NAME"; then
    echo "$MSG_SERVICE_STARTING"
    systemctl start "$APP_NAME" 2>/dev/null || true
    sleep 2
  fi
  
  if systemctl is-active --quiet "$APP_NAME"; then
    echo "$MSG_SERVICE_OK"
  else
    echo "$MSG_SERVICE_FAIL"
    MUST_RESTART=true
  fi
else
  echo "$MSG_SKIP_REGISTER"
  echo "$MSG_SKIP_ENABLE"
  echo "$MSG_SKIP_START"
fi

echo ""
if [ "$MUST_RESTART" = true ]; then
  echo "$MSG_INSTALL_DONE"
  echo "$MSG_MUST_RESTART"
  read -r -p "$MSG_REBOOT_PROMPT" REPLY_REBOOT
  REPLY_REBOOT=${REPLY_REBOOT:-N}
  if [[ "$REPLY_REBOOT" =~ ^[Yy] ]]; then
    reboot
  fi
else
  echo "$MSG_INSTALL_DONE"
  echo ""
  echo "$MSG_SERVICE_URL http://localhost:$HTTP_PORT"
  echo "$MSG_STATUS_CMD systemctl status $APP_NAME"
  echo "$MSG_LOG_CMD journalctl -u $APP_NAME -f"
  echo "$MSG_CONFIG_EDIT sudo nano $APP_DIR/appsettings.json"
  echo "$MSG_RESTART_CMD systemctl restart $APP_NAME"
  echo "$MSG_STOP_CMD systemctl stop $APP_NAME"
  echo "$MSG_UNINSTALL systemctl disable $APP_NAME && rm -rf $APP_DIR /etc/systemd/system/$SERVICE_FILE && systemctl daemon-reload"
  
  if [ "$FORCE_KILLED" = true ]; then
    echo ""
    echo "$MSG_SUGGEST_RESTART"
  fi
fi
