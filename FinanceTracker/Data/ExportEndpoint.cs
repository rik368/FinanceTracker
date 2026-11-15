using System.Globalization;
using System.Security.Claims;
using FinanceTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Data;

public static class ExportEndpoint
{
    public static void MapFinanceExport(this WebApplication app)
    {
        app.MapGet("/exports/transactions", async (string? month, ClaimsPrincipal user,
            IDbContextFactory<FinanceDb> factory, HttpContext http) =>
        {
            if (!DateOnly.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start) || start.Year is < 2000 or > 2099)
                return Results.BadRequest("Choose a month between January 2000 and December 2099.");
            var owner = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (owner is null) return Results.Unauthorized();
            await using var db = await factory.CreateDbContextAsync(http.RequestAborted);
            var entries = await db.Transactions.AsNoTracking().Include(x => x.Category)
                .Where(x => x.Owner == owner && x.Date >= start && x.Date < start.AddMonths(1))
                .OrderBy(x => x.Date).ThenBy(x => x.Id).ToArrayAsync(http.RequestAborted);
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(CsvExport.Create(entries), "text/csv; charset=utf-8",
                $"transactions-{start:yyyy-MM}.csv");
        }).RequireAuthorization();
    }
}
