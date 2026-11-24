using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261124120000_EnsureJournalLineFinancialBookConsistency")]
public sealed class EnsureJournalLineFinancialBookConsistency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM [JournalLines] AS line
                INNER JOIN [JournalEntries] AS entry
                    ON entry.[Id] = line.[JournalEntryId]
                WHERE line.[FinancialBookId] <> entry.[FinancialBookId]
            )
                THROW 51000, 'Cannot enforce journal-line ownership: existing lines belong to a different financial book than their journal entry.', 1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_JournalLines_JournalEntries_JournalEntryId",
            table: "JournalLines");

        migrationBuilder.AddUniqueConstraint(
            name: "AK_JournalEntries_FinancialBookId_Id",
            table: "JournalEntries",
            columns: new[] { "FinancialBookId", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_JournalLines_FinancialBookId_JournalEntryId",
            table: "JournalLines",
            columns: new[] { "FinancialBookId", "JournalEntryId" });

        migrationBuilder.AddForeignKey(
            name: "FK_JournalLines_JournalEntries_FinancialBookId_JournalEntryId",
            table: "JournalLines",
            columns: new[] { "FinancialBookId", "JournalEntryId" },
            principalTable: "JournalEntries",
            principalColumns: new[] { "FinancialBookId", "Id" },
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_JournalLines_JournalEntries_FinancialBookId_JournalEntryId",
            table: "JournalLines");

        migrationBuilder.DropIndex(
            name: "IX_JournalLines_FinancialBookId_JournalEntryId",
            table: "JournalLines");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_JournalEntries_FinancialBookId_Id",
            table: "JournalEntries");

        migrationBuilder.AddForeignKey(
            name: "FK_JournalLines_JournalEntries_JournalEntryId",
            table: "JournalLines",
            column: "JournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
