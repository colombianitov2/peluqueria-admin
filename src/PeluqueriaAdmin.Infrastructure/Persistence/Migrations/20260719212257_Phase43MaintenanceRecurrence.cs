using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase43MaintenanceRecurrence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CustomInterval",
            table: "MaintenanceRecords",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomIntervalUnit",
            table: "MaintenanceRecords",
            type: "TEXT",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "FirstScheduledDate",
            table: "MaintenanceRecords",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.AddColumn<string>(
            name: "Frequency",
            table: "MaintenanceRecords",
            type: "TEXT",
            maxLength: 40,
            nullable: false,
            defaultValue: "Once");

        migrationBuilder.AddColumn<int>(
            name: "OccurrenceNumber",
            table: "MaintenanceRecords",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "SeriesId",
            table: "MaintenanceRecords",
            type: "TEXT",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.Sql(
            """
            UPDATE MaintenanceRecords
            SET Frequency = 'Once',
                FirstScheduledDate = ScheduledDate,
                SeriesId = Id,
                OccurrenceNumber = 0;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceRecords_SeriesId_OccurrenceNumber",
            table: "MaintenanceRecords",
            columns: new[] { "SeriesId", "OccurrenceNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceRecords_SeriesId_ScheduledDate",
            table: "MaintenanceRecords",
            columns: new[] { "SeriesId", "ScheduledDate" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MaintenanceRecords_SeriesId_OccurrenceNumber",
            table: "MaintenanceRecords");

        migrationBuilder.DropIndex(
            name: "IX_MaintenanceRecords_SeriesId_ScheduledDate",
            table: "MaintenanceRecords");

        migrationBuilder.DropColumn(
            name: "CustomInterval",
            table: "MaintenanceRecords");

        migrationBuilder.DropColumn(
            name: "CustomIntervalUnit",
            table: "MaintenanceRecords");

        migrationBuilder.DropColumn(
            name: "FirstScheduledDate",
            table: "MaintenanceRecords");

        migrationBuilder.DropColumn(
            name: "Frequency",
            table: "MaintenanceRecords");

        migrationBuilder.DropColumn(
            name: "OccurrenceNumber",
            table: "MaintenanceRecords");

        migrationBuilder.DropColumn(
            name: "SeriesId",
            table: "MaintenanceRecords");
    }
}
