using FinanceTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Domain;

public sealed class Demo(IDbContextFactory<FinanceDb> factory,
    Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authentication)
{
    public async Task PopulateAsync(DateOnly month)
    {
        var state = await authentication.GetAuthenticationStateAsync();
        var owner = state.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new LedgerRuleException("Please sign in.");
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Categories.AnyAsync(x => x.Owner == owner))
            throw new LedgerRuleException("Sample data is available when your category list is empty.");
        var salary = Category("Salary", Flow.Income);
        var food = Category("Groceries", Flow.Expense);
        var travel = Category("Transport", Flow.Expense);
        var home = Category("Housing", Flow.Expense);
        db.Transactions.AddRange(
            Entry(salary, 0, 320000, "Salary"),
            Entry(home, 1, 110000, "Rent"),
            Entry(food, 3, 19000, "Supermarket"),
            Entry(travel, 5, 8500, "Transit pass"),
            Entry(food, 10, 16000, "Weekly groceries"),
            Entry(travel, 12, 6500, "Taxi"));
        db.Budgets.AddRange(Limit(food, 65000), Limit(travel, 18000), Limit(home, 115000));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        Category Category(string name, Flow flow) =>
            new() { Owner = owner, Name = name, Key = name.ToUpperInvariant(), Flow = flow };
        Transaction Entry(Category category, int day, decimal amount, string note) =>
            new() { Owner = owner, Category = category, Date = month.AddDays(day), Cents = Finance.ToCents(amount), Note = note };
        Budget Limit(Category category, decimal amount) =>
            new() { Owner = owner, Category = category, Month = month, Cents = Finance.ToCents(amount) };
    }
}

