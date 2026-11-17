using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261117150000_AddCashFlowAllocationSets")]
public sealed class AddCashFlowAllocationSets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CashFlowAllocationSets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceLineNumber = table.Column<int>(type: "int", nullable: false),
                SourceAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                SupersedesAllocationSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<byte>(type: "tinyint", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                FinalizedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashFlowAllocationSets", x => x.Id);
                table.CheckConstraint("CK_CashFlowAllocationSets_SourceAmount_Positive", "[SourceAmount] > 0");
                table.CheckConstraint("CK_CashFlowAllocationSets_SourceLineNumber_Positive", "[SourceLineNumber] > 0");
                table.CheckConstraint("CK_CashFlowAllocationSets_Version_Positive", "[Version] > 0");
                table.CheckConstraint("CK_CashFlowAllocationSets_Status_Valid", "[Status] IN (1, 2)");
                table.CheckConstraint("CK_CashFlowAllocationSets_Finalization_Consistent",
                    "([Status] = 1 AND [FinalizedBy] IS NULL AND [FinalizedAt] IS NULL) OR ([Status] = 2 AND [FinalizedBy] IS NOT NULL AND [FinalizedAt] IS NOT NULL)");
                table.CheckConstraint("CK_CashFlowAllocationSets_Supersedes_Consistent",
                    "([Version] = 1 AND [SupersedesAllocationSetId] IS NULL) OR ([Version] > 1 AND [SupersedesAllocationSetId] IS NOT NULL)");
                table.ForeignKey("FK_CashFlowAllocationSets_FinancialBooks_FinancialBookId",
                    x => x.FinancialBookId, "FinancialBooks", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CashFlowAllocationSets_JournalEntries_SourceJournalEntryId",
                    x => x.SourceJournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CashFlowAllocationSets_Supersedes_SupersedesAllocationSetId",
                    x => x.SupersedesAllocationSetId, "CashFlowAllocationSets", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_CashFlowAllocationSets_Source_Version",
            table: "CashFlowAllocationSets",
            columns: new[] { "SourceJournalEntryId", "SourceLineNumber", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_CashFlowAllocationSets_CurrentFinalized",
            table: "CashFlowAllocationSets",
            columns: new[] { "SourceJournalEntryId", "SourceLineNumber" },
            unique: true,
            filter: "[Status] = 2");

        migrationBuilder.CreateIndex(
            name: "IX_CashFlowAllocationSets_FinancialBookId",
            table: "CashFlowAllocationSets",
            column: "FinancialBookId");

        migrationBuilder.CreateIndex(
            name: "IX_CashFlowAllocationSets_SupersedesAllocationSetId",
            table: "CashFlowAllocationSets",
            column: "SupersedesAllocationSetId");

        migrationBuilder.CreateTable(
            name: "CashFlowAllocations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AllocationSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                Activity = table.Column<byte>(type: "tinyint", nullable: false),
                Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CounterpartLineNumber = table.Column<int>(type: "int", nullable: true),
                Memo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashFlowAllocations", x => x.Id);
                table.CheckConstraint("CK_CashFlowAllocations_Amount_Positive", "[Amount] > 0");
                table.CheckConstraint("CK_CashFlowAllocations_Activity_Valid", "[Activity] IN (1, 2, 3)");
                table.ForeignKey("FK_CashFlowAllocations_CashFlowAllocationSets_AllocationSetId",
                    x => x.AllocationSetId, "CashFlowAllocationSets", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CashFlowAllocations_AllocationSetId",
            table: "CashFlowAllocations",
            column: "AllocationSetId");

        // The composite FK guarantees that the selected source line exists in the
        // referenced immutable JournalEntry, not merely that its header exists.
        migrationBuilder.AddForeignKey(
            name: "FK_CashFlowAllocationSets_JournalLines_SourceJournalEntryId_SourceLineNumber",
            table: "CashFlowAllocationSets",
            columns: new[] { "SourceJournalEntryId", "SourceLineNumber" },
            principalTable: "JournalLines",
            principalColumns: new[] { "JournalEntryId", "LineNumber" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CashFlowAllocations");
        migrationBuilder.DropTable(name: "CashFlowAllocationSets");
    }
}
