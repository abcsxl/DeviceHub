#!/bin/bash
set -e

VERSION="${1:-1.0.0}"
CONFIGURATION="${2:-Release}"
RUNTIME="${3:-linux-x64}"
REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/output/publish/$RUNTIME"

# 架构名称映射（dotnet → nfpm）
case "$RUNTIME" in
  linux-x64)      NFPM_ARCH="amd64" ;;
  linux-arm64)    NFPM_ARCH="arm64" ;;
  linux-arm)      NFPM_ARCH="armhf" ;;
  *)              NFPM_ARCH="amd64" ;;
esac

echo "=== DeviceHub Linux 发布脚本 ==="
echo "版本: $VERSION"
echo "运行时: $RUNTIME"
echo ""

# 1. dotnet publish
echo "[1/4] 发布项目 ..."
dotnet publish "$REPO_ROOT/src/DeviceHub.Service.Api" \
  -c "$CONFIGURATION" -r "$RUNTIME" --self-contained true \
  -o "$PUBLISH_DIR"

# 2. 生成 .sha256
echo "[2/4] 计算文件哈希 ..."
find "$PUBLISH_DIR" -type f -printf '%P\0' | while IFS= read -r -d '' rel; do
  sha256sum "$PUBLISH_DIR/$rel"
done > "$PUBLISH_DIR/.sha256"
echo "  已生成 .sha256"

# 3. nfpm 打包 .deb / .rpm
echo "[3/4] nfpm 打包 .deb / .rpm ..."
mkdir -p "$PUBLISH_DIR/package"
cp -r "$PUBLISH_DIR"/* "$PUBLISH_DIR/package/"
cp "$REPO_ROOT/deploy/linux/devicehub.service" "$PUBLISH_DIR/package/"
cp "$REPO_ROOT/deploy/linux/install.sh" "$PUBLISH_DIR/package/"
cp "$REPO_ROOT/deploy/linux/uninstall.sh" "$PUBLISH_DIR/package/"
chmod +x "$PUBLISH_DIR/package/install.sh"

export NFPM_NAME="devicehub"
export NFPM_VERSION="$VERSION"
export NFPM_ARCH="$NFPM_ARCH"
export NFPM_DESC="DeviceHub — Hardware device management service\nProvides PCSC card reader, printer, and ID card reader management"
export NFPM_PUBLISH_DIR="$PUBLISH_DIR/package"
export NFPM_DEPLOY_DIR="$REPO_ROOT/deploy/linux"

cd "$REPO_ROOT"
rm -f "$PUBLISH_DIR/package/.sha256"

# nfpm 可能不在 PATH，尝试常见位置
NFPM_CMD="$(command -v nfpm || true)"
if [ -z "$NFPM_CMD" ]; then
  echo "  ! nfpm not found, skipping .deb/.rpm (install with: apt install nfpm)"
else
  nfpm package -p deb -f deploy/linux/nfpm.yaml
  nfpm package -p rpm -f deploy/linux/nfpm.yaml
  mv devicehub_*.deb "$REPO_ROOT/output/"
  mv devicehub-*.rpm "$REPO_ROOT/output/"
  echo "  已生成: output/DeviceHub-$VERSION-$NFPM_ARCH.deb"
  echo "  已生成: output/DeviceHub-$VERSION-$NFPM_ARCH.rpm"
fi

# 4. 打包 tar.gz
echo "[4/4] 打包 tar.gz ..."
cd "$PUBLISH_DIR"
tar czf "$REPO_ROOT/output/DeviceHub-$VERSION-$RUNTIME.tar.gz" -C package .
echo "  已生成: output/DeviceHub-$VERSION-$RUNTIME.tar.gz"

echo ""
echo "=== 完成 ==="
