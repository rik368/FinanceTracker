using FinanceTracker.Data;
using FinanceTracker.Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinanceTracker.Tests;

public class FinanceTests
{
    [Fact]
    public void EmptyLedgerHasZeroBalance() => Assert.Equal(0, Finance.Summarize([]).Balance);

    [Fact]
    public void TotalsAndCategorySpendingExcludeIncomeFromExpenses()
    {
        Entry[] entries = [
            new(1, "Salary", Flow.Income, new(2026, 11, 1), 10000),
            new(2, "Food", Flow.Expense, new(2026, 11, 2), 2500),
            new(2, "Food", Flow.Expense, new(2026, 11, 3), 1500)
        ];
        Assert.Equal(new Totals(10000, 4000), Finance.Summarize(entries));
        Assert.Equal(6000, Finance.Summarize(entries).Balance);
        Assert.Equal(4000, Finance.Spending(entries)[2]);
        Assert.Single(Finance.Spending(entries));
    }

    [Theory]
    [InlineData(0, 0, 30, 0)]
    [InlineData(150, 3, 30, 1500)]
    public void ForecastUsesElapsedDays(long spent, int elapsed, int days, int expected) =>
        Assert.Equal(expected, Finance.Forecast(spent, elapsed, days));

    [Fact]
    public void MoneyUsesExactMinorUnits() => Assert.Equal(101, Finance.ToCents(1.005m));

    [Fact]
    public async Task SqliteRoundTripSupportsCrudAndAccountIsolation()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var factory = new Factory(new DbContextOptionsBuilder<FinanceDb>().UseSqlite(connection).Options);
        await using var db = factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        var alice = new Ledger(factory, new UserState("alice"));
        var bob = new Ledger(factory, new UserState("bob"));
        var month = new DateOnly(2026, 11, 1);

        await alice.SaveAsync(new CategoryInput { Name = "Food" });
        await Assert.ThrowsAsync<LedgerRuleException>(() => alice.SaveAsync(new CategoryInput { Name = " food " }));
        var category = Assert.Single((await alice.ReadAsync(month)).Categories);
        var entry = new TransactionInput { CategoryId = category.Id, Amount = 30.25m, Date = month };
        await Assert.ThrowsAsync<LedgerRuleException>(() => bob.SaveAsync(entry));
        await Assert.ThrowsAsync<LedgerRuleException>(() => alice.SaveAsync(new TransactionInput { CategoryId = category.Id, Date = month }));
        await alice.SaveAsync(entry);
        await alice.SaveAsync(new BudgetInput { CategoryId = category.Id, Amount = 25 }, month);
        var snapshot = await alice.ReadAsync(month);
        entry.Id = Assert.Single(snapshot.Transactions).Id;
        var budget = Assert.Single(snapshot.Budgets);
        Assert.Equal(3025, Finance.Spending(snapshot.Entries)[category.Id]);
        Assert.Equal(2500, budget.Cents);
        Assert.Empty((await alice.ReadAsync(month.AddMonths(1))).Transactions);
        Assert.Empty((await bob.ReadAsync(month)).Transactions);
        await bob.DeleteAsync("transaction", entry.Id);
        Assert.Single((await alice.ReadAsync(month)).Transactions);
        await Assert.ThrowsAsync<LedgerRuleException>(() => alice.DeleteAsync("category", category.Id));

