using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FinanceTracker.Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Data;

public sealed record LedgerMonth(Category[] Categories, Transaction[] Transactions, Budget[] Budgets)
{
    public IEnumerable<Entry> Entries => Transactions.Select(t =>
        new Entry(t.CategoryId, t.Category.Name, t.Category.Flow, t.Date, t.Cents));
}

public sealed class Ledger(IDbContextFactory<FinanceDb> factory, AuthenticationStateProvider authentication)
{
    private async Task<string> OwnerAsync() =>
        (await authentication.GetAuthenticationStateAsync()).User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new LedgerRuleException("Please sign in.");

    public async Task<LedgerMonth> ReadAsync(DateOnly month)
    {
        ValidateMonth(month);
        var owner = await OwnerAsync();
        await using var db = await factory.CreateDbContextAsync();
        return new LedgerMonth(
            await db.Categories.AsNoTracking().Where(x => x.Owner == owner).OrderBy(x => x.Name).ToArrayAsync(),
            await db.Transactions.AsNoTracking().Include(x => x.Category)
                .Where(x => x.Owner == owner && x.Date >= month && x.Date < month.AddMonths(1))
                .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToArrayAsync(),
            await db.Budgets.AsNoTracking().Include(x => x.Category)
                .Where(x => x.Owner == owner && x.Month == month).OrderBy(x => x.Category.Name).ToArrayAsync());
    }

    public async Task SaveAsync(CategoryInput input)
    {
        Validate(input);
        var owner = await OwnerAsync();
        await using var db = await factory.CreateDbContextAsync();
        await using var write = await db.Database.BeginTransactionAsync();
        var category = input.Id == 0 ? new Category { Owner = owner, Flow = input.Flow } :
            await db.Categories.SingleOrDefaultAsync(x => x.Id == input.Id && x.Owner == owner)
            ?? throw new LedgerRuleException("Category not found.");
        if (category.Flow != input.Flow)
            throw new LedgerRuleException("The type of an existing category cannot be changed.");
        category.Name = input.Name.Trim();
        category.Key = category.Name.ToUpperInvariant();
        if (await db.Categories.AnyAsync(x => x.Owner == owner && x.Flow == category.Flow &&
            x.Key == category.Key && x.Id != category.Id))
            throw new LedgerRuleException("This category already exists.");
        if (input.Id == 0) db.Categories.Add(category);
        await db.SaveChangesAsync();
        await write.CommitAsync();
    }

    public async Task SaveAsync(TransactionInput input)
    {
        Validate(input);
        if (input.Date.Year is < 2000 or > 2099)
            throw new LedgerRuleException("Choose a date between 2000 and 2099.");
        var owner = await OwnerAsync();
        await using var db = await factory.CreateDbContextAsync();
        await using var write = await db.Database.BeginTransactionAsync();
        if (!await db.Categories.AnyAsync(x => x.Id == input.CategoryId && x.Owner == owner))
            throw new LedgerRuleException("Choose a category from your account.");
        var transaction = input.Id == 0 ? new Transaction { Owner = owner } :
            await db.Transactions.SingleOrDefaultAsync(x => x.Id == input.Id && x.Owner == owner)
            ?? throw new LedgerRuleException("Transaction not found.");
        transaction.CategoryId = input.CategoryId;
        transaction.Date = input.Date;
        transaction.Cents = Finance.ToCents(input.Amount);
        transaction.Note = input.Note?.Trim() ?? "";
        if (input.Id == 0) db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        await write.CommitAsync();
    }

    public async Task SaveAsync(BudgetInput input, DateOnly month)
    {
        ValidateMonth(month);
        Validate(input);
        var owner = await OwnerAsync();
        await using var db = await factory.CreateDbContextAsync();
        await using var write = await db.Database.BeginTransactionAsync();
        if (!await db.Categories.AnyAsync(x => x.Id == input.CategoryId && x.Owner == owner && x.Flow == Flow.Expense))
            throw new LedgerRuleException("Choose an expense category.");
        var budget = input.Id == 0 ? new Budget { Owner = owner, Month = month } :
            await db.Budgets.SingleOrDefaultAsync(x => x.Id == input.Id && x.Owner == owner && x.Month == month)
            ?? throw new LedgerRuleException("Budget not found.");
        if (await db.Budgets.AnyAsync(x => x.Owner == owner && x.Month == month &&
            x.CategoryId == input.CategoryId && x.Id != input.Id))
            throw new LedgerRuleException("This category already has a budget for the selected month.");
        budget.CategoryId = input.CategoryId;
        budget.Cents = Finance.ToCents(input.Amount);
        if (input.Id == 0) db.Budgets.Add(budget);
        await db.SaveChangesAsync();
        await write.CommitAsync();
    }

    public async Task DeleteAsync(string kind, int id)
    {
        var owner = await OwnerAsync();
        await using var db = await factory.CreateDbContextAsync();
        switch (kind)
        {
            case "category":
                if (await db.Transactions.AnyAsync(x => x.Owner == owner && x.CategoryId == id) ||
                    await db.Budgets.AnyAsync(x => x.Owner == owner && x.CategoryId == id))
                    throw new LedgerRuleException("Move or delete the related transactions and budgets first.");
                await db.Categories.Where(x => x.Owner == owner && x.Id == id).ExecuteDeleteAsync();
                break;
            case "transaction":
                await db.Transactions.Where(x => x.Owner == owner && x.Id == id).ExecuteDeleteAsync();
                break;
            case "budget":
                await db.Budgets.Where(x => x.Owner == owner && x.Id == id).ExecuteDeleteAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void ValidateMonth(DateOnly month)
    {
        if (month.Day != 1 || month.Year is < 2000 or > 2099)
            throw new LedgerRuleException("Choose a calendar month between 2000 and 2099.");
    }

    private static void Validate(object input)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
            throw new LedgerRuleException(errors[0].ErrorMessage);
    }
}

