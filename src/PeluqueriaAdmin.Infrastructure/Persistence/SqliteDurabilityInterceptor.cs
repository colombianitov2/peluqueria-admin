using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public sealed class SqliteDurabilityInterceptor : DbConnectionInterceptor
{
    public static SqliteDurabilityInterceptor Instance { get; } = new();

    private SqliteDurabilityInterceptor()
    {
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ApplyPolicy(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) => ApplyPolicyAsync(connection, cancellationToken);

    private static void ApplyPolicy(DbConnection connection)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
    }

    private static async Task ApplyPolicyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
