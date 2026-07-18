using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                WeeklyUsageFeeMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                CollaboratorProfitBasisPoints = table.Column<int>(type: "INTEGER", nullable: false),
                OptionalSuppliesMonthlyBudgetMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                TotalChairs = table.Column<int>(type: "INTEGER", nullable: false),
                CurrencyCode = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 3, nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Id);
                table.CheckConstraint("CK_Settings_CollaboratorProfitBasisPoints", "CollaboratorProfitBasisPoints >= 0 AND CollaboratorProfitBasisPoints <= 10000");
                table.CheckConstraint("CK_Settings_CurrencyCode", "length(CurrencyCode) = 3 AND CurrencyCode GLOB '[A-Z][A-Z][A-Z]'");
                table.CheckConstraint("CK_Settings_OptionalSuppliesMonthlyBudgetMinorUnits", "OptionalSuppliesMonthlyBudgetMinorUnits >= 0");
                table.CheckConstraint("CK_Settings_Singleton", "Id = 1");
                table.CheckConstraint("CK_Settings_TotalChairs", "TotalChairs >= 0");
                table.CheckConstraint("CK_Settings_WeeklyUsageFeeMinorUnits", "WeeklyUsageFeeMinorUnits >= 0");
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Settings");
    }
}
