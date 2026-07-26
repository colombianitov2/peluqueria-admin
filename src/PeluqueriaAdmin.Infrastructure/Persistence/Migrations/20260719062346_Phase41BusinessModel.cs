using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase41BusinessModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "DueDate",
            table: "WeeklyCharges",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.AddColumn<long>(
            name: "DefaultSalePriceMinorUnits",
            table: "Products",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Products",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Obligations",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "ObligationPayments",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "MonthlyCloses",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "MaintenanceRecords",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "LocalUsePeople",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "LocalUsePayments",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "InventoryMovements",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "FinancialEntries",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "DistributionPayments",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Collaborators",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ActivityRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ActivityDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                OccurredUtc = table.Column<long>(type: "INTEGER", nullable: false),
                Module = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityRecords", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Chairs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                CreationDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                AssignedPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Chairs", x => x.Id);
                table.ForeignKey(
                    name: "FK_Chairs_LocalUsePeople_AssignedPersonId",
                    column: x => x.AssignedPersonId,
                    principalTable: "LocalUsePeople",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UnofficialExpenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                MonthlyAmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UnofficialExpenses", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityRecords_Module_ActivityDate",
            table: "ActivityRecords",
            columns: new[] { "Module", "ActivityDate" });

        migrationBuilder.CreateIndex(
            name: "IX_Chairs_AssignedPersonId",
            table: "Chairs",
            column: "AssignedPersonId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UnofficialExpenses_EffectiveFrom",
            table: "UnofficialExpenses",
            column: "EffectiveFrom");

        migrationBuilder.Sql(
            """
            UPDATE WeeklyCharges
            SET PeriodEnd = date(PeriodStart, '+7 days'),
                DueDate = date(
                    PeriodStart,
                    '+7 days',
                    '+' || ((6 - CAST(strftime('%w', date(PeriodStart, '+7 days')) AS INTEGER) + 7) % 7) || ' days');
            """);

        migrationBuilder.Sql(
            """
            WITH RECURSIVE numbers(n) AS (
                SELECT 1
                UNION ALL
                SELECT n + 1 FROM numbers, Settings WHERE Settings.Id = 1 AND n < Settings.TotalChairs
            )
            INSERT INTO Chairs (
                Id, Name, CreationDate, Description, AssignedPersonId,
                CreatedUtc, UpdatedUtc, DeletedUtc)
            SELECT
                lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' ||
                substr(lower(hex(randomblob(2))), 2) || '-' ||
                substr('89ab', abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))), 2) || '-' ||
                lower(hex(randomblob(6))),
                'Silla ' || n,
                date('now', 'localtime'),
                NULL,
                NULL,
                621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                NULL
            FROM numbers, Settings
            WHERE Settings.Id = 1
              AND Settings.TotalChairs > 0
              AND n <= Settings.TotalChairs
              AND NOT EXISTS (SELECT 1 FROM Chairs);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ActivityRecords");

        migrationBuilder.DropTable(
            name: "Chairs");

        migrationBuilder.DropTable(
            name: "UnofficialExpenses");

        migrationBuilder.DropColumn(
            name: "DueDate",
            table: "WeeklyCharges");

        migrationBuilder.DropColumn(
            name: "DefaultSalePriceMinorUnits",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "Obligations");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "ObligationPayments");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "MaintenanceRecords");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "LocalUsePeople");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "LocalUsePayments");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "InventoryMovements");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "FinancialEntries");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "DistributionPayments");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "Collaborators");
    }
}
