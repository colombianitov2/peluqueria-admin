using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase52DailyFeesContributionsAndBranding : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChairAssignmentPeriods",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ChairId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                EndDateExclusive = table.Column<DateOnly>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChairAssignmentPeriods", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChairAssignmentPeriods_Chairs_ChairId",
                    column: x => x.ChairId,
                    principalTable: "Chairs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChairAssignmentPeriods_LocalUsePeople_PersonId",
                    column: x => x.PersonId,
                    principalTable: "LocalUsePeople",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DailyRates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                EffectiveFromUtc = table.Column<long>(type: "INTEGER", nullable: false),
                EffectiveToUtc = table.Column<long>(type: "INTEGER", nullable: true),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyRates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FinancialEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurredUtc = table.Column<long>(type: "INTEGER", nullable: false),
                EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                EventType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                PreviousValueMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                NewValueMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                DifferenceMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                State = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialEvents", x => x.Id);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO DailyRates
                (Id, EffectiveDate, EffectiveFromUtc, EffectiveToUtc, AmountMinorUnits,
                 CreatedUtc, UpdatedUtc, DeletedUtc)
            SELECT
                lower(hex(randomblob(16))),
                date('now', 'localtime'),
                621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                NULL,
                WeeklyUsageFeeMinorUnits,
                621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                NULL
            FROM Settings
            WHERE NOT EXISTS (SELECT 1 FROM DailyRates);

            INSERT INTO ChairAssignmentPeriods
                (Id, ChairId, PersonId, StartDate, EndDateExclusive,
                 CreatedUtc, UpdatedUtc, DeletedUtc)
            SELECT
                lower(hex(randomblob(16))),
                Chairs.Id,
                Chairs.AssignedPersonId,
                date('now', 'localtime'),
                NULL,
                621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                621355968000000000 + CAST(strftime('%s', 'now') AS INTEGER) * 10000000,
                NULL
            FROM Chairs
            INNER JOIN LocalUsePeople
                ON LocalUsePeople.Id = Chairs.AssignedPersonId
            WHERE Chairs.AssignedPersonId IS NOT NULL
              AND Chairs.DeletedUtc IS NULL
              AND LocalUsePeople.DeletedUtc IS NULL;

            INSERT INTO FinancialEvents
                (Id, OperationId, OccurredUtc, EntityType, EntityId, EventType,
                 PreviousValueMinorUnits, NewValueMinorUnits, DifferenceMinorUnits,
                 Description, State, CreatedUtc, UpdatedUtc, DeletedUtc)
            SELECT
                lower(hex(randomblob(16))),
                DailyRates.Id,
                DailyRates.CreatedUtc,
                'Tarifa diaria',
                DailyRates.Id,
                'Tarifa diaria inicial de actualización',
                NULL,
                DailyRates.AmountMinorUnits,
                DailyRates.AmountMinorUnits,
                'La base anterior no conservaba una vigencia diaria verificable; la tarifa comienza con esta actualización.',
                'Migrado sin reinterpretar cargos semanales',
                DailyRates.CreatedUtc,
                DailyRates.CreatedUtc,
                NULL
            FROM DailyRates;
            """);

        migrationBuilder.CreateTable(
            name: "DailyCharges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                ChairId = table.Column<Guid>(type: "TEXT", nullable: false),
                RateId = table.Column<Guid>(type: "TEXT", nullable: false),
                ChargeDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyCharges", x => x.Id);
                table.ForeignKey(
                    name: "FK_DailyCharges_Chairs_ChairId",
                    column: x => x.ChairId,
                    principalTable: "Chairs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DailyCharges_DailyRates_RateId",
                    column: x => x.RateId,
                    principalTable: "DailyRates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DailyCharges_LocalUsePeople_PersonId",
                    column: x => x.PersonId,
                    principalTable: "LocalUsePeople",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChairAssignmentPeriods_ChairId_StartDate",
            table: "ChairAssignmentPeriods",
            columns: new[] { "ChairId", "StartDate" });

        migrationBuilder.CreateIndex(
            name: "IX_ChairAssignmentPeriods_PersonId_StartDate",
            table: "ChairAssignmentPeriods",
            columns: new[] { "PersonId", "StartDate" });

        migrationBuilder.CreateIndex(
            name: "IX_DailyCharges_ChairId",
            table: "DailyCharges",
            column: "ChairId");

        migrationBuilder.CreateIndex(
            name: "IX_DailyCharges_DueDate",
            table: "DailyCharges",
            column: "DueDate");

        migrationBuilder.CreateIndex(
            name: "IX_DailyCharges_PersonId_ChargeDate",
            table: "DailyCharges",
            columns: new[] { "PersonId", "ChargeDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DailyCharges_RateId",
            table: "DailyCharges",
            column: "RateId");

        migrationBuilder.CreateIndex(
            name: "IX_DailyRates_EffectiveDate_EffectiveFromUtc",
            table: "DailyRates",
            columns: new[] { "EffectiveDate", "EffectiveFromUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_FinancialEvents_EntityType_EntityId_OccurredUtc",
            table: "FinancialEvents",
            columns: new[] { "EntityType", "EntityId", "OccurredUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_FinancialEvents_OperationId",
            table: "FinancialEvents",
            column: "OperationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ChairAssignmentPeriods");

        migrationBuilder.DropTable(
            name: "DailyCharges");

        migrationBuilder.DropTable(
            name: "FinancialEvents");

        migrationBuilder.DropTable(
            name: "DailyRates");
    }
}
