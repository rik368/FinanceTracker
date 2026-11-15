using FinanceTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Data;

public sealed class Category
{
    public int Id { get; set; }
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public Flow Flow { get; set; }
}

public sealed class Transaction
{
    public int Id { get; set; }
    public string Owner { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateOnly Date { get; set; }
    public long Cents { get; set; }
    public string Note { get; set; } = "";
}

public sealed class Budget
{
    public int Id { get; set; }
    public string Owner { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateOnly Month { get; set; }
    public long Cents { get; set; }
}

public sealed class FinanceDb(DbContextOptions<FinanceDb> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Category>().HasIndex(x => new { x.Owner, x.Flow, x.Key }).IsUnique();
        model.Entity<Transaction>().HasIndex(x => new { x.Owner, x.Date });
        model.Entity<Budget>().HasIndex(x => new { x.Owner, x.Month, x.CategoryId }).IsUnique();
        model.Entity<Category>().HasAlternateKey(x => new { x.Owner, x.Id });
        model.Entity<Transaction>().HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => new { x.Owner, x.CategoryId }).HasPrincipalKey(x => new { x.Owner, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<Budget>().HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => new { x.Owner, x.CategoryId }).HasPrincipalKey(x => new { x.Owner, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
