using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class CompleteAdministration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Collaborators",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ExitDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Collaborators", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FinancialEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Concept = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Category = table.Column<int>(type: "INTEGER", nullable: true),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialEntries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LocalUsePeople",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                EntryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ExitDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LocalUsePeople", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MaintenanceRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Asset = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                MaintenanceType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ScheduledDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                EstimatedCostMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                CompletedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                ActualCostMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MaintenanceRecords", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MonthlyCloses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                CollaboratorPercentageBasisPoints = table.Column<int>(type: "INTEGER", nullable: false),
                IncomeMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                GoalMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                BaseResultMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                FundMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                RetainedResultMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                ClosedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                ReopenedUtc = table.Column<long>(type: "INTEGER", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonthlyCloses", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Obligations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SeriesId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ExpectedAmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                Recurrence = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Obligations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Category = table.Column<int>(type: "INTEGER", nullable: false),
                UnitOfMeasure = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WeeklyRates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WeeklyRates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "LocalUsePayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LocalUsePayments", x => x.Id);
                table.ForeignKey(
                    name: "FK_LocalUsePayments_LocalUsePeople_PersonId",
                    column: x => x.PersonId,
                    principalTable: "LocalUsePeople",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WeeklyCharges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WeeklyCharges", x => x.Id);
                table.ForeignKey(
                    name: "FK_WeeklyCharges_LocalUsePeople_PersonId",
                    column: x => x.PersonId,
                    principalTable: "LocalUsePeople",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MonthlyCloseParticipants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CloseId = table.Column<Guid>(type: "TEXT", nullable: false),
                CollaboratorId = table.Column<Guid>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonthlyCloseParticipants", x => x.Id);
                table.ForeignKey(
                    name: "FK_MonthlyCloseParticipants_Collaborators_CollaboratorId",
                    column: x => x.CollaboratorId,
                    principalTable: "Collaborators",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MonthlyCloseParticipants_MonthlyCloses_CloseId",
                    column: x => x.CloseId,
                    principalTable: "MonthlyCloses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ObligationPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ObligationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ObligationPayments", x => x.Id);
                table.ForeignKey(
                    name: "FK_ObligationPayments_Obligations_ObligationId",
                    column: x => x.ObligationId,
                    principalTable: "Obligations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InventoryMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                QuantityDelta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                CashAmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                EstimatedCostMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                table.ForeignKey(
                    name: "FK_InventoryMovements_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MonthlyRestockPlans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                NeededQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonthlyRestockPlans", x => x.Id);
                table.ForeignKey(
                    name: "FK_MonthlyRestockPlans_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DistributionPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DistributionPayments", x => x.Id);
                table.ForeignKey(
                    name: "FK_DistributionPayments_MonthlyCloseParticipants_ParticipantId",
                    column: x => x.ParticipantId,
                    principalTable: "MonthlyCloseParticipants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DistributionPayments_Date",
            table: "DistributionPayments",
            column: "Date");

        migrationBuilder.CreateIndex(
            name: "IX_DistributionPayments_ParticipantId",
            table: "DistributionPayments",
            column: "ParticipantId");

        migrationBuilder.CreateIndex(
            name: "IX_FinancialEntries_Date",
            table: "FinancialEntries",
            column: "Date");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryMovements_ProductId_Date",
            table: "InventoryMovements",
            columns: new[] { "ProductId", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_LocalUsePayments_PaymentDate",
            table: "LocalUsePayments",
            column: "PaymentDate");

        migrationBuilder.CreateIndex(
            name: "IX_LocalUsePayments_PersonId",
            table: "LocalUsePayments",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyCloseParticipants_CloseId_CollaboratorId",
            table: "MonthlyCloseParticipants",
            columns: new[] { "CloseId", "CollaboratorId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyCloseParticipants_CollaboratorId",
            table: "MonthlyCloseParticipants",
            column: "CollaboratorId");

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyCloses_Month",
            table: "MonthlyCloses",
            column: "Month");

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyRestockPlans_ProductId_Month",
            table: "MonthlyRestockPlans",
            columns: new[] { "ProductId", "Month" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ObligationPayments_Date",
            table: "ObligationPayments",
            column: "Date");

        migrationBuilder.CreateIndex(
            name: "IX_ObligationPayments_ObligationId",
            table: "ObligationPayments",
            column: "ObligationId");

        migrationBuilder.CreateIndex(
            name: "IX_Obligations_SeriesId_DueDate",
            table: "Obligations",
            columns: new[] { "SeriesId", "DueDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WeeklyCharges_PersonId_PeriodStart",
            table: "WeeklyCharges",
            columns: new[] { "PersonId", "PeriodStart" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WeeklyRates_EffectiveFrom",
            table: "WeeklyRates",
            column: "EffectiveFrom");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DistributionPayments");

        migrationBuilder.DropTable(
            name: "FinancialEntries");

        migrationBuilder.DropTable(
            name: "InventoryMovements");

        migrationBuilder.DropTable(
            name: "LocalUsePayments");

        migrationBuilder.DropTable(
            name: "MaintenanceRecords");

        migrationBuilder.DropTable(
            name: "MonthlyRestockPlans");

        migrationBuilder.DropTable(
            name: "ObligationPayments");

        migrationBuilder.DropTable(
            name: "WeeklyCharges");

        migrationBuilder.DropTable(
            name: "WeeklyRates");

        migrationBuilder.DropTable(
            name: "MonthlyCloseParticipants");

        migrationBuilder.DropTable(
            name: "Products");

        migrationBuilder.DropTable(
            name: "Obligations");

        migrationBuilder.DropTable(
            name: "LocalUsePeople");

        migrationBuilder.DropTable(
            name: "Collaborators");

        migrationBuilder.DropTable(
            name: "MonthlyCloses");
    }
}
