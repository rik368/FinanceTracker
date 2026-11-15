$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'FinanceTracker.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Use this helper inside the extracted Windows package.' }
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"'
    Start-Process powershell.exe -ArgumentList $arguments -Verb RunAs -Wait -WindowStyle Hidden
    exit
}
try {
    # Scope changes to this executable only. Never disable Windows Firewall.
    $existing = @(Get-NetFirewallApplicationFilter | Where-Object { $_.Program -eq $exe } | Get-NetFirewallRule)
    $existing | Where-Object { $_.Direction -eq 'Inbound' -and $_.Action -eq 'Block' } | Disable-NetFirewallRule
    $ruleName = 'FinanceTracker-Phone-' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($exe)).Replace('/','_')
    if (-not (Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name $ruleName -DisplayName 'Finance Tracker - phone on local network' -Direction Inbound -Action Allow -Program $exe -Protocol TCP -LocalPort 5142 -RemoteAddress LocalSubnet -Profile Any | Out-Null
    }
    'Phone access enabled for this application, TCP 5142, local subnet only.' | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Phone-Access-Result.txt')
} catch {
    $_.Exception.Message | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Phone-Access-Result.txt')
    throw
}
