using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public static string CreateConnectionString(string databaseFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseFilePath);

        return new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            DefaultTimeout = 5,
        }.ToString();
    }

    public static void Configure(
        DbContextOptionsBuilder options,
        string databaseFilePath) => options
        .UseSqlite(CreateConnectionString(databaseFilePath))
        .AddInterceptors(SqliteDurabilityInterceptor.Instance);
}
