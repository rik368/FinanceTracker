using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Data;

/// <summary>Deletes both SQLite stores in one attached-database transaction.</summary>
public sealed class AccountDeletion(ApplicationDbContext identity, IDbContextFactory<FinanceDb> factory)
{
    public async Task DeleteAsync(ApplicationUser user)
    {
        await using var finance = await factory.CreateDbContextAsync();
        var settings = new SqliteConnectionStringBuilder(identity.Database.GetConnectionString())
        {
            ForeignKeys = true,
            Pooling = false
        };
        await using var connection = new SqliteConnection(settings.ToString());
        await connection.OpenAsync();
        await using var attach = connection.CreateCommand();
        attach.CommandText = "ATTACH DATABASE $path AS ledger";
        attach.Parameters.AddWithValue("$path", new SqliteConnectionStringBuilder(finance.Database.GetConnectionString()).DataSource);
        await attach.ExecuteNonQueryAsync();

        // Rollback journals are required for crash-atomic commits across attached files.
        foreach (var schema in new[] { "main", "ledger" })
        {
            await using var mode = connection.CreateCommand();
            mode.CommandText = $"PRAGMA {schema}.journal_mode";
            if (!string.Equals((string?)await mode.ExecuteScalarAsync(), "delete", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Account deletion requires DELETE journal mode.");
        }

        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$owner", user.Id);
        command.Parameters.AddWithValue("$stamp", (object?)user.ConcurrencyStamp ?? DBNull.Value);
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ledger.DeletedAccounts (Owner TEXT PRIMARY KEY NOT NULL);
            CREATE TRIGGER IF NOT EXISTS ledger.PreventDeletedAccountInsert
            BEFORE INSERT ON Categories
            WHEN EXISTS (SELECT 1 FROM DeletedAccounts WHERE Owner = NEW.Owner)
            BEGIN SELECT RAISE(ABORT, 'Account has been deleted.'); END;
            CREATE TRIGGER IF NOT EXISTS ledger.PreventDeletedAccountUpdate
            BEFORE UPDATE ON Categories
            WHEN EXISTS (SELECT 1 FROM DeletedAccounts WHERE Owner = NEW.Owner)
            BEGIN SELECT RAISE(ABORT, 'Account has been deleted.'); END;
            INSERT OR IGNORE INTO ledger.DeletedAccounts (Owner) VALUES ($owner);
            DELETE FROM ledger.Transactions WHERE Owner = $owner;
            DELETE FROM ledger.Budgets WHERE Owner = $owner;
            DELETE FROM ledger.Categories WHERE Owner = $owner;
            """;
        await command.ExecuteNonQueryAsync();
        // Identity's foreign keys cascade to claims, logins, tokens, roles and passkeys.
        command.CommandText = "DELETE FROM main.AspNetUsers WHERE Id = $owner AND ConcurrencyStamp IS $stamp";
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new DbUpdateConcurrencyException("The account changed. Reload and try again.");
        await transaction.CommitAsync();
        identity.ChangeTracker.Clear();
    }
}
