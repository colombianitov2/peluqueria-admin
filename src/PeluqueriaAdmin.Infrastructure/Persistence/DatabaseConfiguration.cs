using Microsoft.Data.Sqlite;

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
        }.ToString();
    }
}
