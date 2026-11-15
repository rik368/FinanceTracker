using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Components.Pages;
using FinanceTracker.Data;
using FinanceTracker.Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace FinanceTracker.Tests;

public sealed class ComponentTests
{
    [Fact]
    public async Task CategoryBudgetAndTransactionCrudWorksThroughForms()
    {
        await using var context = new Harness();
        var page = context.Open();
        Assert.Contains("Start with a category", page.Markup);
        Navigate(page, "Categories");
        Button(page, "Add category").Click();
        page.Find("form input").Change("Food");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Category saved.", page.Markup));
        Assert.Contains("Food", page.Markup);
        Button(page, "Edit").Click();
        page.Find("form input").Change("Groceries");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Groceries", page.Markup));
        Navigate(page, "Budgets");
        Button(page, "Add budget").Click();
        page.Find("form select").Change("1");
        page.Find("form input").Change("100");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Budget saved.", page.Markup));
        Button(page, "Edit").Click();
        page.Find("form input").Change("50");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("50.00", page.Markup));

        Navigate(page, "Transactions");
        Button(page, "Add transaction").Click();
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Choose a category.", page.Markup));
        page.Find("form select").Change("1");
        page.Find("form input[type=number]").Change("60");
        page.Find("form input[maxlength]").Change("Weekly shopping");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Transaction saved.", page.Markup));
        Assert.Contains("Weekly shopping", page.Markup);
        Navigate(page, "Budgets");
        Assert.Contains("Over budget by", page.Markup);
        Assert.Contains("10.00", page.Markup);
        Navigate(page, "Transactions");
        Button(page, "Edit").Click();
        page.Find("form input[type=number]").Change("20");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("20.00", page.Markup));

        Navigate(page, "Categories");
        Button(page, "Delete").Click();
        page.Find("[role=group][aria-label='Confirm deletion'] button").Click();
        page.WaitForAssertion(() => Assert.Contains("Move or delete the related", page.Markup));
        Button(page, "Cancel").Click();
        Navigate(page, "Transactions");
        DeleteOnlyRow(page);
        Navigate(page, "Budgets");
        DeleteOnlyRow(page);
        Navigate(page, "Categories");
        DeleteOnlyRow(page);
        Assert.Contains("No categories yet.", page.Markup);
    }

    [Fact]
    public async Task SearchCategoryAndPaginationComposeAndReset()
    {
        await using var context = new Harness();
        using (var db = context.Factory.CreateDbContext())
        {
            var category = new Category { Owner = "component-user", Name = "Food", Key = "FOOD" };
            db.Transactions.AddRange(Enumerable.Range(1, 45).Select(i => new Transaction {
                Owner = "component-user", Category = category, Date = context.Month, Cents = i * 100, Note = $"Item {i}" }));
            db.Categories.Add(new Category { Owner = "component-user", Name = "Other", Key = "OTHER" });
            db.SaveChanges();
        }
        var page = context.Open();
        Navigate(page, "Transactions");
        Assert.Equal(20, page.FindAll(".items .item").Count);
        Assert.Contains("Page 1 of 3", page.Markup);
        Button(page, "Next").Click();
        Assert.Contains("Page 2 of 3", page.Markup);
        Button(page, "Next").Click();
        Assert.Equal(5, page.FindAll(".items .item").Count);
        Button(page, "Previous").Click();
        page.Find("input[type=search]").Input("item 45");
        Assert.Single(page.FindAll(".items .item"));
        page.Find(".toolbar select").Change("2");
        Assert.Empty(page.FindAll(".items .item"));
        Assert.Contains("No transactions match", page.Markup);
        page.Find(".toolbar select").Change("0");
        page.Find("input[type=search]").Input("");
        Assert.Contains("Page 1 of 3", page.Markup);
        Assert.Equal(20, page.FindAll(".items .item").Count);
    }

    [Fact]
    public async Task DemoDashboardForecastAndMonthNavigationWork()
    {
        await using var context = new Harness();
        var page = context.Open();
        Button(page, "Load sample data").Click();
        page.WaitForAssertion(() => Assert.Contains("Sample data loaded.", page.Markup));
        Assert.Contains("320,000.00", page.Markup);
        Assert.Equal(4, page.FindAll(".gap .item").Count);
        Navigate(page, "Analytics");
        Assert.Contains("The projection extends", page.Markup);
        page.Find("button[aria-label='Previous month']").Click();
        page.WaitForAssertion(() => Assert.Contains("The forecast is available for the current month.", page.Markup));
        Navigate(page, "Transactions");
        Assert.Contains("No transactions match", page.Markup);
        page.Find("button[aria-label='Next month']").Click();
        page.WaitForAssertion(() => Assert.Equal(6, page.FindAll(".items .item").Count));
        Button(page, "Add transaction").Click();
        Assert.True(page.Find("button[aria-label='Next month']").HasAttribute("disabled"));
        Assert.All(page.FindAll("nav.tabs button"), x => Assert.True(x.HasAttribute("disabled")));
        Button(page, "Cancel").Click();
        Assert.False(page.Find("button[aria-label='Next month']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task InitialDatabaseFailureCanBeRetried()
    {
        await using var context = new Harness();
        context.Factory.FailNext = true;
        var page = context.Render<FinanceWorkspace>();
        page.WaitForAssertion(() => Assert.Contains("Something went wrong", page.Markup));
        Button(page, "Try again").Click();
        page.WaitForAssertion(() => Assert.Contains("Start with a category", page.Markup));
    }

    [Fact]
    public async Task RefreshFailureDoesNotRepeatSuccessfulWrite()
    {
        await using var context = new Harness();
        var page = context.Open();
        Navigate(page, "Categories");
        Button(page, "Add category").Click();
        page.Find("form input").Change("Travel");
        context.Factory.FailOnCall = context.Factory.Calls + 2;
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Your changes were saved", page.Markup));
        Button(page, "Try again").Click();
        page.WaitForAssertion(() => Assert.Contains("Travel", page.Markup));
        Assert.Single(page.FindAll(".items .item"));
    }

    [Fact]
    public async Task DuplicateCategoryShowsRuleErrorAndAllowsCorrection()
    {
        await using var context = new Harness();
        var page = context.Open();
        Navigate(page, "Categories");
        Button(page, "Add category").Click();
        page.Find("form input").Change("Travel");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Category saved.", page.Markup));
        Button(page, "Add category").Click();
        page.Find("form input").Change("travel");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("This category already exists.", page.Markup));
        page.Find("form input").Change("Food");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".items .item").Count));
    }

    private static void Navigate(IRenderedComponent<FinanceWorkspace> page, string title) =>
        page.FindAll("nav.tabs button").Single(x => x.TextContent.Trim() == title).Click();

    private static IElement Button(IRenderedComponent<FinanceWorkspace> page, string label) =>
        page.FindAll("button").Single(x => x.TextContent.Trim() == label);

    private static void DeleteOnlyRow(IRenderedComponent<FinanceWorkspace> page)
    {
        Button(page, "Delete").Click();
        page.Find("[aria-label='Confirm deletion'] button").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".items .item")));
    }

    private sealed class Harness : BunitContext
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
        public TestFactory Factory { get; }
        public DateOnly Month { get; }
        public Harness()
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
            connection.Open();
            Factory = new TestFactory(new DbContextOptionsBuilder<FinanceDb>().UseSqlite(connection).Options);
            using var db = Factory.CreateDbContext();
            db.Database.EnsureCreated();
            var state = new MonthState();
            Month = state.Month;
            Services.AddSingleton<IDbContextFactory<FinanceDb>>(Factory);
            Services.AddSingleton<AuthenticationStateProvider>(new TestUser());
            Services.AddScoped<Ledger>();
            Services.AddScoped<Demo>();
            Services.AddSingleton(state);
        }
        public IRenderedComponent<FinanceWorkspace> Open()
        {
            var page = Render<FinanceWorkspace>();
            page.WaitForAssertion(() => Assert.Contains("Monthly balance", page.Markup));
            return page;
        }
        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestFactory(DbContextOptions<FinanceDb> options) : IDbContextFactory<FinanceDb>
    {
        public int Calls { get; private set; }
        public int FailOnCall { get; set; } = -1;
        public bool FailNext { get; set; }
        public FinanceDb CreateDbContext()
        {
            Calls++;
            if (FailNext || Calls == FailOnCall)
            {
                FailNext = false;
                throw new IOException("Simulated temporary database outage");
            }
            return new(options);
        }
    }
    private sealed class TestUser : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "component-user")], "component-test"))));
    }
}


