<#
.SYNOPSIS
    Builds unpackaged EXE installers (x64 + ARM64) for WinGet distribution.
.DESCRIPTION
    Publishes the extension unpackaged (WindowsPackageType=None, so the packaged-MSIX
    project config is left untouched) and wraps each build in an Inno Setup installer
    that registers the COM server for Command Palette. Run from the project directory
    (where the .csproj and setup-template.iss live). Requires Inno Setup 6.
#>
param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [string[]]$Platforms = @("x64", "arm64")
)

$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot
$project = Join-Path $projectDir "VsCodeProjectsDockExtension.csproj"
$template = Join-Path $projectDir "setup-template.iss"

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { throw "Inno Setup 6 (ISCC.exe) not found." }

foreach ($arch in $Platforms) {
    Write-Host "`n=== Publishing $arch ===" -ForegroundColor Cyan
    $publishDir = Join-Path $projectDir "bin\$Configuration\win-$arch\publish"

    # Unpackaged publish. -p:PublishProfile= clears the MSIX publish profile; the rest
    # comes from the csproj (single-file, AOT-compatible).
    dotnet publish $project `
        --configuration $Configuration `
        --runtime "win-$arch" `
        --self-contained true `
        -p:WindowsPackageType=None `
        -p:PublishProfile= `
        --output $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $arch ($LASTEXITCODE)" }

    Write-Host "=== Packaging $arch installer ===" -ForegroundColor Cyan
    $iss = Get-Content $template -Raw
    $iss = $iss -replace '#define AppVersion ".*"', "#define AppVersion `"$Version`""
    $iss = $iss -replace 'OutputBaseFilename=VsCodeProjectsDock-Setup-\{#AppVersion\}', "OutputBaseFilename=VsCodeProjectsDock-Setup-{#AppVersion}-$arch"
    $iss = $iss -replace 'bin\\Release\\win-x64\\publish', "bin\$Configuration\win-$arch\publish"
    $archLine = if ($arch -eq "arm64") {
        "ArchitecturesAllowed=arm64`r`nArchitecturesInstallIn64BitMode=arm64`r`n"
    } else {
        "ArchitecturesAllowed=x64compatible`r`nArchitecturesInstallIn64BitMode=x64compatible`r`n"
    }
    $iss = $iss -replace '(\[Setup\]\r?\n)', "`$1$archLine"
    $perArch = Join-Path $projectDir "setup-$arch.iss"
    $iss | Out-File -FilePath $perArch -Encoding UTF8

    & $iscc $perArch
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $arch ($LASTEXITCODE)" }
}

Write-Host "`nInstallers:" -ForegroundColor Green
Get-ChildItem (Join-Path $projectDir "bin\$Configuration\installer") -Filter *.exe | ForEach-Object { Write-Host "  $($_.Name)" }
