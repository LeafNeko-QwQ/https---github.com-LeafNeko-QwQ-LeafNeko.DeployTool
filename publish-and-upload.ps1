# LeafNeko.DeployTool 发布并上传脚本
# 用法: .\publish-and-upload.ps1 [-SkipUpload] [-Message "自定义提交信息"]

param(
    [switch]$SkipUpload,
    [string]$Message = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSCommandPath
$projectDir = Join-Path $repoRoot "LeafNeko.DeployTool"
$publishDir = Join-Path $repoRoot "publish"

Write-Host "=== LeafNeko.DeployTool 发布脚本 ===" -ForegroundColor Cyan

# 1. 发布
Write-Host "`n[1/4] dotnet publish..." -ForegroundColor Yellow
Push-Location $projectDir
try {
    dotnet publish -c Release -o "$publishDir" /p:Version=(Get-Date -Format "1.0.yy.Mdd")
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }
}
finally {
    Pop-Location
}

# 2. 获取版本
$exePath = Join-Path $publishDir "LeafNeko.DeployTool.exe"
$version = (Get-Item $exePath).VersionInfo.FileVersion
Write-Host "  已生成: v$version" -ForegroundColor Green

# 3. 更新远程版本文件（供自更新检测用）
Write-Host "`n[2/4] 更新 latest-version.txt..." -ForegroundColor Yellow
$versionFile = Join-Path $repoRoot "latest-version.txt"
Set-Content -Path $versionFile -Value $version -NoNewline

# 4. Git 提交并推送
if (-not $SkipUpload) {
    Write-Host "`n[3/4] git 提交..." -ForegroundColor Yellow
    Push-Location $repoRoot
    try {
        git add publish/* LeafNeko.DeployTool/LeafNeko.DeployTool.csproj latest-version.txt
        if ($Message) {
            $commitMsg = $Message
        } else {
            $commitMsg = "发布 v$version"
        }
        git commit -m $commitMsg
        Write-Host "  已提交: $commitMsg" -ForegroundColor Green

        Write-Host "`n[4/4] git push..." -ForegroundColor Yellow
        git push
        Write-Host "  推送完成" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
} else {
    Write-Host "`n[3/4] git 提交 已跳过 (-SkipUpload)" -ForegroundColor DarkYellow
    Write-Host "[4/4] git push 已跳过 (-SkipUpload)" -ForegroundColor DarkYellow
}

Write-Host "`n=== 发布完成 v$version ===" -ForegroundColor Cyan
Write-Host "输出目录: $publishDir"
