using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase521NoEditableDefaults : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<long>(
            name: "WeeklyUsageFeeMinorUnits",
            table: "Settings",
            type: "INTEGER",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "INTEGER");

        migrationBuilder.AlterColumn<int>(
            name: "CollaboratorProfitBasisPoints",
            table: "Settings",
            type: "INTEGER",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "INTEGER");

        migrationBuilder.AddColumn<DateOnly>(
            name: "EffectiveToDateExclusive",
            table: "DailyRates",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "ProfitShareBasisPoints",
            table: "Collaborators",
            type: "INTEGER",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "INTEGER",
            oldDefaultValue: 0);

        migrationBuilder.AlterColumn<int>(
            name: "FundParticipationBasisPoints",
            table: "Collaborators",
            type: "INTEGER",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "INTEGER",
            oldDefaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EffectiveToDateExclusive",
            table: "DailyRates");

        migrationBuilder.AlterColumn<long>(
            name: "WeeklyUsageFeeMinorUnits",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "INTEGER",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "CollaboratorProfitBasisPoints",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "INTEGER",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "ProfitShareBasisPoints",
            table: "Collaborators",
            type: "INTEGER",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "INTEGER",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "FundParticipationBasisPoints",
            table: "Collaborators",
            type: "INTEGER",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "INTEGER",
            oldNullable: true);
    }
}
