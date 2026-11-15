# Coverage evidence

67 tests passed, zero failed or skipped. Full application: **1,844/2,622 lines (70.32%)**, branch coverage **56.39%**. No production files were excluded to increase this figure. Generated Identity migrations and all included Identity template pages remain in the denominator. Coverage shows execution, not proof of correctness.

The earlier 7.58% came from 24 mostly calculation/database cases; HTTP/account and Blazor render/event paths were not exercised. New component tests, real cookie/antiforgery integration tests, recovery/2FA scenarios and concurrent SQLite operations raise measured coverage. Browser tests run separately and are not added to this percentage.

| File | Executed lines | Line coverage |
|---|---:|---:|
| Components\Account\IdentityComponentsEndpointRouteBuilderExtensions.cs | 61/122 | 50.00% |
| Components\Account\IdentityNoOpEmailSender.cs | 2/4 | 50.00% |
| Components\Account\IdentityRedirectManager.cs | 25/30 | 83.33% |
| Components\Account\IdentityRevalidatingAuthenticationStateProvider.cs | 2/20 | 10.00% |
| Components\Account\Pages\ConfirmEmail.razor | 0/20 | 0.00% |
| Components\Account\Pages\ConfirmEmailChange.razor | 0/29 | 0.00% |
| Components\Account\Pages\ExternalLogin.razor | 0/108 | 0.00% |
| Components\Account\Pages\ForgotPassword.razor | 5/19 | 26.32% |
| Components\Account\Pages\Login.razor | 40/51 | 78.43% |
| Components\Account\Pages\LoginWith2fa.razor | 25/30 | 83.33% |
| Components\Account\Pages\LoginWithRecoveryCode.razor | 23/28 | 82.14% |
| Components\Account\Pages\Manage\ChangePassword.razor | 23/32 | 71.88% |
| Components\Account\Pages\Manage\DeletePersonalData.razor | 31/37 | 83.78% |
| Components\Account\Pages\Manage\Disable2fa.razor | 0/27 | 0.00% |
| Components\Account\Pages\Manage\Email.razor | 14/54 | 25.93% |
| Components\Account\Pages\Manage\EnableAuthenticator.razor | 66/75 | 88.00% |
| Components\Account\Pages\Manage\ExternalLogins.razor | 0/80 | 0.00% |
| Components\Account\Pages\Manage\GenerateRecoveryCodes.razor | 19/27 | 70.37% |
| Components\Account\Pages\Manage\Index.razor | 20/30 | 66.67% |
| Components\Account\Pages\Manage\Passkeys.razor | 14/86 | 16.28% |
| Components\Account\Pages\Manage\PersonalData.razor | 5/8 | 62.50% |
| Components\Account\Pages\Manage\RenamePasskey.razor | 0/39 | 0.00% |
| Components\Account\Pages\Manage\ResetAuthenticator.razor | 13/17 | 76.47% |
| Components\Account\Pages\Manage\SetPassword.razor | 0/30 | 0.00% |
| Components\Account\Pages\Manage\TwoFactorAuthentication.razor | 17/45 | 37.78% |
| Components\Account\Pages\Register.razor | 40/52 | 76.92% |
| Components\Account\Pages\RegisterConfirmation.razor | 0/29 | 0.00% |
| Components\Account\Pages\ResendEmailConfirmation.razor | 5/20 | 25.00% |
| Components\Account\Pages\ResetPassword.razor | 0/28 | 0.00% |
| Components\Account\PasskeyInputModel.cs | 0/2 | 0.00% |
| Components\Account\Shared\ExternalLoginPicker.razor | 0/14 | 0.00% |
| Components\Account\Shared\ManageLayout.razor | 1/1 | 100.00% |
| Components\Account\Shared\ManageNavMenu.razor | 4/6 | 66.67% |
| Components\Account\Shared\PasskeySubmit.razor | 10/10 | 100.00% |
| Components\Account\Shared\RedirectToLogin.razor | 0/3 | 0.00% |
| Components\Account\Shared\ShowRecoveryCodes.razor | 6/6 | 100.00% |
| Components\Account\Shared\StatusMessage.razor | 15/15 | 100.00% |
| Components\Layout\MainLayout.razor | 2/2 | 100.00% |
| Components\Pages\Auth.razor | 0/1 | 0.00% |
| Components\Pages\BarChart.razor | 11/11 | 100.00% |
| Components\Pages\ChartRow.cs | 1/1 | 100.00% |
| Components\Pages\Error.razor | 8/8 | 100.00% |
| Components\Pages\FinanceWorkspace.razor | 161/163 | 98.77% |
| Components\Pages\FormActions.razor | 1/1 | 100.00% |
| Components\Pages\Metric.razor | 4/4 | 100.00% |
| Components\Pages\MonthPicker.razor | 6/6 | 100.00% |
| Components\Pages\RowActions.razor | 3/3 | 100.00% |
| Data\AccountDeletion.cs | 47/48 | 97.92% |
| Data\ApplicationDbContext.cs | 1/1 | 100.00% |
| Data\ExportEndpoint.cs | 18/18 | 100.00% |
| Data\FinanceDb.cs | 34/34 | 100.00% |
| Data\Ledger.cs | 98/101 | 97.03% |
| Data\Migrations\00000000000000_CreateIdentitySchema.Designer.cs | 314/314 | 100.00% |
| Data\Migrations\00000000000000_CreateIdentitySchema.cs | 192/210 | 91.43% |
| Data\Migrations\ApplicationDbContextModelSnapshot.cs | 314/314 | 100.00% |
| Domain\CsvExport.cs | 18/18 | 100.00% |
| Domain\Demo.cs | 28/28 | 100.00% |
| Domain\Finance.cs | 23/23 | 100.00% |
| Domain\MonthState.cs | 3/3 | 100.00% |
| Program.cs | 71/76 | 93.42% |
