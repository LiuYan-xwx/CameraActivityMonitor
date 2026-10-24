$projectRoot = Join-Path $PSScriptRoot "..\CameraActivityMonitor"
$projectPath = Join-Path $projectRoot "CameraActivityMonitor.csproj"
$binPath = Join-Path $projectRoot "bin"

if (Test-Path -Path $binPath) {
    Remove-Item $binPath -Recurse -Force
}

dotnet publish $projectPath -c Release -p:CreateCipx=true
