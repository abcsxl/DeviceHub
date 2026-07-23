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
cp "$REPO_ROOT/deploy/linux/postinst.sh" "$PUBLISH_DIR/package/"
cp "$REPO_ROOT/deploy/linux/prerm.sh" "$PUBLISH_DIR/package/"
chmod +x "$PUBLISH_DIR/package/install.sh"

PACKAGE_DIR="$PUBLISH_DIR/package"

cd "$REPO_ROOT"
rm -f "$PACKAGE_DIR/.sha256"

NFPM_CMD="$(command -v nfpm || true)"
if [ -z "$NFPM_CMD" ]; then
  echo "  ! nfpm not found, skipping .deb/.rpm (install with: apt install nfpm)"
else
  cat > "$REPO_ROOT/.nfpm.yaml" << EOF
name: "devicehub"
arch: "$NFPM_ARCH"
platform: linux
version: "$VERSION"
section: net
priority: optional
maintainer: "DeviceHub Team"
description: |-
  DeviceHub — Hardware device management service
  Provides PCSC card reader, printer, and ID card reader management
homepage: "https://github.com/abcsxl/DeviceHub"
license: MIT

contents:
  - src: "$PACKAGE_DIR/"
    dst: /usr/local/bin/devicehub/
  - src: "$PACKAGE_DIR/devicehub.service"
    dst: /etc/systemd/system/devicehub.service

overrides:
  deb:
    depends:
      - libc6
      - libstdc++6
      - pcscd
      - cups
  rpm:
    depends:
      - glibc
      - libstdc++
      - pcsc-lite
      - cups-libs

scripts:
  postinstall: $PACKAGE_DIR/postinst.sh
  preremove: $PACKAGE_DIR/prerm.sh
EOF
  nfpm package -p deb -f "$REPO_ROOT/.nfpm.yaml"
  nfpm package -p rpm -f "$REPO_ROOT/.nfpm.yaml"
  # RPM ~ → _ for GitHub upload compat (RPM uses ~ for pre-release)
  for f in "$REPO_ROOT"/devicehub-*.rpm; do
    [ -f "$f" ] || continue
    mv "$f" "${f//\~/_}" 2>/dev/null || true
  done
  mv devicehub_*.deb "$REPO_ROOT/output/"
  mv devicehub-*.rpm "$REPO_ROOT/output/"
  rm -f "$REPO_ROOT/.nfpm.yaml"
  echo "  已生成: output/devicehub_${VERSION}_${NFPM_ARCH}.deb"
  echo "  已生成: output/devicehub-${VERSION}-1.${NFPM_ARCH}.rpm"
fi

# 4. 打包 tar.gz
echo "[4/4] 打包 tar.gz ..."
cd "$PUBLISH_DIR"
tar czf "$REPO_ROOT/output/DeviceHub-$VERSION-$RUNTIME.tar.gz" -C package .
echo "  已生成: output/DeviceHub-$VERSION-$RUNTIME.tar.gz"

echo ""
echo "=== 完成 ==="
