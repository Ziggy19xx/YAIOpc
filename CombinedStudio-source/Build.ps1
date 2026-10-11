param([string]$DotNet='dotnet',[string]$OutputDirectory=(Join-Path $PSScriptRoot '..\outputs\FramePilot-VelvetVoice'))
$ErrorActionPreference='Stop'
& $DotNet publish (Join-Path $PSScriptRoot 'CombinedStudio.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RuntimeFrameworkVersion=8.0.31 -p:NuGetAudit=false -o $OutputDirectory
if($LASTEXITCODE -ne 0){throw 'Combined application build failed.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Bundled\monitor'),(Join-Path $PSScriptRoot 'Bundled\models') -Destination $OutputDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'HANDOFF.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TRUE-STRETCH.md'),(Join-Path $PSScriptRoot 'CROSSHAIR-STUDIO.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FramePilot\THIRD-PARTY-NOTICES.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VelvetVoice\THIRD-PARTY.txt') -Destination (Join-Path $OutputDirectory 'VELVET-THIRD-PARTY.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VelvetVoice\README.md') -Destination (Join-Path $OutputDirectory 'VOICE-SETUP.md') -Force
Write-Output "Run $OutputDirectory\YAIOpc.exe"
