using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261123150000_AddFinancialObligationPayments")]
public sealed class AddFinancialObligationPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_FinancialObligations_Book_Id",
            table: "FinancialObligations",
            columns: new[] { "FinancialBookId", "Id" });

        migrationBuilder.AddUniqueConstraint(
            name: "AK_FinancialObligationInstallments_Obligation_Id",
            table: "FinancialObligationInstallments",
            columns: new[] { "FinancialObligationId", "Id" });

        migrationBuilder.AddUniqueConstraint(
            name: "AK_JournalEntries_FinancialBookId_Id",
            table: "JournalEntries",
            columns: new[] { "FinancialBookId", "Id" });

        migrationBuilder.CreateTable(
            name: "FinancialObligationPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "char(3)", fixedLength: true, unicode: false, maxLength: 3, nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                RequestHash = table.Column<string>(type: "char(64)", fixedLength: true, unicode: false, maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialObligationPayments", x => x.Id);
                table.UniqueConstraint("AK_FinancialObligationPayments_Id_ObligationId", x => new { x.Id, x.FinancialObligationId });
                table.CheckConstraint("CK_FinancialObligationPayments_Amount_Positive", "[Amount] > 0");
                table.CheckConstraint("CK_FinancialObligationPayments_IdempotencyMetadata_Valid", "LEN([IdempotencyKey]) BETWEEN 1 AND 128 AND LEN([RequestHash]) = 64");
                table.ForeignKey(
                    name: "FK_FinancialObligationPayments_FinancialObligations_FinancialBookId_FinancialObligationId",
                    columns: x => new { x.FinancialBookId, x.FinancialObligationId },
                    principalTable: "FinancialObligations",
                    principalColumns: new[] { "FinancialBookId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinancialObligationPayments_Accounts_FinancialBookId_CashAccountId",
                    columns: x => new { x.FinancialBookId, x.CashAccountId },
                    principalTable: "Accounts",
                    principalColumns: new[] { "FinancialBookId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinancialObligationPayments_JournalEntries_FinancialBookId_JournalEntryId",
                    columns: x => new { x.FinancialBookId, x.JournalEntryId },
                    principalTable: "JournalEntries",
                    principalColumns: new[] { "FinancialBookId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinancialObligationPaymentAllocations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialObligationPaymentAllocations", x => x.Id);
                table.CheckConstraint("CK_FinancialObligationPaymentAllocations_Amount_Positive", "[Amount] > 0");
                table.ForeignKey(
                    name: "FK_FinancialObligationPaymentAllocations_FinancialObligationPayments_PaymentId_FinancialObligationId",
                    columns: x => new { x.PaymentId, x.FinancialObligationId },
                    principalTable: "FinancialObligationPayments",
                    principalColumns: new[] { "Id", "FinancialObligationId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinancialObligationPaymentAllocations_FinancialObligationInstallments_FinancialObligationId_InstallmentId",
                    columns: x => new { x.FinancialObligationId, x.InstallmentId },
                    principalTable: "FinancialObligationInstallments",
                    principalColumns: new[] { "FinancialObligationId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_FinancialObligationPayments_Idempotency",
            table: "FinancialObligationPayments",
            columns: new[] { "FinancialBookId", "CreatedBy", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinancialObligationPayments_Obligation_Date",
            table: "FinancialObligationPayments",
            columns: new[] { "FinancialBookId", "FinancialObligationId", "PaymentDate", "Id" });

        migrationBuilder.CreateIndex(
            name: "UX_FinancialObligationPayments_Book_Id",
            table: "FinancialObligationPayments",
            columns: new[] { "FinancialBookId", "Id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_FinancialObligationPaymentAllocations_Payment_Installment",
            table: "FinancialObligationPaymentAllocations",
            columns: new[] { "PaymentId", "InstallmentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinancialObligationPaymentAllocations_Obligation_Installment",
            table: "FinancialObligationPaymentAllocations",
            columns: new[] { "FinancialObligationId", "InstallmentId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FinancialObligationPaymentAllocations");
        migrationBuilder.DropTable(name: "FinancialObligationPayments");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_JournalEntries_FinancialBookId_Id",
            table: "JournalEntries");
        migrationBuilder.DropUniqueConstraint(
            name: "AK_FinancialObligationInstallments_Obligation_Id",
            table: "FinancialObligationInstallments");
        migrationBuilder.DropUniqueConstraint(
            name: "AK_FinancialObligations_Book_Id",
            table: "FinancialObligations");
    }
}
