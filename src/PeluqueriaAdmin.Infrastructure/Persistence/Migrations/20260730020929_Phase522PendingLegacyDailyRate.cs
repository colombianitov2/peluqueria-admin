using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase522PendingLegacyDailyRate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Settings_WeeklyUsageFeeMinorUnits",
            table: "Settings");

        migrationBuilder.AlterColumn<long>(
            name: "WeeklyUsageFeeMinorUnits",
            table: "Settings",
            type: "INTEGER",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "INTEGER");

        migrationBuilder.AddColumn<bool>(
            name: "IsDailyUsageFeeConfirmed",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AlterColumn<long>(
            name: "AmountMinorUnits",
            table: "DailyRates",
            type: "INTEGER",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "INTEGER");

        migrationBuilder.Sql(
            """
            UPDATE Settings
            SET IsDailyUsageFeeConfirmed =
                CASE
                    WHEN WeeklyUsageFeeMinorUnits IS NULL THEN 0
                    WHEN WeeklyUsageFeeMinorUnits <> 1200 THEN 1
                    WHEN EXISTS (
                        SELECT 1
                        FROM FinancialEvents
                        WHERE EntityType = 'Tarifa diaria'
                          AND EventType <> 'Tarifa diaria inicial de actualización'
                          AND DeletedUtc IS NULL
                    ) THEN 1
                    ELSE 0
                END;

            UPDATE DailyRates
            SET EffectiveToUtc = COALESCE(
                    EffectiveToUtc,
                    621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000),
                UpdatedUtc =
                    621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                DeletedUtc =
                    621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000
            WHERE AmountMinorUnits = 1200
              AND EXISTS (
                  SELECT 1
                  FROM Settings
                  WHERE IsDailyUsageFeeConfirmed = 0
                    AND WeeklyUsageFeeMinorUnits = 1200
              )
              AND EXISTS (
                  SELECT 1
                  FROM FinancialEvents
                  WHERE FinancialEvents.EntityId = DailyRates.Id
                    AND FinancialEvents.EntityType = 'Tarifa diaria'
                    AND FinancialEvents.EventType = 'Tarifa diaria inicial de actualización'
              );

            UPDATE FinancialEvents
            SET UpdatedUtc =
                    621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                DeletedUtc =
                    621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                State = 'Valor heredado pendiente de confirmación'
            WHERE EntityType = 'Tarifa diaria'
              AND EventType = 'Tarifa diaria inicial de actualización'
              AND EXISTS (
                  SELECT 1
                  FROM Settings
                  WHERE IsDailyUsageFeeConfirmed = 0
                    AND WeeklyUsageFeeMinorUnits = 1200
              );
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Settings_WeeklyUsageFeeMinorUnits",
            table: "Settings",
            sql: "WeeklyUsageFeeMinorUnits IS NULL OR WeeklyUsageFeeMinorUnits >= 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Settings_WeeklyUsageFeeMinorUnits",
            table: "Settings");

        migrationBuilder.DropColumn(
            name: "IsDailyUsageFeeConfirmed",
            table: "Settings");

        migrationBuilder.AlterColumn<long>(
            name: "WeeklyUsageFeeMinorUnits",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(long),
            oldType: "INTEGER",
            oldNullable: true);

        migrationBuilder.AlterColumn<long>(
            name: "AmountMinorUnits",
            table: "DailyRates",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(long),
            oldType: "INTEGER",
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Settings_WeeklyUsageFeeMinorUnits",
            table: "Settings",
            sql: "WeeklyUsageFeeMinorUnits >= 0");
    }
}
