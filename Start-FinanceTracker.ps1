$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$project = Join-Path $PSScriptRoot 'FinanceTracker'
$executable = Join-Path $project 'bin\Debug\net10.0\FinanceTracker.exe'
$running = Get-Process FinanceTracker -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executable }
if ($running) {
    Write-Host 'Finance Tracker is already running: http://localhost:5142'
    Start-Process 'http://localhost:5142'
    exit 0
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install .NET 10 SDK, then close and reopen VS Code.'
}
Write-Host 'Starting Finance Tracker. Open http://localhost:5142 when the server is ready.'
Write-Host 'Keep this terminal open. Press Ctrl+C to stop the application.'
dotnet run --project $project --launch-profile http
exit $LASTEXITCODE
