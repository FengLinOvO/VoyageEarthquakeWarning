$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$projectDir = Join-Path $PSScriptRoot 'src\EarthquakeWarning'
$projectFile = Join-Path $projectDir 'EarthquakeWarning.csproj'
$cipxDir = Join-Path $projectDir 'cipx'
$binDir = Join-Path $projectDir 'bin'
$objDir = Join-Path $projectDir 'obj'

foreach ($dir in @($binDir, $objDir, $cipxDir)) {
    if (Test-Path $dir) {
        Remove-Item $dir -Recurse -Force
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found.'
}

if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) {
    throw 'PowerShell 7 (pwsh) is required.'
}

$sdks = dotnet --list-sdks
if (-not ($sdks -match '^8\.')) {
    throw 'No .NET 8 SDK was found.'
}

$audioDir = Join-Path $projectDir 'Assets\Audio'

$requiredAudio = @(
    'eew_blue_nofeel.mp3'
    'eew_blue_nofeel_seconds.mp3'
    'eew_blue_feel.mp3'
    'eew_blue_feel_seconds.mp3'
    'eew_yellow.mp3'
    'eew_yellow_seconds.mp3'
    'eew_orange.mp3'
    'eew_orange_seconds.mp3'
    'eew_red.mp3'
    'eew_red_seconds.mp3'
    'eew_update.mp3'
    'eew_arrived.mp3'
    'arrived_blue_nofeel.mp3'
    'arrived_blue_feel.mp3'
    'arrived_yellow.mp3'
    'arrived_orange.mp3'
    'arrived_red.mp3'
)

foreach ($name in $requiredAudio) {
    $file = Join-Path $audioDir $name

    if (-not (Test-Path $file)) {
        throw "Missing audio file: $name"
    }

    if ((Get-Item $file).Length -le 44) {
        throw "Audio file is empty or invalid: $name"
    }
}

dotnet restore $projectFile
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet restore failed.'
}

dotnet publish $projectFile -c Release -p:CreateCipx=true
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

$publishDir = Join-Path $projectDir 'bin\Release\net8.0-windows\publish'
$publishedAudioDir = Join-Path $publishDir 'Assets\Audio'

foreach ($name in $requiredAudio) {
    $source = Join-Path $audioDir $name
    $published = Join-Path $publishedAudioDir $name

    if (-not (Test-Path $published)) {
        throw "Published audio file is missing: $name"
    }

    if ((Get-Item $source).Length -ne (Get-Item $published).Length) {
        throw "Published audio file size mismatch: $name"
    }
}

if (-not (Test-Path $cipxDir)) {
    throw 'The cipx directory was not created.'
}

$package = Get-ChildItem `
    -Path $cipxDir `
    -Filter '*.cipx' `
    -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    throw 'No cipx package was generated.'
}

Write-Host $package.FullName
