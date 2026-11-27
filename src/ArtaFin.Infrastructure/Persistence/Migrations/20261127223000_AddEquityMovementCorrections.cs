using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261127223000_AddEquityMovementCorrections")]
public sealed class AddEquityMovementCorrections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EquityMovementCorrections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LineNumber = table.Column<int>(type: "int", nullable: false),
                OriginalMovementType = table.Column<byte>(type: "tinyint", nullable: false),
                CorrectedMovementType = table.Column<byte>(type: "tinyint", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EquityMovementCorrections", x => x.Id);
                table.CheckConstraint(
                    "CK_EquityMovementCorrections_LineNumber_Positive",
                    "[LineNumber] > 0");
                table.CheckConstraint(
                    "CK_EquityMovementCorrections_OriginalType_Valid",
                    "[OriginalMovementType] IN (0, 1, 2, 3, 4)");
                table.CheckConstraint(
                    "CK_EquityMovementCorrections_CorrectedType_Valid",
                    "[CorrectedMovementType] IN (1, 2, 3, 4)");
                table.CheckConstraint(
                    "CK_EquityMovementCorrections_Types_Different",
                    "[OriginalMovementType] <> [CorrectedMovementType]");
                table.ForeignKey(
                    "FK_EquityMovementCorrections_FinancialBooks_FinancialBookId",
                    x => x.FinancialBookId,
                    "FinancialBooks",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_EquityMovementCorrections_JournalEntries_FinancialBookId_JournalEntryId",
                    x => new { x.FinancialBookId, x.JournalEntryId },
                    "JournalEntries",
                    new[] { "FinancialBookId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EquityMovementCorrections_Line_History",
            table: "EquityMovementCorrections",
            columns: new[] { "FinancialBookId", "JournalEntryId", "LineNumber", "CreatedAt" });

        // The correction must target an existing immutable journal line.
        migrationBuilder.AddForeignKey(
            name: "FK_EquityMovementCorrections_JournalLines_JournalEntryId_LineNumber",
            table: "EquityMovementCorrections",
            columns: new[] { "JournalEntryId", "LineNumber" },
            principalTable: "JournalLines",
            principalColumns: new[] { "JournalEntryId", "LineNumber" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EquityMovementCorrections");
    }
}
