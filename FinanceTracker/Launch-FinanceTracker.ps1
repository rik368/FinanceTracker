$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$port = 5142
Write-Host ''
Write-Host 'FINANCE TRACKER'
Write-Host '1 - Open on this computer'
Write-Host '2 - Open on this computer and a phone on the same Wi-Fi'
$choice = Read-Host 'Choose 1 or 2 (Enter = 1)'
$urls = "http://localhost:$port"
if ($choice -eq '2') {
    $addresses = @([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
        Where-Object { $_.OperationalStatus -eq 'Up' -and $_.NetworkInterfaceType -ne 'Loopback' } |
        ForEach-Object {
            $nic = $_
            $properties = $nic.GetIPProperties()
            if (@($properties.GatewayAddresses | Where-Object { $_.Address.AddressFamily -eq 'InterNetwork' -and $_.Address.ToString() -ne '0.0.0.0' }).Count -gt 0) {
                foreach ($entry in $properties.UnicastAddresses) {
                    if ($entry.Address.AddressFamily -eq 'InterNetwork' -and $entry.Address.ToString() -notlike '169.254.*') {
                        [PSCustomObject]@{ Name = $nic.Name; Address = $entry.Address.ToString() }
                    }
                }
            }
        })
    if ($addresses.Count -eq 0) { throw 'No active local network found. Connect the computer and phone to the same Wi-Fi.' }
    $index = 0
    if ($addresses.Count -gt 1) {
        for ($i = 0; $i -lt $addresses.Count; $i++) { Write-Host "$($i + 1) - $($addresses[$i].Name): $($addresses[$i].Address)" }
        $selected = Read-Host 'Choose the Wi-Fi network number'
        $number = 0
        if (-not [int]::TryParse($selected, [ref]$number) -or $number -lt 1 -or $number -gt $addresses.Count) { throw 'Invalid network number.' }
        $index = $number - 1
    }
    $phoneUrl = "http://$($addresses[$index].Address):$port"
    $urls += ";$phoneUrl"
    Write-Host ''
    Write-Host "PHONE: $phoneUrl" -ForegroundColor Green
    Write-Host 'Use the same Wi-Fi. localhost on a phone does not point to this computer.'
    Write-Host 'If Windows asks, allow this app on the network you are using.'
    Write-Host 'Use a demo account on a trusted local network; this is an HTTP demonstration.'
}
$occupied = [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
    Where-Object Port -eq $port
if ($occupied) {
    Write-Host 'Port 5142 is already in use. Stop the previous Finance Tracker with Ctrl+C, then launch again.'
    Write-Host 'If you only want to use the existing app, open http://localhost:5142.'
    exit 1
}
Write-Host "COMPUTER: http://localhost:$port"
Write-Host 'Keep this window open. Press Ctrl+C to stop the server.'
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'FinanceTracker.exe')) {
    & (Join-Path $PSScriptRoot 'FinanceTracker.exe') --urls $urls --OpenBrowser true
} else {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Use the ready-to-run Windows package, or install .NET 10 SDK.' }
    & dotnet run --project (Join-Path $PSScriptRoot 'FinanceTracker.csproj') --no-launch-profile --urls $urls --OpenBrowser true
}
exit $LASTEXITCODE
