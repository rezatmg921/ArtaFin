using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

public sealed partial class AddJournalEntryIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_JournalEntries_FinancialBookId",
            table: "JournalEntries");

        migrationBuilder.AddColumn<string>(
            name: "IdempotencyKey",
            table: "JournalEntries",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "IdempotencyUserId",
            table: "JournalEntries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RequestHash",
            table: "JournalEntries",
            type: "char(64)",
            fixedLength: true,
            unicode: false,
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_JournalEntries_IdempotencyMetadata_Consistent",
            table: "JournalEntries",
            sql: "([IdempotencyKey] IS NULL AND [IdempotencyUserId] IS NULL AND [RequestHash] IS NULL) OR ([IdempotencyKey] IS NOT NULL AND [IdempotencyUserId] IS NOT NULL AND [RequestHash] IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "UX_JournalEntries_Idempotency",
            table: "JournalEntries",
            columns: new[] { "FinancialBookId", "IdempotencyUserId", "IdempotencyKey" },
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_JournalEntries_Idempotency",
            table: "JournalEntries");

        migrationBuilder.DropCheckConstraint(
            name: "CK_JournalEntries_IdempotencyMetadata_Consistent",
            table: "JournalEntries");

        migrationBuilder.DropColumn(name: "IdempotencyKey", table: "JournalEntries");
        migrationBuilder.DropColumn(name: "IdempotencyUserId", table: "JournalEntries");
        migrationBuilder.DropColumn(name: "RequestHash", table: "JournalEntries");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_FinancialBookId",
            table: "JournalEntries",
            column: "FinancialBookId");
    }
}
