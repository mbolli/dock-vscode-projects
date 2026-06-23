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

# Public symbols (.appxsym) per arch, bundled into the .msixupload so Partner Center
# can symbolicate crash dumps. .appxsym = a zip of the managed .pdb; .msixupload = a
# zip of {bundle + .appxsym files}. No mspdbcmf / C++ toolset needed for managed PDBs.
$syms = @()

foreach ($arch in @("x64", "ARM64")) {
    Write-Host "=== Building $arch MSIX ===" -ForegroundColor Cyan
    dotnet build $project -c $Configuration -p:Platform=$arch `
        -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never -p:AppxPackageSigningEnabled=false `
        -p:AppxPackageVersion=$Version "-p:AppxPackageDir=$appPackages\$arch\"
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $arch ($LASTEXITCODE)" }

    # Locate the app's portable PDB from this build's output (bin\<arch>\...\win-<rid>\).
    $rid = "win-" + $arch.ToLower()
    $pdb = Get-ChildItem (Join-Path $projectDir "bin\$arch\$Configuration") -Recurse -Filter "VsCodeProjectsDockExtension.pdb" -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -like "*\$rid" } | Select-Object -First 1
    if (-not $pdb) { throw "PDB not found for $arch (expected under bin\$arch\$Configuration\...\$rid\)." }

    $sym = Join-Path $appPackages ("VsCodeProjectsDockExtension_{0}_{1}.appxsym" -f $Version, $arch.ToLower())
    $symZip = "$sym.zip"
    if (Test-Path $symZip) { Remove-Item $symZip -Force }
    Compress-Archive -Path $pdb.FullName -DestinationPath $symZip -Force
    Move-Item $symZip $sym -Force
    $syms += $sym
    Write-Host "    symbols: $sym" -ForegroundColor DarkGray
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

# Assemble the .msixupload (zip of the bundle + both .appxsym), the format Partner
# Center prefers - it carries the symbols used for crash analytics.
$uploadStage = Join-Path $appPackages "upload"
if (Test-Path $uploadStage) { Remove-Item $uploadStage -Recurse -Force }
New-Item -ItemType Directory -Path $uploadStage -Force | Out-Null
Copy-Item $bundle $uploadStage
foreach ($s in $syms) { Copy-Item $s $uploadStage }

$uploadZip = Join-Path $appPackages "VsCodeProjectsDockExtension_$Version.zip"
if (Test-Path $uploadZip) { Remove-Item $uploadZip -Force }
Compress-Archive -Path (Join-Path $uploadStage "*") -DestinationPath $uploadZip -Force
$upload = Join-Path $appPackages "VsCodeProjectsDockExtension_$Version.msixupload"
if (Test-Path $upload) { Remove-Item $upload -Force }
Move-Item $uploadZip $upload -Force

Write-Host "`nSubmit this to Partner Center (unsigned - the Store signs it):" -ForegroundColor Green
Write-Host "  $upload" -ForegroundColor Green
Write-Host "  (contains the .msixbundle + x64/arm64 .appxsym symbols for crash analytics)"
Write-Host "`nRaw bundle (no symbols), if needed:" -ForegroundColor DarkGray
Write-Host "  $bundle"
