using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain;

public enum Flow { Expense, Income }

public sealed record Entry(int CategoryId, string Category, Flow Flow, DateOnly Date, long Cents);
public sealed record Totals(long Income, long Expenses)
{
    public long Balance => Income - Expenses;
}

public static class Finance
{
    public static Totals Summarize(IEnumerable<Entry> entries) =>
        entries.Aggregate(new Totals(0, 0), (total, entry) => entry.Flow == Flow.Income
            ? total with { Income = total.Income + entry.Cents }
            : total with { Expenses = total.Expenses + entry.Cents });

    public static Dictionary<int, long> Spending(IEnumerable<Entry> entries) =>
        entries.Where(e => e.Flow == Flow.Expense).GroupBy(e => e.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Cents));

    public static decimal Forecast(long spent, int elapsedDays, int daysInMonth) =>
        elapsedDays > 0 ? (decimal)spent * daysInMonth / elapsedDays : 0;

    public static string Money(long cents) => $"{cents / 100m:N2} ₸";
    public static long ToCents(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero));
}

public sealed class TransactionInput
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Range(typeof(decimal), "0.01", "1000000000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter an amount between 0.01 and 1,000,000,000.")]
    public decimal Amount { get; set; }
    [StringLength(160)]
    public string Note { get; set; } = "";
}

public sealed class CategoryInput
{
    public int Id { get; set; }
    [Required, StringLength(60)]
    public string Name { get; set; } = "";
    [EnumDataType(typeof(Flow))]
    public Flow Flow { get; set; }
}

public sealed class BudgetInput
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose an expense category.")]
    public int CategoryId { get; set; }
    [Range(typeof(decimal), "0.01", "1000000000", ParseLimitsInInvariantCulture = true)]
    public decimal Amount { get; set; }
}
public sealed class LedgerRuleException(string? message) : InvalidOperationException(message);

