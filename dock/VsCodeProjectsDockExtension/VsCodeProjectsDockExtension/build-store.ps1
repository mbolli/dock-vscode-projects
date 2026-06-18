<#
.SYNOPSIS
    Builds the unsigned MSIX bundle (x64 + ARM64) to submit to the Microsoft Store.
.DESCRIPTION
    The Store signs the package at submission, so this produces UNSIGNED packages.
    Set the Partner Center identity (Package.appxmanifest Identity + .csproj
    AppxPackageIdentityName/Publisher) before a real submission. Run from this project
    directory. Requires the .NET 10 SDK and the Windows SDK (makeappx).
#>
param(
    [string]$Version = "0.1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot
$project = Join-Path $projectDir "VsCodeProjectsDockExtension.csproj"
$appPackages = Join-Path $projectDir "AppPackages"

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName | Select-Object -Last 1
if (-not $makeappx) { throw "makeappx.exe not found (install the Windows SDK)." }

if (Test-Path $appPackages) { Remove-Item $appPackages -Recurse -Force }

foreach ($arch in @("x64", "ARM64")) {
    Write-Host "=== Building $arch MSIX ===" -ForegroundColor Cyan
    dotnet build $project -c $Configuration -p:Platform=$arch `
        -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never -p:AppxPackageSigningEnabled=false `
        -p:AppxPackageVersion=$Version "-p:AppxPackageDir=$appPackages\$arch\"
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $arch ($LASTEXITCODE)" }
}

# Collect the two produced .msix into a flat staging dir, then bundle.
$stage = Join-Path $appPackages "bundle"
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Get-ChildItem $appPackages -Recurse -Filter *.msix |
    Where-Object { $_.DirectoryName -notlike "*\bundle" } |
    ForEach-Object { Copy-Item $_.FullName $stage }

$bundle = Join-Path $appPackages "VsCodeProjectsDockExtension_$Version.msixbundle"
& $makeappx.FullName bundle /d $stage /p $bundle /o
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed ($LASTEXITCODE)" }

Write-Host "`nBundle (unsigned — upload to Partner Center, the Store signs it):" -ForegroundColor Green
Write-Host "  $bundle"
