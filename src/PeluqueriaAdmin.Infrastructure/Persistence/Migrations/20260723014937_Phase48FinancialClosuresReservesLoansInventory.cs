using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase48FinancialClosuresReservesLoansInventory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "AccountsPayableMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "AccountsReceivableMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "BreakEvenMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "CarriedReservesMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "FinancingReceivedMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "LoanPaymentsMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "NewReservesMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "PaidOutflowsMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "PriorUncoveredCommitmentsMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "ReserveAdjustmentsMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "ShortfallMinorUnits",
            table: "MonthlyCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<int>(
            name: "GlobalPercentageBasisPoints",
            table: "MonthlyCloseParticipants",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "IndividualPercentageBasisPoints",
            table: "MonthlyCloseParticipants",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("""
            UPDATE MonthlyCloseParticipants
            SET GlobalPercentageBasisPoints = COALESCE(
                    (SELECT CollaboratorPercentageBasisPoints FROM MonthlyCloses
                     WHERE MonthlyCloses.Id = MonthlyCloseParticipants.CloseId), 0),
                IndividualPercentageBasisPoints = COALESCE(
                    (SELECT FundParticipationBasisPoints FROM Collaborators
                     WHERE Collaborators.Id = MonthlyCloseParticipants.CollaboratorId), 0);
            """);

        migrationBuilder.CreateTable(
            name: "AnnualCloses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Year = table.Column<int>(type: "INTEGER", nullable: false),
                IncomeMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                PaidOutflowsMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                ReservesMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                ObligationsMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                LoanPaymentsMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CollaboratorFundMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                ResultMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                ClosedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnnualCloses", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FinancialCloseExclusions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialCloseExclusions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FinancialReserves",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ReservedAmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                ActualAmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                SettledDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialReserves", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Loans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                InitialBalanceMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                PendingBalanceMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                UsualInstallmentMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                InstallmentCount = table.Column<int>(type: "INTEGER", nullable: true),
                NextDueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Loans", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MonthlyPurchaseItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                ExpectedUnitCostMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                ReserveWhenOutOfStock = table.Column<bool>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                PurchaseMovementId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonthlyPurchaseItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_MonthlyPurchaseItems_InventoryMovements_PurchaseMovementId",
                    column: x => x.PurchaseMovementId,
                    principalTable: "InventoryMovements",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MonthlyPurchaseItems_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LoanPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                LoanId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoanPayments", x => x.Id);
                table.ForeignKey(
                    name: "FK_LoanPayments_Loans_LoanId",
                    column: x => x.LoanId,
                    principalTable: "Loans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AnnualCloses_Year",
            table: "AnnualCloses",
            column: "Year",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinancialCloseExclusions_Month_SourceType_SourceId",
            table: "FinancialCloseExclusions",
            columns: new[] { "Month", "SourceType", "SourceId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinancialReserves_Month_SourceType_SourceId",
            table: "FinancialReserves",
            columns: new[] { "Month", "SourceType", "SourceId" });

        migrationBuilder.CreateIndex(
            name: "IX_LoanPayments_LoanId_Date",
            table: "LoanPayments",
            columns: new[] { "LoanId", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_Loans_NextDueDate",
            table: "Loans",
            column: "NextDueDate");

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyPurchaseItems_ProductId_Month",
            table: "MonthlyPurchaseItems",
            columns: new[] { "ProductId", "Month" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyPurchaseItems_PurchaseMovementId",
            table: "MonthlyPurchaseItems",
            column: "PurchaseMovementId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AnnualCloses");

        migrationBuilder.DropTable(
            name: "FinancialCloseExclusions");

        migrationBuilder.DropTable(
            name: "FinancialReserves");

        migrationBuilder.DropTable(
            name: "LoanPayments");

        migrationBuilder.DropTable(
            name: "MonthlyPurchaseItems");

        migrationBuilder.DropTable(
            name: "Loans");

        migrationBuilder.DropColumn(
            name: "AccountsPayableMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "AccountsReceivableMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "BreakEvenMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "CarriedReservesMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "FinancingReceivedMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "LoanPaymentsMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "NewReservesMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "PaidOutflowsMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "PriorUncoveredCommitmentsMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "ReserveAdjustmentsMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "ShortfallMinorUnits",
            table: "MonthlyCloses");

        migrationBuilder.DropColumn(
            name: "GlobalPercentageBasisPoints",
            table: "MonthlyCloseParticipants");

        migrationBuilder.DropColumn(
            name: "IndividualPercentageBasisPoints",
            table: "MonthlyCloseParticipants");
    }
}
