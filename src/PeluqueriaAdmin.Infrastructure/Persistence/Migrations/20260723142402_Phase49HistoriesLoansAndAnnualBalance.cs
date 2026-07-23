using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase49HistoriesLoansAndAnnualBalance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MonthlyPurchaseItems_ProductId_Month",
            table: "MonthlyPurchaseItems");

        migrationBuilder.AlterColumn<Guid>(
            name: "ProductId",
            table: "MonthlyPurchaseItems",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "TEXT");

        migrationBuilder.AddColumn<int>(
            name: "Category",
            table: "MonthlyPurchaseItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "MonthlyPurchaseItems",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "CalculationMethod",
            table: "Loans",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "EquivalentMonthlyRateBasisPoints",
            table: "Loans",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "ExpectedTotalMinorUnits",
            table: "Loans",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<int>(
            name: "MonthlyInterestBasisPoints",
            table: "Loans",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "TotalInterestMinorUnits",
            table: "Loans",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<Guid>(
            name: "InstallmentId",
            table: "LoanPayments",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "AccountsPayableMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "AccountsReceivableMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "AvailableBalanceMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "DeficitMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "PendingLoansMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "PendingReservesMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "ProjectedNextYearBalanceMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "SurplusMinorUnits",
            table: "AnnualCloses",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "AnnualCarryovers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceYear = table.Column<int>(type: "INTEGER", nullable: false),
                TargetYear = table.Column<int>(type: "INTEGER", nullable: false),
                AccountsReceivableMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                AccountsPayableMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                PendingReservesMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                PendingLoansMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                SurplusMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                DeficitMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnnualCarryovers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CollaboratorContributionEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ContributionId = table.Column<Guid>(type: "TEXT", nullable: false),
                CollaboratorId = table.Column<Guid>(type: "TEXT", nullable: false),
                EventType = table.Column<int>(type: "INTEGER", nullable: false),
                PreviousEffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                PreviousAmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: true),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                PreviousDescription = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                OccurredUtc = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CollaboratorContributionEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_CollaboratorContributionEvents_Collaborators_CollaboratorId",
                    column: x => x.CollaboratorId,
                    principalTable: "Collaborators",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LoanInstallments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                LoanId = table.Column<Guid>(type: "TEXT", nullable: false),
                Number = table.Column<int>(type: "INTEGER", nullable: false),
                DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                PrincipalMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                InterestMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                PrincipalBalanceAfterMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DeletedUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoanInstallments", x => x.Id);
                table.ForeignKey(
                    name: "FK_LoanInstallments_Loans_LoanId",
                    column: x => x.LoanId,
                    principalTable: "Loans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql(
            """
            UPDATE "MonthlyPurchaseItems"
            SET "Name" = COALESCE(
                    (SELECT "Name" FROM "Products"
                     WHERE "Products"."Id" = "MonthlyPurchaseItems"."ProductId"),
                    'Producto planificado'),
                "Category" = COALESCE(
                    (SELECT "Category" FROM "Products"
                     WHERE "Products"."Id" = "MonthlyPurchaseItems"."ProductId"),
                    6);

            UPDATE "Loans"
            SET "ExpectedTotalMinorUnits" = "InitialBalanceMinorUnits",
                "TotalInterestMinorUnits" = 0,
                "CalculationMethod" = 0,
                "MonthlyInterestBasisPoints" = 0,
                "EquivalentMonthlyRateBasisPoints" = 0;

            INSERT INTO "CollaboratorContributionEvents" (
                "Id", "ContributionId", "CollaboratorId", "EventType",
                "PreviousEffectiveDate", "EffectiveDate",
                "PreviousAmountMinorUnits", "AmountMinorUnits",
                "PreviousDescription", "Description", "OccurredUtc",
                "CreatedUtc", "UpdatedUtc", "DeletedUtc")
            SELECT
                lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' ||
                      hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' ||
                      hex(randomblob(6))),
                "Id", "CollaboratorId", 4,
                NULL, "Date", NULL, "AmountMinorUnits",
                NULL, "Description", "CreatedUtc",
                "CreatedUtc", "CreatedUtc", NULL
            FROM "CollaboratorContributions";

            INSERT INTO "CollaboratorContributionEvents" (
                "Id", "ContributionId", "CollaboratorId", "EventType",
                "PreviousEffectiveDate", "EffectiveDate",
                "PreviousAmountMinorUnits", "AmountMinorUnits",
                "PreviousDescription", "Description", "OccurredUtc",
                "CreatedUtc", "UpdatedUtc", "DeletedUtc")
            SELECT
                lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' ||
                      hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' ||
                      hex(randomblob(6))),
                "Id", "CollaboratorId", 3,
                "Date", "Date", "AmountMinorUnits", "AmountMinorUnits",
                "Description", "Description", "DeletedUtc",
                "DeletedUtc", "DeletedUtc", NULL
            FROM "CollaboratorContributions"
            WHERE "DeletedUtc" IS NOT NULL;

            INSERT INTO "LoanInstallments" (
                "Id", "LoanId", "Number", "DueDate",
                "AmountMinorUnits", "PrincipalMinorUnits", "InterestMinorUnits",
                "PrincipalBalanceAfterMinorUnits", "Description",
                "CreatedUtc", "UpdatedUtc", "DeletedUtc")
            SELECT
                lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' ||
                      hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' ||
                      hex(randomblob(6))),
                "Id", 1, "NextDueDate",
                "PendingBalanceMinorUnits", "PendingBalanceMinorUnits", 0,
                0, 'Saldo pendiente migrado desde Fase 4.8',
                "UpdatedUtc", "UpdatedUtc", NULL
            FROM "Loans"
            WHERE "PendingBalanceMinorUnits" > 0;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyPurchaseItems_Name_Month",
            table: "MonthlyPurchaseItems",
            columns: new[] { "Name", "Month" });

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyPurchaseItems_ProductId",
            table: "MonthlyPurchaseItems",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_LoanPayments_InstallmentId",
            table: "LoanPayments",
            column: "InstallmentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AnnualCarryovers_SourceYear",
            table: "AnnualCarryovers",
            column: "SourceYear",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AnnualCarryovers_TargetYear",
            table: "AnnualCarryovers",
            column: "TargetYear",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CollaboratorContributionEvents_CollaboratorId_OccurredUtc",
            table: "CollaboratorContributionEvents",
            columns: new[] { "CollaboratorId", "OccurredUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_CollaboratorContributionEvents_ContributionId_OccurredUtc",
            table: "CollaboratorContributionEvents",
            columns: new[] { "ContributionId", "OccurredUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_LoanInstallments_LoanId_DueDate",
            table: "LoanInstallments",
            columns: new[] { "LoanId", "DueDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LoanInstallments_LoanId_Number",
            table: "LoanInstallments",
            columns: new[] { "LoanId", "Number" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_LoanPayments_LoanInstallments_InstallmentId",
            table: "LoanPayments",
            column: "InstallmentId",
            principalTable: "LoanInstallments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_LoanPayments_LoanInstallments_InstallmentId",
            table: "LoanPayments");

        migrationBuilder.DropTable(
            name: "AnnualCarryovers");

        migrationBuilder.DropTable(
            name: "CollaboratorContributionEvents");

        migrationBuilder.DropTable(
            name: "LoanInstallments");

        migrationBuilder.DropIndex(
            name: "IX_MonthlyPurchaseItems_Name_Month",
            table: "MonthlyPurchaseItems");

        migrationBuilder.DropIndex(
            name: "IX_MonthlyPurchaseItems_ProductId",
            table: "MonthlyPurchaseItems");

        migrationBuilder.DropIndex(
            name: "IX_LoanPayments_InstallmentId",
            table: "LoanPayments");

        migrationBuilder.DropColumn(
            name: "Category",
            table: "MonthlyPurchaseItems");

        migrationBuilder.DropColumn(
            name: "Name",
            table: "MonthlyPurchaseItems");

        migrationBuilder.DropColumn(
            name: "CalculationMethod",
            table: "Loans");

        migrationBuilder.DropColumn(
            name: "EquivalentMonthlyRateBasisPoints",
            table: "Loans");

        migrationBuilder.DropColumn(
            name: "ExpectedTotalMinorUnits",
            table: "Loans");

        migrationBuilder.DropColumn(
            name: "MonthlyInterestBasisPoints",
            table: "Loans");

        migrationBuilder.DropColumn(
            name: "TotalInterestMinorUnits",
            table: "Loans");

        migrationBuilder.DropColumn(
            name: "InstallmentId",
            table: "LoanPayments");

        migrationBuilder.DropColumn(
            name: "AccountsPayableMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.DropColumn(
            name: "AccountsReceivableMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.DropColumn(
            name: "AvailableBalanceMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.DropColumn(
            name: "DeficitMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.DropColumn(
            name: "PendingLoansMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.DropColumn(
            name: "PendingReservesMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.DropColumn(
            name: "ProjectedNextYearBalanceMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.DropColumn(
            name: "SurplusMinorUnits",
            table: "AnnualCloses");

        migrationBuilder.AlterColumn<Guid>(
            name: "ProductId",
            table: "MonthlyPurchaseItems",
            type: "TEXT",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_MonthlyPurchaseItems_ProductId_Month",
            table: "MonthlyPurchaseItems",
            columns: new[] { "ProductId", "Month" },
            unique: true);
    }
}
