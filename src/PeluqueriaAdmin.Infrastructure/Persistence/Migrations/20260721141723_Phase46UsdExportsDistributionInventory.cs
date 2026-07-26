using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase46UsdExportsDistributionInventory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExportDirectory",
            table: "Settings",
            type: "TEXT",
            maxLength: 1024,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<long>(
            name: "DefaultUnitCostMinorUnits",
            table: "Products",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ProfitShareBasisPoints",
            table: "Collaborators",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Collaborators_ProfitShareBasisPoints",
            table: "Collaborators",
            sql: "ProfitShareBasisPoints >= 0 AND ProfitShareBasisPoints <= 10000");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Collaborators_ProfitShareBasisPoints",
            table: "Collaborators");

        migrationBuilder.DropColumn(
            name: "ExportDirectory",
            table: "Settings");

        migrationBuilder.DropColumn(
            name: "DefaultUnitCostMinorUnits",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "ProfitShareBasisPoints",
            table: "Collaborators");
    }
}
