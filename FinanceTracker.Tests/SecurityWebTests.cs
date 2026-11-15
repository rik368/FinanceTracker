using System.Net;
using System.Security.Cryptography;
using AngleSharp.Html.Parser;
using FinanceTracker.Data;
using FinanceTracker.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests;

public sealed partial class WebTests
{
    [Fact]
    public async Task AuthenticatorRecoveryAndResetWorkThroughRealForms()
    {
        using var client = Client();
        var email = await Register(client);
        var html = new HtmlParser().ParseDocument(await client.GetStringAsync("/Account/Manage/EnableAuthenticator"));
        var key = html.QuerySelector("kbd")!.TextContent.Replace(" ", "").ToUpperInvariant();
        var invalid = await Submit(client, "/Account/Manage/EnableAuthenticator", "send-code", new() { ["Input.Code"] = "abcdef" });
        Assert.Contains("invalid", await invalid.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var enabled = await Submit(client, "/Account/Manage/EnableAuthenticator", "send-code", new() { ["Input.Code"] = Totp(key) });
        html = new HtmlParser().ParseDocument(await enabled.Content.ReadAsStringAsync());
        var recovery = html.QuerySelectorAll(".recovery-code").Select(x => x.TextContent.Trim()).ToArray();
        Assert.Equal(10, recovery.Length);
        await Logout(client);
        var login = await Login(client, email, Password);
        Assert.Contains("LoginWith2fa", login.Headers.Location!.ToString());
        var wrong = await Submit(client, "/Account/LoginWith2fa", "login-with-2fa", new() { ["Input.TwoFactorCode"] = "abcdef" });
        Assert.Contains("Invalid authenticator code", await wrong.Content.ReadAsStringAsync());
        var valid = await Submit(client, "/Account/LoginWith2fa", "login-with-2fa", new() { ["Input.TwoFactorCode"] = Totp(key) });
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        await Logout(client);
        await Login(client, email, Password);
        var recovered = await Submit(client, "/Account/LoginWithRecoveryCode", "login-with-recovery-code", new() { ["Input.RecoveryCode"] = recovery[0] });
        Assert.Equal(HttpStatusCode.Redirect, recovered.StatusCode);
        await Logout(client);
        await Login(client, email, Password);
        var reused = await Submit(client, "/Account/LoginWithRecoveryCode", "login-with-recovery-code", new() { ["Input.RecoveryCode"] = recovery[0] });
        Assert.Contains("Invalid recovery code", await reused.Content.ReadAsStringAsync());
        await Submit(client, "/Account/LoginWithRecoveryCode", "login-with-recovery-code", new() { ["Input.RecoveryCode"] = recovery[1] });
        var regenerated = await Submit(client, "/Account/Manage/GenerateRecoveryCodes", "generate-recovery-codes", new());
        Assert.Equal(10, new HtmlParser().ParseDocument(await regenerated.Content.ReadAsStringAsync()).QuerySelectorAll(".recovery-code").Length);
        var reset = await Submit(client, "/Account/Manage/ResetAuthenticator", "reset-authenticator", new());
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        await Logout(client);
        Assert.DoesNotContain("LoginWith2fa", (await Login(client, email, Password)).Headers.Location!.ToString());
    }

    [Fact]
    public async Task ConcurrentFileDatabaseWritesPreserveEveryOwnerAndAmount()
    {
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<FinanceDb>>();
        var month = new DateOnly(2026, 11, 1);
        var owners = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid().ToString()).ToArray();
        var started = System.Diagnostics.Stopwatch.StartNew();
        await Task.WhenAll(owners.Select(owner => Task.Run(async () =>
        {
            var ledger = new Ledger(dbFactory, new OwnerState(owner));
            await ledger.SaveAsync(new CategoryInput { Name = "Concurrent" });
            var category = Assert.Single((await ledger.ReadAsync(month)).Categories);
            for (var i = 0; i < 25; i++)
            {
                await ledger.SaveAsync(new TransactionInput { CategoryId = category.Id, Date = month, Amount = 1.23m, Note = $"Entry {i}" });
                Assert.Equal(i + 1, (await ledger.ReadAsync(month)).Transactions.Length);
            }
        })));
        foreach (var owner in owners)
        {
            var result = await new Ledger(dbFactory, new OwnerState(owner)).ReadAsync(month);
            Assert.Equal(25, result.Transactions.Length);
            Assert.Equal(3075, Finance.Summarize(result.Entries).Expenses);
            Assert.All(result.Transactions, x => Assert.Equal(owner, x.Owner));
        }
        Assert.True(started.Elapsed < TimeSpan.FromMinutes(2), "Local workload exceeded the generous test timeout.");
    }

    private static string Totp(string key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var character in key)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); }
        }
        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter);
        int offset = hash[^1] & 15;
        int value = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1000000).ToString("D6");
    }
}
