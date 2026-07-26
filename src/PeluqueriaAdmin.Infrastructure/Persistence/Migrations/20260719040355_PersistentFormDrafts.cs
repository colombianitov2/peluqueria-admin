using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PersistentFormDrafts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FormDrafts",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                Module = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                FormType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                IsEdit = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FormDrafts", x => x.Key);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FormDrafts_Module_FormType_EntityId",
            table: "FormDrafts",
            columns: new[] { "Module", "FormType", "EntityId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FormDrafts");
    }
}
