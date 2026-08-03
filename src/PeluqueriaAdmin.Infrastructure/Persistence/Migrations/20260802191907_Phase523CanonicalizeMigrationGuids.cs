using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaAdmin.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase523CanonicalizeMigrationGuids : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TEMP TABLE "__Phase523GuidValidation"
            (
                "Value" INTEGER NOT NULL,
                CONSTRAINT "CK_Phase523_AllGuidValuesAreValid"
                    CHECK ("Value" = 0)
            );

            INSERT INTO "__Phase523GuidValidation" ("Value")
            SELECT 1
            WHERE EXISTS
            (
                SELECT 1
                FROM
                (
                    SELECT "Id" AS "GuidValue" FROM "DailyRates"
                    UNION ALL SELECT "Id" FROM "Chairs"
                    UNION ALL SELECT "AssignedPersonId" FROM "Chairs" WHERE "AssignedPersonId" IS NOT NULL
                    UNION ALL SELECT "Id" FROM "DailyCharges"
                    UNION ALL SELECT "PersonId" FROM "DailyCharges"
                    UNION ALL SELECT "ChairId" FROM "DailyCharges"
                    UNION ALL SELECT "RateId" FROM "DailyCharges"
                    UNION ALL SELECT "Id" FROM "ChairAssignmentPeriods"
                    UNION ALL SELECT "ChairId" FROM "ChairAssignmentPeriods"
                    UNION ALL SELECT "PersonId" FROM "ChairAssignmentPeriods"
                    UNION ALL SELECT "Id" FROM "FinancialEvents"
                    UNION ALL SELECT "OperationId" FROM "FinancialEvents"
                    UNION ALL SELECT "EntityId" FROM "FinancialEvents"
                ) AS "GuidValues"
                WHERE typeof("GuidValue") <> 'text'
                   OR length("GuidValue") NOT IN (32, 36)
                   OR length(replace("GuidValue", '-', '')) <> 32
                   OR replace("GuidValue", '-', '') GLOB '*[^0-9A-Fa-f]*'
                   OR
                   (
                       length("GuidValue") = 36
                       AND
                       (
                           substr("GuidValue", 9, 1) <> '-'
                           OR substr("GuidValue", 14, 1) <> '-'
                           OR substr("GuidValue", 19, 1) <> '-'
                           OR substr("GuidValue", 24, 1) <> '-'
                       )
                   )
            );

            CREATE TEMP TABLE "__Phase523CanonicalPrimaryKeys"
            (
                "TableName" TEXT NOT NULL,
                "CanonicalId" TEXT NOT NULL,
                CONSTRAINT "PK_Phase523_CanonicalPrimaryKeys"
                    PRIMARY KEY ("TableName", "CanonicalId")
            );

            INSERT INTO "__Phase523CanonicalPrimaryKeys" ("TableName", "CanonicalId")
            SELECT 'DailyRates',
                   upper(substr(replace("Id", '-', ''), 1, 8)
                       || '-' || substr(replace("Id", '-', ''), 9, 4)
                       || '-' || substr(replace("Id", '-', ''), 13, 4)
                       || '-' || substr(replace("Id", '-', ''), 17, 4)
                       || '-' || substr(replace("Id", '-', ''), 21, 12))
            FROM "DailyRates"
            UNION ALL
            SELECT 'Chairs',
                   upper(substr(replace("Id", '-', ''), 1, 8)
                       || '-' || substr(replace("Id", '-', ''), 9, 4)
                       || '-' || substr(replace("Id", '-', ''), 13, 4)
                       || '-' || substr(replace("Id", '-', ''), 17, 4)
                       || '-' || substr(replace("Id", '-', ''), 21, 12))
            FROM "Chairs"
            UNION ALL
            SELECT 'DailyCharges',
                   upper(substr(replace("Id", '-', ''), 1, 8)
                       || '-' || substr(replace("Id", '-', ''), 9, 4)
                       || '-' || substr(replace("Id", '-', ''), 13, 4)
                       || '-' || substr(replace("Id", '-', ''), 17, 4)
                       || '-' || substr(replace("Id", '-', ''), 21, 12))
            FROM "DailyCharges"
            UNION ALL
            SELECT 'ChairAssignmentPeriods',
                   upper(substr(replace("Id", '-', ''), 1, 8)
                       || '-' || substr(replace("Id", '-', ''), 9, 4)
                       || '-' || substr(replace("Id", '-', ''), 13, 4)
                       || '-' || substr(replace("Id", '-', ''), 17, 4)
                       || '-' || substr(replace("Id", '-', ''), 21, 12))
            FROM "ChairAssignmentPeriods"
            UNION ALL
            SELECT 'FinancialEvents',
                   upper(substr(replace("Id", '-', ''), 1, 8)
                       || '-' || substr(replace("Id", '-', ''), 9, 4)
                       || '-' || substr(replace("Id", '-', ''), 13, 4)
                       || '-' || substr(replace("Id", '-', ''), 17, 4)
                       || '-' || substr(replace("Id", '-', ''), 21, 12))
            FROM "FinancialEvents";

            PRAGMA defer_foreign_keys = ON;

            UPDATE "DailyCharges"
            SET "Id" = upper(substr(replace("Id", '-', ''), 1, 8)
                    || '-' || substr(replace("Id", '-', ''), 9, 4)
                    || '-' || substr(replace("Id", '-', ''), 13, 4)
                    || '-' || substr(replace("Id", '-', ''), 17, 4)
                    || '-' || substr(replace("Id", '-', ''), 21, 12)),
                "PersonId" = upper(substr(replace("PersonId", '-', ''), 1, 8)
                    || '-' || substr(replace("PersonId", '-', ''), 9, 4)
                    || '-' || substr(replace("PersonId", '-', ''), 13, 4)
                    || '-' || substr(replace("PersonId", '-', ''), 17, 4)
                    || '-' || substr(replace("PersonId", '-', ''), 21, 12)),
                "ChairId" = upper(substr(replace("ChairId", '-', ''), 1, 8)
                    || '-' || substr(replace("ChairId", '-', ''), 9, 4)
                    || '-' || substr(replace("ChairId", '-', ''), 13, 4)
                    || '-' || substr(replace("ChairId", '-', ''), 17, 4)
                    || '-' || substr(replace("ChairId", '-', ''), 21, 12)),
                "RateId" = upper(substr(replace("RateId", '-', ''), 1, 8)
                    || '-' || substr(replace("RateId", '-', ''), 9, 4)
                    || '-' || substr(replace("RateId", '-', ''), 13, 4)
                    || '-' || substr(replace("RateId", '-', ''), 17, 4)
                    || '-' || substr(replace("RateId", '-', ''), 21, 12));

            UPDATE "ChairAssignmentPeriods"
            SET "Id" = upper(substr(replace("Id", '-', ''), 1, 8)
                    || '-' || substr(replace("Id", '-', ''), 9, 4)
                    || '-' || substr(replace("Id", '-', ''), 13, 4)
                    || '-' || substr(replace("Id", '-', ''), 17, 4)
                    || '-' || substr(replace("Id", '-', ''), 21, 12)),
                "ChairId" = upper(substr(replace("ChairId", '-', ''), 1, 8)
                    || '-' || substr(replace("ChairId", '-', ''), 9, 4)
                    || '-' || substr(replace("ChairId", '-', ''), 13, 4)
                    || '-' || substr(replace("ChairId", '-', ''), 17, 4)
                    || '-' || substr(replace("ChairId", '-', ''), 21, 12)),
                "PersonId" = upper(substr(replace("PersonId", '-', ''), 1, 8)
                    || '-' || substr(replace("PersonId", '-', ''), 9, 4)
                    || '-' || substr(replace("PersonId", '-', ''), 13, 4)
                    || '-' || substr(replace("PersonId", '-', ''), 17, 4)
                    || '-' || substr(replace("PersonId", '-', ''), 21, 12));

            UPDATE "FinancialEvents"
            SET "Id" = upper(substr(replace("Id", '-', ''), 1, 8)
                    || '-' || substr(replace("Id", '-', ''), 9, 4)
                    || '-' || substr(replace("Id", '-', ''), 13, 4)
                    || '-' || substr(replace("Id", '-', ''), 17, 4)
                    || '-' || substr(replace("Id", '-', ''), 21, 12)),
                "OperationId" = upper(substr(replace("OperationId", '-', ''), 1, 8)
                    || '-' || substr(replace("OperationId", '-', ''), 9, 4)
                    || '-' || substr(replace("OperationId", '-', ''), 13, 4)
                    || '-' || substr(replace("OperationId", '-', ''), 17, 4)
                    || '-' || substr(replace("OperationId", '-', ''), 21, 12)),
                "EntityId" = upper(substr(replace("EntityId", '-', ''), 1, 8)
                    || '-' || substr(replace("EntityId", '-', ''), 9, 4)
                    || '-' || substr(replace("EntityId", '-', ''), 13, 4)
                    || '-' || substr(replace("EntityId", '-', ''), 17, 4)
                    || '-' || substr(replace("EntityId", '-', ''), 21, 12));

            UPDATE "DailyRates"
            SET "Id" = upper(substr(replace("Id", '-', ''), 1, 8)
                    || '-' || substr(replace("Id", '-', ''), 9, 4)
                    || '-' || substr(replace("Id", '-', ''), 13, 4)
                    || '-' || substr(replace("Id", '-', ''), 17, 4)
                    || '-' || substr(replace("Id", '-', ''), 21, 12));

            UPDATE "Chairs"
            SET "Id" = upper(substr(replace("Id", '-', ''), 1, 8)
                    || '-' || substr(replace("Id", '-', ''), 9, 4)
                    || '-' || substr(replace("Id", '-', ''), 13, 4)
                    || '-' || substr(replace("Id", '-', ''), 17, 4)
                    || '-' || substr(replace("Id", '-', ''), 21, 12)),
                "AssignedPersonId" = CASE
                    WHEN "AssignedPersonId" IS NULL THEN NULL
                    ELSE upper(substr(replace("AssignedPersonId", '-', ''), 1, 8)
                        || '-' || substr(replace("AssignedPersonId", '-', ''), 9, 4)
                        || '-' || substr(replace("AssignedPersonId", '-', ''), 13, 4)
                        || '-' || substr(replace("AssignedPersonId", '-', ''), 17, 4)
                        || '-' || substr(replace("AssignedPersonId", '-', ''), 21, 12))
                END;

            DROP TABLE "__Phase523CanonicalPrimaryKeys";
            DROP TABLE "__Phase523GuidValidation";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // La procedencia del formato anterior no se puede distinguir después
        // de normalizar. Reintroducir identificadores incompatibles sería
        // destructivo, por lo que la reversión más segura no modifica datos.
    }
}
