#!/bin/bash
set -e

VERSION="${1:-1.0.0}"
CONFIGURATION="${2:-Release}"
RUNTIME="${3:-linux-x64}"
REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/output/publish/$RUNTIME"

echo "=== DeviceHub Linux 发布脚本 ==="
echo "版本: $VERSION"
echo "运行时: $RUNTIME"
echo ""

# 1. dotnet publish
echo "[1/3] 发布项目 ..."
dotnet publish "$REPO_ROOT/src/DeviceHub.Service.Api" \
  -c "$CONFIGURATION" -r "$RUNTIME" --self-contained true \
  -o "$PUBLISH_DIR"

# 2. 生成 hashes.json
echo "[2/3] 计算文件哈希 ..."
find "$PUBLISH_DIR" -type f | while read -r f; do
  rel="${f#$PUBLISH_DIR/}"
  hash=$(sha256sum "$f" | cut -d' ' -f1)
  echo "\"$rel\": \"$hash\""
done | jq -s 'add' > "$PUBLISH_DIR/hashes.json"
echo "  已生成 hashes.json"

# 3. 打包 tar.gz
echo "[3/3] 打包 ..."
mkdir -p "$PUBLISH_DIR/package"
cp -r "$PUBLISH_DIR"/* "$PUBLISH_DIR/package/"
cp "$REPO_ROOT/deploy/ubuntu/devicehub.service" "$PUBLISH_DIR/package/"
cp "$REPO_ROOT/deploy/ubuntu/install.sh" "$PUBLISH_DIR/package/"
chmod +x "$PUBLISH_DIR/package/install.sh"

cd "$PUBLISH_DIR"
tar czf "$REPO_ROOT/output/DeviceHub-$VERSION-$RUNTIME.tar.gz" -C package .
echo "  已生成: output/DeviceHub-$VERSION-$RUNTIME.tar.gz"

echo ""
echo "=== 完成 ==="
