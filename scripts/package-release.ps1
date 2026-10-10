# Packages Named Saves Mod for GitHub Releases.
# Usage: .\scripts\package-release.ps1
# The version comes from PluginVersion in Plugin.cs, and the DLL is always rebuilt.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$pluginCs = Get-Content "src\Dizzy.NamedSaves\Plugin.cs" -Raw
if ($pluginCs -notmatch 'PluginVersion\s*=\s*"([^"]+)"') {
    throw "Could not read PluginVersion from Plugin.cs."
}
$Version = $Matches[1]

Write-Host "Building Dizzy.NamedSaves $Version Release..."
dotnet build src\Dizzy.NamedSaves\Dizzy.NamedSaves.csproj -c Release -p:DeployOnBuild=false
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$dll = "src\Dizzy.NamedSaves\bin\Release\Dizzy.NamedSaves.dll"
if (-not (Test-Path $dll)) {
    throw "Missing $dll - build failed."
}

$staging = "dist\Dizzy.NamedSaves-$Version"
$pluginDir = "$staging\Dizzy.NamedSaves"
$zipPath = "dist\Dizzy.NamedSaves-$Version.zip"

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item $dll $pluginDir -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path $pluginDir -DestinationPath $zipPath -Force

Write-Host "Created: $zipPath"
Write-Host "Next: GitHub -> Releases -> Draft new release -> tag v$Version -> attach the zip."
