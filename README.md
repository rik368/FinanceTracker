# Finance Tracker

Final project, group IT-2508.

| Team member | Contribution |
|---|---|
| Rat Renat | 50% |
| Yerkebulan Alshynbekuly | 50% |

Shared work: design, implementation, testing, documentation and presentation.

An English-language Blazor Web App for personal income, expenses and monthly budgets. Currency is KZT. The displayed balance is income minus expenses within the selected month.

## Requirements and launch

Install .NET 10 SDK. Open the solution folder in Visual Studio Code. Choose Terminal → New Terminal and run the following commands from the folder containing FinanceTracker.sln.

```powershell
dotnet restore FinanceTracker.sln
dotnet run --project FinanceTracker --launch-profile http
```

Open http://localhost:5142. Create an account, then select **Load sample data**. SQLite files and their initial schemas are created automatically. An Internet connection is needed for the first package restore.

In VS Code, Terminal → Run Task → **Run Finance Tracker** runs the same command. Keep the terminal running while using the application. Ctrl+C stops it. A second process cannot use the same port.

Visual Studio users can open FinanceTracker.sln, select FinanceTracker as the startup project, choose the http profile and press Ctrl+F5.

## User guide

1. **Categories** defines the income or expense type. Names are unique within each type and account. Rename a category using Edit. Referenced categories must have their transactions and budgets reassigned or removed before deletion.
2. **Transactions** supports create, edit, delete, monthly navigation, category filtering and case-insensitive search in notes or category names. Results display 20 rows per page. Amounts are positive, with direction determined by the category.
3. **Export month CSV** downloads all of your transactions for the selected month, including entries outside the current search results. Amounts use a decimal point and the currency is KZT.
4. **Budgets** sets one limit per expense category per month. The progress indicator shows actual spending and the text reports remaining funds or an overspend.
5. **Overview** shows monthly totals, category bars and the latest four transactions.
6. **Analytics** projects current-month spending by extending the average spent per elapsed day. One-off bills and transactions dated later in the month can affect interpretation.
7. **Account** supports profile management, password changes and the Identity security features. Five failed passwords temporarily lock the account for five minutes.

Sample data contains four categories, six transactions and three budgets. It is available for accounts with an empty category list. New accounts in the submitted archive start empty.

## Tests

```powershell
dotnet test FinanceTracker.sln
dotnet test FinanceTracker.sln --collect:"XPlat Code Coverage"
```

The suite includes 67 test cases using xUnit and actual in-memory and temporary file SQLite. See QA.md for measured coverage, browser checks and checks still pending. The executed browser replay suite is described in tests/README.md.

## Files and backups

The identity database is FinanceTracker/Data/app.db. Financial records are in FinanceTracker/finance.db. Stop the application before copying both files for a consistent local backup. Keep the databases private. The submission ZIP excludes database files, build output and local account credentials.

The initial finance schema uses EnsureCreated. A changed finance model requires a planned migration for existing databases. Do not delete a database containing records you want to retain.

## Deployment boundary

This is a locally demonstrated final project. Before public hosting, configure HTTPS, durable protected Data Protection keys, backups, monitoring and a real email sender. Email delivery and password recovery by email are not configured. Concurrent edits currently use the last successful write. SQLite concurrent-write limits and multi-server hosting require further work.

## Submission materials

- ARCHITECTURE.md contains architecture, UML and functional programming notes.
- QA.md records actual verification results.
- FinanceTracker-Defense.pptx is the editable English slide deck.
- dashboard.png is a screenshot of the running application.

Team: Rat Renat and Yerkebulan Alshynbekuly, group IT-2508. Equal contribution: 50% each across design, implementation, testing, documentation and presentation.

## Delete an account

Open account settings → Personal data → Delete. Enter the current password and select **Delete data and close my account**. All months of transactions, budgets and categories are removed together with the account. Other accounts are unaffected. This cannot be undone. Export anything you need before deleting.

## VS Code on Windows: repeated launch

Choose Terminal → Run Task → Run Finance Tracker. The Windows task uses Start-FinanceTracker.ps1: it starts the project or opens the already-running instance. If you edit application code, stop the running server with Ctrl+C in its terminal before rebuilding. A background instance of the same executable can otherwise cause MSB3026/MSB3027 file-lock errors.

