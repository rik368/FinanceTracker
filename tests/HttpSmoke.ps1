param([string]$BaseUrl = 'http://localhost:5142')
$ErrorActionPreference = 'Stop'
$email = "http-test-$([guid]::NewGuid().ToString('N'))@example.test"
$password = 'Http-Testing2026!'
$registration = Invoke-WebRequest "$BaseUrl/Account/Register" -SessionVariable session
$token = [regex]::Match($registration.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
if (!$token) { throw 'Registration antiforgery token missing' }
$body = @{
    '__RequestVerificationToken' = $token
    '_handler' = 'register'
    'Input.Email' = $email
    'Input.Password' = $password
    'Input.ConfirmPassword' = $password
}
$result = Invoke-WebRequest "$BaseUrl/Account/Register" -Method Post -Body $body -WebSession $session
if ($result.Content -notmatch [regex]::Escape($email)) { throw 'Registration did not sign in the test user' }
$month = Get-Date -Format 'yyyy-MM'
$csv = Invoke-WebRequest "$BaseUrl/exports/transactions?month=$month" -WebSession $session
if ($csv.StatusCode -ne 200 -or $csv.Headers['Content-Type'] -notmatch 'text/csv') { throw 'CSV export failed' }
$text = if ($csv.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($csv.Content) } else { $csv.Content }
if ($text.Trim().Split("`n").Count -ne 1) { throw 'Empty account export unexpectedly contains another account records' }
$bad = Invoke-WebRequest "$BaseUrl/exports/transactions?month=bad" -WebSession $session -SkipHttpErrorCheck
if ($bad.StatusCode -ne 400) { throw 'Invalid month was not rejected' }
$anonymous = Invoke-WebRequest "$BaseUrl/exports/transactions?month=$month" -SkipHttpErrorCheck
if ($anonymous.Content -match 'Date,Category,Type,Amount KZT,Note') { throw 'Anonymous export was allowed' }
$login = Invoke-WebRequest "$BaseUrl/Account/Login" -SessionVariable loginSession
for ($attempt=0; $attempt -lt 5; $attempt++) {
    $form = Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $loginSession
    $token = [regex]::Match($form.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
    $failure = Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -WebSession $loginSession -Body @{
        '__RequestVerificationToken'=$token; '_handler'='login'; 'Input.Email'=$email; 'Input.Password'='Wrong-Password1!'
    }
}
if ($failure.Content -notmatch 'locked out') { throw 'Five failed passwords did not lock the test account' }
Write-Output 'PASS registration, authenticated empty CSV, owner isolation, invalid month, anonymous export restriction and login lockout.'

