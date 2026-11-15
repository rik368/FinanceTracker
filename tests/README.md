# Browser verification

Start the application first. Install Node.js, then run these commands from this tests directory.

```powershell
npm install
npm test
```

The script uses Microsoft Edge by default. It creates an isolated test account and deletes that test account after completing verification. It exercises registration, sample loading, search, validation, transaction CRUD, CSV, mobile width, logout and login. BROWSER_CHANNEL can select another Playwright-supported installed browser and BASE_URL can select a different local address.

The packaged script passed in Edge, Chrome, Firefox and WebKit on Windows. JSON version/results and screenshots are in verification. For Firefox/WebKit run npx playwright install firefox webkit, then set $env:BROWSER_ENGINE="firefox" or "webkit". For Chrome use $env:BROWSER_CHANNEL="chrome" with BROWSER_ENGINE unset. Clear BROWSER_CHANNEL before switching engines. WebKit on Windows does not replace native Safari testing.

## HTTP integration smoke test

With PowerShell 7 and the app running, execute pwsh -File tests/HttpSmoke.ps1 from the solution folder. This test creates a disposable account, verifies authentication and export boundaries, and intentionally locks that test account by entering five wrong passwords. This script passed during the audit.

The browser replay also deletes its own account and checks that the deleted credentials can no longer sign in.
