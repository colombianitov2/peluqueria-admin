using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase47SimplificationAndNotes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSettled",
            table: "Obligations",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "FundParticipationBasisPoints",
            table: "Collaborators",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "Notes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notes", x => x.Id);
                table.CheckConstraint("CK_Notes_Singleton", "Id = 1");
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_Collaborators_FundParticipationBasisPoints",
            table: "Collaborators",
            sql: "FundParticipationBasisPoints >= 0 AND FundParticipationBasisPoints <= 10000");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Collaborators_FundParticipationBasisPoints",
            table: "Collaborators");

        migrationBuilder.DropColumn(
            name: "IsSettled",
            table: "Obligations");

        migrationBuilder.DropColumn(
            name: "FundParticipationBasisPoints",
            table: "Collaborators");
    }
}
