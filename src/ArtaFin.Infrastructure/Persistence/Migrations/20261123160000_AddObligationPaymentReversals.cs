using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261123160000_AddObligationPaymentReversals")]
public sealed class AddObligationPaymentReversals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ReversedAt", table: "FinancialObligationPayments",
            type: "datetimeoffset(7)", nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "ReversedBy", table: "FinancialObligationPayments",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "ReversalJournalEntryId", table: "FinancialObligationPayments",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReversalReason", table: "FinancialObligationPayments",
            type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReversalIdempotencyKey", table: "FinancialObligationPayments",
            type: "nvarchar(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReversalRequestHash", table: "FinancialObligationPayments",
            type: "char(64)", fixedLength: true, unicode: false, maxLength: 64, nullable: true);

        migrationBuilder.CreateIndex(
            name: "UX_FinancialObligationPayments_ReversalJournalEntryId",
            table: "FinancialObligationPayments",
            column: "ReversalJournalEntryId",
            unique: true,
            filter: "[ReversalJournalEntryId] IS NOT NULL");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FinancialObligationPayments_ReversalMetadata_Consistent",
            table: "FinancialObligationPayments",
            sql: "([ReversedAt] IS NULL AND [ReversedBy] IS NULL AND [ReversalJournalEntryId] IS NULL AND [ReversalReason] IS NULL AND [ReversalIdempotencyKey] IS NULL AND [ReversalRequestHash] IS NULL) OR ([ReversedAt] IS NOT NULL AND [ReversedBy] IS NOT NULL AND [ReversalJournalEntryId] IS NOT NULL AND [ReversalReason] IS NOT NULL AND [ReversalIdempotencyKey] IS NOT NULL AND [ReversalRequestHash] IS NOT NULL)");

        migrationBuilder.AddForeignKey(
            name: "FK_FinancialObligationPayments_JournalEntries_ReversalJournalEntryId",
            table: "FinancialObligationPayments",
            column: "ReversalJournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_FinancialObligationPayments_JournalEntries_ReversalJournalEntryId",
            table: "FinancialObligationPayments");
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinancialObligationPayments_ReversalMetadata_Consistent",
            table: "FinancialObligationPayments");
        migrationBuilder.DropIndex(
            name: "UX_FinancialObligationPayments_ReversalJournalEntryId",
            table: "FinancialObligationPayments");

        migrationBuilder.DropColumn(name: "ReversedAt", table: "FinancialObligationPayments");
        migrationBuilder.DropColumn(name: "ReversedBy", table: "FinancialObligationPayments");
        migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: "FinancialObligationPayments");
        migrationBuilder.DropColumn(name: "ReversalReason", table: "FinancialObligationPayments");
        migrationBuilder.DropColumn(name: "ReversalIdempotencyKey", table: "FinancialObligationPayments");
        migrationBuilder.DropColumn(name: "ReversalRequestHash", table: "FinancialObligationPayments");
    }
}
