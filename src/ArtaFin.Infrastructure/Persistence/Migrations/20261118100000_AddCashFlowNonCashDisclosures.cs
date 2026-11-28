using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261118100000_AddCashFlowNonCashDisclosures")]
public sealed class AddCashFlowNonCashDisclosures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CashFlowNonCashDisclosures",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceLineNumber = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                Activity = table.Column<byte>(type: "tinyint", nullable: false),
                Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Memo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashFlowNonCashDisclosures", x => x.Id);
                table.CheckConstraint("CK_CashFlowNonCashDisclosures_Amount_Positive", "[Amount] > 0");
                table.CheckConstraint("CK_CashFlowNonCashDisclosures_SourceLineNumber_Positive", "[SourceLineNumber] > 0");
                table.CheckConstraint("CK_CashFlowNonCashDisclosures_Activity_Valid", "[Activity] IN (2, 3)");
                table.ForeignKey("FK_CashFlowNonCashDisclosures_FinancialBooks_FinancialBookId",
                    x => x.FinancialBookId, "FinancialBooks", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CashFlowNonCashDisclosures_JournalEntries_JournalEntryId",
                    x => x.JournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_CashFlowNonCashDisclosures_SourceLine",
            table: "CashFlowNonCashDisclosures",
            columns: new[] { "JournalEntryId", "SourceLineNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CashFlowNonCashDisclosures_Book_CreatedAt",
            table: "CashFlowNonCashDisclosures",
            columns: new[] { "FinancialBookId", "CreatedAt" });

        // The source line must exist in the immutable journal entry.
        migrationBuilder.AddForeignKey(
            name: "FK_CashFlowNonCashDisclosures_JournalLines_JournalEntryId_SourceLineNumber",
            table: "CashFlowNonCashDisclosures",
            columns: new[] { "JournalEntryId", "SourceLineNumber" },
            principalTable: "JournalLines",
            principalColumns: new[] { "JournalEntryId", "LineNumber" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "CashFlowNonCashDisclosures");
}
