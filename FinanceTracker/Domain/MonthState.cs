namespace FinanceTracker.Domain;

public sealed class MonthState
{
    private static DateOnly CurrentMonth() => DateOnly.FromDateTime(DateTime.Today.AddDays(1 - DateTime.Today.Day));
    public DateOnly Month { get; private set; } = CurrentMonth();
    public void Move(int offset) => Month = Month.AddMonths(offset);
}

