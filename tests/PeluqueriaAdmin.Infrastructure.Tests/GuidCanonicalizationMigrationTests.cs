using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class GuidCanonicalizationMigrationTests
{
    [Fact]
    public async Task EfCore_PersistsGuidAsUppercaseHyphenatedText()
    {
        string temporaryRoot = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            DateTime utcNow = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
            LocalUsePerson person = LocalUsePerson.Create(
                "Formato GUID EF",
                new DateOnly(2026, 8, 2),
                null,
                utcNow);
            Chair chair = Chair.Create(
                "Relación GUID EF",
                new DateOnly(2026, 8, 2),
                null,
                utcNow);
            chair.Assign(person.Id, utcNow);

            await using PeluqueriaDbContext context =
                await factory.CreateDbContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
            context.AddRange(person, chair);
            await context.SaveChangesAsync(cancellationToken);
            await context.Database.OpenConnectionAsync(cancellationToken);

            string storedId = await ReadScalarAsync(
                context,
                """SELECT "Id" FROM "LocalUsePeople" LIMIT 1;""",
                cancellationToken);
            string storedForeignKey = await ReadScalarAsync(
                context,
                """SELECT "AssignedPersonId" FROM "Chairs" LIMIT 1;""",
                cancellationToken);

            Assert.Equal(36, storedId.Length);
            Assert.Equal(person.Id.ToString("D").ToUpperInvariant(), storedId);
            Assert.Equal(4, storedId.Count(character => character == '-'));
            Assert.True(Guid.TryParseExact(storedId, "D", out Guid parsed));
            Assert.Equal(person.Id, parsed);
            Assert.Equal(storedId, storedForeignKey);

            await using var foreignKeyCommand =
                context.Database.GetDbConnection().CreateCommand();
            foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";
            await using var violations =
                await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);
            Assert.False(await violations.ReadAsync(cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Phase523_PreservesDataAndAllowsIdempotentScheduledCharges(
        bool useLegacyFormat)
    {
        string temporaryRoot = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths =
                ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory =
                new TestDbContextFactory(paths.DatabaseFilePath);

            DateTime utcNow =
                new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
            DateOnly startDate = new(2026, 7, 28);
            DateOnly throughDate = new(2026, 7, 30);

            GeneralSettings settings =
                GeneralSettings.CreateDefault(utcNow);
            settings.Update(
                Money.FromDecimal(10m),
                isDailyUsageFeeConfirmed: true,
                Percentage.FromPercent(20m),
                totalChairs: 1,
                exportDirectory: string.Empty,
                utcNow);

            LocalUsePerson person = LocalUsePerson.Create(
                "Trabajador migrado",
                startDate,
                null,
                utcNow);
            Chair chair = Chair.Create(
                "Silla migrada",
                startDate,
                null,
                utcNow);
            chair.Assign(person.Id, utcNow);

            ChairAssignmentPeriod assignment =
                ChairAssignmentPeriod.Create(
                    chair.Id,
                    person.Id,
                    startDate,
                    utcNow);
            DailyRate rate = DailyRate.Create(
                startDate,
                utcNow,
                Money.FromDecimal(10m),
                utcNow);
            FinancialEvent rateEvent = FinancialEvent.Create(
                rate.Id,
                utcNow,
                "Tarifa diaria",
                rate.Id,
                "Tarifa diaria inicial de actualización",
                null,
                Money.FromDecimal(10m),
                1_000,
                "Registro que reproduce el formato de la migración alpha.2.");

            var repository = new EfAdministrationRepository(factory);
            var service = new AdministrationService(
                repository,
                new EfSettingsRepository(factory),
                new FixedTimeProvider(new DateTimeOffset(utcNow)));
            IReadOnlyDictionary<string, long> countsBefore;

            await using (PeluqueriaDbContext context =
                await factory.CreateDbContextAsync(cancellationToken))
            {
                string previousMigration =
                    context.Database.GetMigrations().Single(
                        item => item.EndsWith(
                            "_Phase522PendingLegacyDailyRate",
                            StringComparison.Ordinal));

                await context.GetService<IMigrator>().MigrateAsync(
                    previousMigration,
                    cancellationToken);

                context.AddRange(
                    settings,
                    person,
                    chair,
                    assignment,
                    rate,
                    rateEvent);
                await context.SaveChangesAsync(cancellationToken);

                await service.GenerateScheduledRecordsAsync(
                    throughDate,
                    cancellationToken);
                countsBefore = await ReadTableCountsAsync(
                    context,
                    cancellationToken);

                if (useLegacyFormat)
                {
                    string chairLegacy =
                        chair.Id.ToString("D").ToLowerInvariant();
                    string assignmentLegacy =
                        assignment.Id.ToString("N").ToLowerInvariant();
                    string rateLegacy =
                        rate.Id.ToString("N").ToLowerInvariant();
                    string eventLegacy =
                        rateEvent.Id.ToString("D").ToLowerInvariant();

                    await using var transaction =
                        await context.Database.BeginTransactionAsync(
                            cancellationToken);

                    await context.Database.ExecuteSqlRawAsync(
                        "PRAGMA defer_foreign_keys = ON;",
                        cancellationToken);

                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "DailyCharges"
                        SET "Id" = lower(replace("Id", '-', '')),
                            "ChairId" = {chairLegacy},
                            "RateId" = {rateLegacy};
                        """,
                        cancellationToken);

                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "ChairAssignmentPeriods"
                        SET "Id" = {assignmentLegacy},
                            "ChairId" = {chairLegacy}
                        WHERE "Id" = {assignment.Id.ToString("D").ToUpperInvariant()};
                        """,
                        cancellationToken);

                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "Chairs"
                        SET "Id" = {chairLegacy}
                        WHERE "Id" = {chair.Id.ToString("D").ToUpperInvariant()};
                        """,
                        cancellationToken);

                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "FinancialEvents"
                        SET "Id" = {eventLegacy},
                            "OperationId" = {rateLegacy},
                            "EntityId" = {rateLegacy}
                        WHERE "Id" = {rateEvent.Id.ToString("D").ToUpperInvariant()};
                        """,
                        cancellationToken);

                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "DailyRates"
                        SET "Id" = {rateLegacy}
                        WHERE "Id" = {rate.Id.ToString("D").ToUpperInvariant()};
                        """,
                        cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
            }

            await using (PeluqueriaDbContext migrationContext =
                await factory.CreateDbContextAsync(cancellationToken))
            {
                await migrationContext.GetService<IMigrator>()
                    .MigrateAsync(cancellationToken: cancellationToken);
            }

            await service.GenerateScheduledRecordsAsync(
                throughDate,
                cancellationToken);
            await service.GenerateScheduledRecordsAsync(
                throughDate,
                cancellationToken);

            await using PeluqueriaDbContext verification =
                await factory.CreateDbContextAsync(cancellationToken);

            Assert.Equal(
                3,
                await verification.DailyCharges.CountAsync(
                    cancellationToken));
            GeneralSettings preservedSettings =
                await verification.Settings.SingleAsync(cancellationToken);
            DailyRate preservedRate =
                await verification.DailyRates.SingleAsync(cancellationToken);
            FinancialEvent preservedEvent =
                await verification.FinancialEvents.SingleAsync(
                    item => item.Id == rateEvent.Id,
                    cancellationToken);
            ChairAssignmentPeriod preservedAssignment =
                await verification.ChairAssignmentPeriods.SingleAsync(
                    cancellationToken);
            DailyCharge[] preservedCharges =
                await verification.DailyCharges
                    .OrderBy(item => item.ChargeDate)
                    .ToArrayAsync(cancellationToken);

            Assert.Equal(1_000, preservedSettings.DailyUsageFee?.MinorUnits);
            Assert.True(preservedSettings.IsDailyUsageFeeConfirmed);
            Assert.Equal(1_000, preservedRate.Amount?.MinorUnits);
            Assert.Equal(startDate, preservedRate.EffectiveDate);
            Assert.Equal(1_000, preservedEvent.DifferenceMinorUnits);
            Assert.Equal(
                "Registro que reproduce el formato de la migración alpha.2.",
                preservedEvent.Description);
            Assert.Equal(startDate, preservedAssignment.StartDate);
            Assert.All(
                preservedCharges,
                item => Assert.Equal(1_000, item.Amount.MinorUnits));
            Assert.Equal(
                [
                    new DateOnly(2026, 7, 28),
                    new DateOnly(2026, 7, 29),
                    new DateOnly(2026, 7, 30),
                ],
                preservedCharges.Select(item => item.ChargeDate).ToArray());

            await verification.Database.OpenConnectionAsync(
                cancellationToken);
            IReadOnlyDictionary<string, long> countsAfter =
                await ReadTableCountsAsync(
                    verification,
                    cancellationToken);
            Assert.Equal(countsBefore.Count, countsAfter.Count);
            foreach ((string table, long count) in countsBefore)
            {
                Assert.True(countsAfter.TryGetValue(table, out long afterCount));
                Assert.Equal(count, afterCount);
            }

            string storedChairId = await ReadScalarAsync(
                verification,
                """SELECT "Id" FROM "Chairs" LIMIT 1;""",
                cancellationToken);
            string storedAssignmentId = await ReadScalarAsync(
                verification,
                """SELECT "Id" FROM "ChairAssignmentPeriods" LIMIT 1;""",
                cancellationToken);
            string storedRateId = await ReadScalarAsync(
                verification,
                """SELECT "Id" FROM "DailyRates" LIMIT 1;""",
                cancellationToken);
            string storedEventId = await ReadScalarAsync(
                verification,
                """SELECT "Id" FROM "FinancialEvents" ORDER BY "CreatedUtc" LIMIT 1;""",
                cancellationToken);

            Assert.Equal(
                chair.Id.ToString("D").ToUpperInvariant(),
                storedChairId);
            Assert.Equal(
                assignment.Id.ToString("D").ToUpperInvariant(),
                storedAssignmentId);
            Assert.Equal(
                rate.Id.ToString("D").ToUpperInvariant(),
                storedRateId);
            Assert.Equal(
                rateEvent.Id.ToString("D").ToUpperInvariant(),
                storedEventId);
            Assert.Equal(
                0,
                await ReadInt64Async(
                    verification,
                    """
                    SELECT count(*)
                    FROM "DailyCharges"
                    WHERE length("Id") <> 36
                       OR "Id" <> upper("Id")
                       OR "RateId" <> upper("RateId")
                       OR "ChairId" <> upper("ChairId")
                       OR "PersonId" <> upper("PersonId");
                    """,
                    cancellationToken));
            Assert.Equal(
                "ok",
                await ReadScalarAsync(
                    verification,
                    "PRAGMA integrity_check;",
                    cancellationToken));

            await using var foreignKeyCommand =
                verification.Database.GetDbConnection().CreateCommand();
            foreignKeyCommand.CommandText =
                "PRAGMA foreign_key_check;";
            await using var violations =
                await foreignKeyCommand.ExecuteReaderAsync(
                    cancellationToken);
            Assert.False(
                await violations.ReadAsync(cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(
                    temporaryRoot,
                    recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false, 1, "CK_Phase523_AllGuidValuesAreValid")]
    [InlineData(true, 2, "UNIQUE constraint failed")]
    public async Task Phase523_RejectsInvalidOrCollidingGuidsWithoutPartialChanges(
        bool createCollision,
        int expectedRows,
        string expectedError)
    {
        string temporaryRoot = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);

            await using PeluqueriaDbContext context =
                await factory.CreateDbContextAsync(cancellationToken);
            string previousMigration = context.Database.GetMigrations().Single(
                item => item.EndsWith(
                    "_Phase522PendingLegacyDailyRate",
                    StringComparison.Ordinal));
            await context.GetService<IMigrator>().MigrateAsync(
                previousMigration,
                cancellationToken);

            if (createCollision)
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "DailyRates"
                        ("Id", "EffectiveDate", "EffectiveFromUtc", "EffectiveToUtc",
                         "AmountMinorUnits", "CreatedUtc", "UpdatedUtc", "DeletedUtc")
                    VALUES
                        ('00112233445566778899aabbccddeeff', '2026-08-02', 639002736000000000, NULL, 1000, 639002736000000000, 639002736000000000, NULL),
                        ('00112233-4455-6677-8899-AABBCCDDEEFF', '2026-08-03', 639003600000000000, NULL, 1500, 639003600000000000, 639003600000000000, NULL);
                    """,
                    cancellationToken);
            }
            else
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "DailyRates"
                        ("Id", "EffectiveDate", "EffectiveFromUtc", "EffectiveToUtc",
                         "AmountMinorUnits", "CreatedUtc", "UpdatedUtc", "DeletedUtc")
                    VALUES
                        ('not-a-guid', '2026-08-02', 639002736000000000, NULL, 1000, 639002736000000000, 639002736000000000, NULL);
                    """,
                    cancellationToken);
            }

            SqliteException exception = await Assert.ThrowsAsync<SqliteException>(
                () => context.GetService<IMigrator>()
                    .MigrateAsync(cancellationToken: cancellationToken));

            Assert.Contains(expectedError, exception.Message, StringComparison.Ordinal);
            Assert.Equal(
                expectedRows,
                await context.DailyRates.IgnoreQueryFilters().CountAsync(
                    cancellationToken));
            Assert.DoesNotContain(
                await context.Database.GetAppliedMigrationsAsync(cancellationToken),
                item => item.EndsWith(
                    "_Phase523CanonicalizeMigrationGuids",
                    StringComparison.Ordinal));

            await context.Database.OpenConnectionAsync(cancellationToken);
            Assert.Equal(
                "ok",
                await ReadScalarAsync(
                    context,
                    "PRAGMA integrity_check;",
                    cancellationToken));

            await using var foreignKeyCommand =
                context.Database.GetDbConnection().CreateCommand();
            foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";
            await using var violations =
                await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);
            Assert.False(await violations.ReadAsync(cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static async Task<string> ReadScalarAsync(
        PeluqueriaDbContext context,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command =
            context.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        object? result =
            await command.ExecuteScalarAsync(cancellationToken);
        return Assert.IsType<string>(result);
    }

    private static async Task<long> ReadInt64Async(
        PeluqueriaDbContext context,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command =
            context.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Assert.IsType<long>(result);
    }

    private static async Task<IReadOnlyDictionary<string, long>>
        ReadTableCountsAsync(
            PeluqueriaDbContext context,
            CancellationToken cancellationToken)
    {
        if (context.Database.GetDbConnection().State
            != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        var names = new List<string>();
        await using (var namesCommand =
            context.Database.GetDbConnection().CreateCommand())
        {
            namesCommand.CommandText =
                """
                SELECT "name"
                FROM "sqlite_master"
                WHERE "type" = 'table'
                  AND "name" NOT LIKE 'sqlite_%'
                  AND "name" <> '__EFMigrationsHistory'
                ORDER BY "name";
                """;
            await using var reader =
                await namesCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                names.Add(reader.GetString(0));
            }
        }

        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            await using var countCommand =
                context.Database.GetDbConnection().CreateCommand();
            countCommand.CommandText =
                $"SELECT count(*) FROM \"{name.Replace("\"", "\"\"")}\";";
            object? count =
                await countCommand.ExecuteScalarAsync(cancellationToken);
            result.Add(name, Assert.IsType<long>(count));
        }

        return result;
    }

    private sealed class TestDbContextFactory(
        string databaseFilePath)
        : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options =
            CreateOptions(databaseFilePath);

        public PeluqueriaDbContext CreateDbContext() =>
            new(options);

        public Task<PeluqueriaDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        private static DbContextOptions<PeluqueriaDbContext>
            CreateOptions(string databaseFilePath)
        {
            var builder =
                new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(
                builder,
                databaseFilePath);
            return builder.Options;
        }
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }
}
