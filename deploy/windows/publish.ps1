param(
  [Parameter(Mandatory = $false)]
  [string]$Version = "1.0.0",

  [Parameter(Mandatory = $false)]
  [string]$Configuration = "Release",

  [Parameter(Mandatory = $false)]
  [ValidateSet("win-x64")]
  [string]$Runtime = "win-x64",

  [Parameter(Mandatory = $false)]
  [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path "$PSScriptRoot\..\.."
$PublishDir = "$RepoRoot\output\publish\$Runtime"

Write-Host "=== DeviceHub Windows 发布脚本 ===" -ForegroundColor Cyan
Write-Host "版本: $Version"
Write-Host "运行时: $Runtime"
Write-Host ""

# 1. dotnet publish
Write-Host "[1/3] 发布项目..." -ForegroundColor Green
dotnet publish "$RepoRoot\src\DeviceHub.Service.Api" `
  -c $Configuration -r $Runtime --self-contained true `
  /p:Version="$Version" `
  -o $PublishDir

if ($LASTEXITCODE -ne 0) { throw "发布失败" }

# 2. 生成 SHA256
Write-Host "[2/3] 计算哈希..." -ForegroundColor Green
$files = Get-ChildItem -LiteralPath $PublishDir -Recurse -File
$hasher = [System.Security.Cryptography.SHA256]::Create()
$manifest = @{}
foreach ($f in $files) {
  $relPath = $f.FullName.Substring($PublishDir.Length + 1)
  $stream = $f.OpenRead()
  $hashBytes = $hasher.ComputeHash($stream)
  $stream.Dispose()
  $hashHex = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLower()
  $manifest[$relPath] = $hashHex
}
$shaPath = "$PublishDir\.sha256"
$manifest.GetEnumerator() | ForEach-Object { "$($_.Value) *$($_.Key)" } | Out-File -Encoding ASCII $shaPath
Write-Host "  已生成 .sha256 ($($manifest.Count) 个文件)"

if (-not $SkipInstaller) {
  # 3. 编译 Inno Setup 安装包
  Write-Host "[3/3] 编译安装包..." -ForegroundColor Green

  $iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
  if (-not $iscc) {
    # 尝试默认路径
    $isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $isccPath)) {
      throw "未找到 ISCC.exe，请安装 Inno Setup 6"
    }
    $iscc = $isccPath
  }
  else {
    $iscc = $iscc.Source
  }

  & $iscc "$PSScriptRoot\devicehub.iss" `
    /DMyAppVersion="$Version" `
    /DPublishDir="$PublishDir" `
    /O"$RepoRoot\output"

  Write-Host "  安装包已生成: $RepoRoot\output\" -ForegroundColor Green
}
else {
  Write-Host "[3/3] 跳过安装包编译 (-SkipInstaller)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== 完成 ===" -ForegroundColor Cyan
