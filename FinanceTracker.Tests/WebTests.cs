using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Security.Claims;
using AngleSharp.Html.Parser;
using FinanceTracker.Data;
using FinanceTracker.Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Tests;

public sealed partial class WebTests : IClassFixture<FinanceWebFactory>
{
    private readonly FinanceWebFactory factory;
    public WebTests(FinanceWebFactory factory) => this.factory = factory;

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/ForgotPasswordConfirmation")]
    [InlineData("/Account/ResendEmailConfirmation")]
    [InlineData("/Account/Lockout")]
    [InlineData("/Error")]
    public async Task PublicPagesRender(string path)
    {
        using var client = Client();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType!.ToString());
    }

    [Theory]
    [InlineData("/Account/Manage")]
    [InlineData("/Account/Manage/ChangePassword")]
    [InlineData("/Account/Manage/PersonalData")]
    [InlineData("/exports/transactions?month=2026-11")]
    public async Task PrivatePagesRequireSignIn(string path)
    {
        using var client = Client();
        var response = await client.GetAsync(path);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Unauthorized });
        if (response.StatusCode == HttpStatusCode.Redirect)
            Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Account/Manage")]
    [InlineData("/Account/Manage/ChangePassword")]
    [InlineData("/Account/Manage/Email")]
    [InlineData("/Account/Manage/PersonalData")]
    [InlineData("/Account/Manage/TwoFactorAuthentication")]
    [InlineData("/Account/Manage/EnableAuthenticator")]
    [InlineData("/Account/Manage/Passkeys")]
    [InlineData("/Account/Manage/DeletePersonalData")]
    public async Task AccountPagesRenderForRealIdentityCookie(string path)
    {
        using var client = Client();
        await Register(client);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("An unhandled exception", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RegistrationRejectsInvalidPasswordAndDuplicateEmail()
    {
        using var client = Client();
        var email = Email();
        var weak = await Submit(client, "/Account/Register", "register", new() {
            ["Input.Email"] = email, ["Input.Password"] = "abc", ["Input.ConfirmPassword"] = "abc" });
        Assert.Equal(HttpStatusCode.OK, weak.StatusCode);
        Assert.Contains("at least", await weak.Content.ReadAsStringAsync());
        await Register(client, email);
        using var other = Client();
        var duplicate = await Submit(other, "/Account/Register", "register", Registration(email));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("already taken", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RegistrationRequiresAntiforgeryToken()
    {
        using var client = Client();
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(
            Registration(Email()).Append(new KeyValuePair<string, string>("_handler", "register"))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoginLogoutPasswordChangeAndProfilePersist()
    {
        using var client = Client();
        var email = await Register(client);
        var profile = await Submit(client, "/Account/Manage", "profile", new() { ["Input.PhoneNumber"] = "+15551234567" });
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
        var wrong = await Submit(client, "/Account/Manage/ChangePassword", "change-password", new() {
            ["Input.OldPassword"] = "Wrong1!", ["Input.NewPassword"] = "Changed-2026!", ["Input.ConfirmPassword"] = "Changed-2026!" });
        Assert.Contains("Incorrect password", await wrong.Content.ReadAsStringAsync());
        var changed = await Submit(client, "/Account/Manage/ChangePassword", "change-password", new() {
            ["Input.OldPassword"] = Password, ["Input.NewPassword"] = "Changed-2026!", ["Input.ConfirmPassword"] = "Changed-2026!" });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        await Logout(client);
        var old = await Login(client, email, Password);
        Assert.Contains("Invalid login attempt", await old.Content.ReadAsStringAsync());
        var valid = await Login(client, email, "Changed-2026!");
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        var page = await client.GetStringAsync("/Account/Manage");
        Assert.Contains("+15551234567", WebUtility.HtmlDecode(page));
    }

    [Fact]
    public async Task FiveFailuresLockOnlyThatAccount()
    {
        using var client = Client();
        var email = await Register(client);
        await Logout(client);
        HttpResponseMessage? response = null;
        for (var i = 0; i < 5; i++)
            response = await Login(client, email, "Wrong1!");
        Assert.Contains("Lockout", response!.Headers.Location!.ToString());
        var correctButLocked = await Login(client, email, Password);
        Assert.Contains("Lockout", correctButLocked.Headers.Location!.ToString());
        using var other = Client();
        var otherEmail = await Register(other);
        await Logout(other);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(other, otherEmail, Password)).StatusCode);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("1999-12")]
    [InlineData("2100-01")]
    [InlineData("2026-13")]
    public async Task CsvRejectsInvalidMonths(string month)
    {
        using var client = Client();
        await Register(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/exports/transactions?month=" + month)).StatusCode);
    }

    [Fact]
    public async Task CsvUsesSignedInOwnerAndMonthWithCorrectHeaders()
    {
        using var client = Client();
        var email = await Register(client);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FinanceDb>>();
        var ledger = new Ledger(dbFactory, new OwnerState(user.Id));
        var month = new DateOnly(2026, 11, 1);
        await ledger.SaveAsync(new CategoryInput { Name = "Test, food" });
        var category = Assert.Single((await ledger.ReadAsync(month)).Categories);
        await ledger.SaveAsync(new TransactionInput { CategoryId = category.Id, Date = month, Amount = 12.34m, Note = "=1+1" });
        await ledger.SaveAsync(new TransactionInput { CategoryId = category.Id, Date = month.AddMonths(1), Amount = 99, Note = "Next month" });
        var response = await client.GetAsync("/exports/transactions?month=2026-11");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains("transactions-2026-11.csv", response.Content.Headers.ContentDisposition!.ToString());
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Test, food\"", csv);
        Assert.Contains(",12.34,", csv);
        Assert.Contains("'=1+1", csv);
        Assert.DoesNotContain("Next month", csv);
        using var other = Client();
        await Register(other);
        var empty = await other.GetStringAsync("/exports/transactions?month=2026-11");
        Assert.DoesNotContain("Test, food", empty);
        Assert.Single(empty.Trim().Split('\n'));
    }

    [Fact]
    public async Task StaticStylesAndBlazorScriptAreServed()
    {
        using var client = Client();
        var html = new HtmlParser().ParseDocument(await client.GetStringAsync("/"));
        var paths = html.QuerySelectorAll("link[rel=stylesheet]").Select(x => x.GetAttribute("href")!)
            .Concat(html.QuerySelectorAll("script[src]").Select(x => x.GetAttribute("src")!));
        foreach (var path in paths)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 20);
        }
    }

    private const string Password = "Web-Testing2026!";
    private static string Email() => $"test-{Guid.NewGuid():N}@example.test";
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
    private static Dictionary<string, string> Registration(string email) => new() {
        ["Input.Email"] = email, ["Input.Password"] = Password, ["Input.ConfirmPassword"] = Password };
    private static async Task<string> Register(HttpClient client, string? email = null)
    {
        email ??= Email();
        var response = await Submit(client, "/Account/Register", "register", Registration(email));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return email;
    }
    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        Submit(client, "/Account/Login", "login", new() { ["Input.Email"] = email, ["Input.Password"] = password });
    private static async Task Logout(HttpClient client)
    {
        var html = new HtmlParser().ParseDocument(await client.GetStringAsync("/"));
        var token = html.QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["ReturnUrl"] = "." }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static async Task<HttpResponseMessage> Submit(HttpClient client, string route, string handler, Dictionary<string, string> fields)
    {
        var response = await client.GetAsync(route);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var html = new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync());
        var form = html.QuerySelectorAll("form").Single(x => x.QuerySelector("input[name=_handler]")?.GetAttribute("value") == handler);
        fields["__RequestVerificationToken"] = form.QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
        fields["_handler"] = handler;
        return await client.PostAsync(route, new FormUrlEncodedContent(fields));
    }

    private sealed class OwnerState(string owner) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, owner)], "integration"))));
    }
}

public sealed class FinanceWebFactory : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FinanceTrackerTests", Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(directory);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(directory, "identity.db")}");
        builder.UseSetting("ConnectionStrings:FinanceConnection", $"Data Source={Path.Combine(directory, "finance.db")};Foreign Keys=True");
        builder.ConfigureTestServices(services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Error));
    }
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnectionClear();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    private static void SqliteConnectionClear() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}