        entry.Amount = 20;
        await alice.SaveAsync(entry);
        await alice.SaveAsync(new BudgetInput { Id = budget.Id, CategoryId = category.Id, Amount = 35 }, month);
        await alice.SaveAsync(new CategoryInput { Id = category.Id, Name = "Groceries" });
        snapshot = await alice.ReadAsync(month);
        Assert.Equal("Groceries", Assert.Single(snapshot.Categories).Name);
        Assert.Equal(2000, Assert.Single(snapshot.Transactions).Cents);
        Assert.Equal(3500, Assert.Single(snapshot.Budgets).Cents);
        await alice.DeleteAsync("transaction", entry.Id);
        await alice.DeleteAsync("budget", budget.Id);
        await alice.DeleteAsync("category", category.Id);
        snapshot = await alice.ReadAsync(month);
        Assert.Empty(snapshot.Transactions);
        Assert.Empty(snapshot.Budgets);
        Assert.Empty(snapshot.Categories);
    }

    [Fact]
    public async Task DemoCreatesDataOnlyForCurrentAccount()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var factory = new Factory(new DbContextOptionsBuilder<FinanceDb>().UseSqlite(connection).Options);
        await using var db = factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        var user = new UserState("demo");
        var month = new DateOnly(2026, 11, 1);
        var demo = new Demo(factory, user);
        await demo.PopulateAsync(month);
        var snapshot = await new Ledger(factory, user).ReadAsync(month);
        Assert.Equal(6, snapshot.Transactions.Length);
        Assert.Equal(3, snapshot.Budgets.Length);
        Assert.Empty((await new Ledger(factory, new UserState("other")).ReadAsync(month)).Categories);
        await Assert.ThrowsAsync<LedgerRuleException>(() => demo.PopulateAsync(month));
    }


    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1000000001)]
    public async Task InvalidAmountsAreRejected(long amount)
    {
        await using var setup = await Fixture.Create();
        await Assert.ThrowsAsync<LedgerRuleException>(() => setup.Ledger.SaveAsync(
            new TransactionInput { CategoryId = setup.CategoryId, Amount = amount }));
        Assert.Empty((await setup.Ledger.ReadAsync(setup.Month)).Transactions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyCategoryNamesAreRejected(string name)
    {
        await using var setup = await Fixture.Create();
        await Assert.ThrowsAsync<LedgerRuleException>(() =>
            setup.Ledger.SaveAsync(new CategoryInput { Name = name }));
    }

    [Fact]
    public async Task OwnershipChecksApplyToUpdatesAndBudgets()
    {
        await using var setup = await Fixture.Create();
        await setup.Ledger.SaveAsync(new TransactionInput { CategoryId = setup.CategoryId, Amount = 10, Date = setup.Month });
        await setup.Ledger.SaveAsync(new BudgetInput { CategoryId = setup.CategoryId, Amount = 20 }, setup.Month);
        var snapshot = await setup.Ledger.ReadAsync(setup.Month);
        var bob = new Ledger(setup.Factory, new UserState("bob"));
        await bob.SaveAsync(new CategoryInput { Name = "Bob food" });
        var bobCategory = Assert.Single((await bob.ReadAsync(setup.Month)).Categories);
        await Assert.ThrowsAsync<LedgerRuleException>(() => bob.SaveAsync(new TransactionInput {
            Id = snapshot.Transactions[0].Id, CategoryId = bobCategory.Id, Amount = 99, Date = setup.Month }));
        await Assert.ThrowsAsync<LedgerRuleException>(() => bob.SaveAsync(new BudgetInput {
            Id = snapshot.Budgets[0].Id, CategoryId = bobCategory.Id, Amount = 99 }, setup.Month));
        await Assert.ThrowsAsync<LedgerRuleException>(() => bob.SaveAsync(new CategoryInput {
            Id = setup.CategoryId, Name = "Changed" }));
        await bob.DeleteAsync("budget", snapshot.Budgets[0].Id);
        await bob.DeleteAsync("category", setup.CategoryId);
        Assert.Single((await setup.Ledger.ReadAsync(setup.Month)).Budgets);
        Assert.Single((await setup.Ledger.ReadAsync(setup.Month)).Categories);
    }

    [Fact]
    public async Task DuplicateBudgetAndIncomeBudgetAreRejected()
    {
        await using var setup = await Fixture.Create();
        var budget = new BudgetInput { CategoryId = setup.CategoryId, Amount = 10 };
        await setup.Ledger.SaveAsync(budget, setup.Month);
        await Assert.ThrowsAsync<LedgerRuleException>(() => setup.Ledger.SaveAsync(budget, setup.Month));
        await setup.Ledger.SaveAsync(new CategoryInput { Name = "Salary", Flow = Flow.Income });
        var income = (await setup.Ledger.ReadAsync(setup.Month)).Categories.Single(c => c.Flow == Flow.Income);
        await Assert.ThrowsAsync<LedgerRuleException>(() => setup.Ledger.SaveAsync(
            new BudgetInput { CategoryId = income.Id, Amount = 10 }, setup.Month));
    }

    [Theory]
    [InlineData(1999, 1, 1)]
    [InlineData(2100, 1, 1)]
    [InlineData(2026, 11, 2)]
    public async Task InvalidMonthsAreRejected(int year, int month, int day)
    {
        await using var setup = await Fixture.Create();
        var date = new DateOnly(year, month, day);
        await Assert.ThrowsAsync<LedgerRuleException>(() => setup.Ledger.ReadAsync(date));
        await Assert.ThrowsAsync<LedgerRuleException>(() => setup.Ledger.SaveAsync(
            new BudgetInput { CategoryId = setup.CategoryId, Amount = 10 }, date));
    }

    [Fact]
    public async Task DatabaseRejectsCrossOwnerCategoryReferences()
    {
        await using var setup = await Fixture.Create();
        await using var db = setup.Factory.CreateDbContext();
        db.Transactions.Add(new Transaction { Owner = "bob", CategoryId = setup.CategoryId, Cents = 100, Date = setup.Month });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task MonthlyReadHandlesTenThousandRows()
    {
        await using var setup = await Fixture.Create();
        await using (var db = setup.Factory.CreateDbContext())
        {
            db.Transactions.AddRange(Enumerable.Range(0, 10000).Select(i => new Transaction {
                Owner = "alice", CategoryId = setup.CategoryId, Cents = 100, Date = setup.Month.AddDays(i % 28) }));
            await db.SaveChangesAsync();
        }
        var snapshot = await setup.Ledger.ReadAsync(setup.Month);
        Assert.Equal(10000, snapshot.Transactions.Length);
        Assert.Equal(1000000, Finance.Summarize(snapshot.Entries).Expenses);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("  +SUM(A1:A2)")]
    [InlineData("@SUM(A1:A2)")]
    [InlineData("-1+2")]
    public void CsvNeutralizesSpreadsheetFormulas(string note)
    {
        var bytes = CsvExport.Create([new Transaction {
            Category = new Category { Name = "Food", Flow = Flow.Expense },
            Date = new(2026, 11, 1), Cents = 1234, Note = note }]);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"'" + note + "\"", text);
        Assert.Contains(",12.34,", text);
    }

    [Fact]
    public void CsvEscapesQuotesCommasAndLineBreaks()
    {
        var text = System.Text.Encoding.UTF8.GetString(CsvExport.Create([new Transaction {
            Category = new Category { Name = "Food, home" },
            Date = new(2026, 11, 1), Note = "A \"quote\"\nnext line" }]));
        Assert.Contains("\"Food, home\"", text);
        Assert.Contains("\"A \"\"quote\"\"\nnext line\"", text);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
        public Factory Factory { get; private set; } = null!;
        public Ledger Ledger { get; private set; } = null!;
        public int CategoryId { get; private set; }
        public DateOnly Month { get; } = new(2026, 11, 1);
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            await fixture.connection.OpenAsync();
            fixture.Factory = new Factory(new DbContextOptionsBuilder<FinanceDb>().UseSqlite(fixture.connection).Options);
            await using var db = fixture.Factory.CreateDbContext();
            await db.Database.EnsureCreatedAsync();
            fixture.Ledger = new Ledger(fixture.Factory, new UserState("alice"));
            await fixture.Ledger.SaveAsync(new CategoryInput { Name = "Food" });
            fixture.CategoryId = (await fixture.Ledger.ReadAsync(fixture.Month)).Categories[0].Id;
            return fixture;
        }
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private sealed class Factory(DbContextOptions<FinanceDb> options) : IDbContextFactory<FinanceDb>
    {
        public FinanceDb CreateDbContext() => new(options);
    }

    private sealed class UserState(string user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user)], "test"))));
    }
}
