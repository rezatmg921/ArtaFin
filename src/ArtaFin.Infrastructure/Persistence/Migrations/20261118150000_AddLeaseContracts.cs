using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261118150000_AddLeaseContracts")]
public sealed class AddLeaseContracts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LeaseContracts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CommencementDate = table.Column<DateOnly>(type: "date", nullable: false),
                FirstPaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                ContractEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                InitialLiabilityAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                EffectivePeriodicRate = table.Column<decimal>(type: "decimal(19,10)", precision: 19, scale: 10, nullable: false),
                PaymentFrequency = table.Column<byte>(type: "tinyint", nullable: false),
                PaymentTiming = table.Column<byte>(type: "tinyint", nullable: false),
                LeaseLiabilityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InterestExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PaymentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<byte>(type: "tinyint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaseContracts", x => x.Id);
                table.ForeignKey("FK_LeaseContracts_FinancialBooks_FinancialBookId",
                    x => x.FinancialBookId, "FinancialBooks", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_LeaseContracts_Accounts_FinancialBookId_LeaseLiabilityAccountId",
                    x => new { x.FinancialBookId, x.LeaseLiabilityAccountId },
                    "Accounts", new[] { "FinancialBookId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_LeaseContracts_Accounts_FinancialBookId_InterestExpenseAccountId",
                    x => new { x.FinancialBookId, x.InterestExpenseAccountId },
                    "Accounts", new[] { "FinancialBookId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_LeaseContracts_Accounts_FinancialBookId_PaymentAccountId",
                    x => new { x.FinancialBookId, x.PaymentAccountId },
                    "Accounts", new[] { "FinancialBookId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_LeaseContracts_InitialLiability_Positive", "[InitialLiabilityAmount] > 0");
                table.CheckConstraint("CK_LeaseContracts_EffectivePeriodicRate_NonNegative", "[EffectivePeriodicRate] >= 0");
                table.CheckConstraint("CK_LeaseContracts_PaymentFrequency_Valid", "[PaymentFrequency] IN (1, 3, 6, 12)");
                table.CheckConstraint("CK_LeaseContracts_PaymentTiming_Valid", "[PaymentTiming] IN (1, 2)");
                table.CheckConstraint("CK_LeaseContracts_Status_Valid", "[Status] IN (1, 2, 3)");
                table.CheckConstraint("CK_LeaseContracts_PaymentDates_Valid",
                    "([PaymentTiming] = 1 AND [FirstPaymentDate] >= [CommencementDate]) OR ([PaymentTiming] = 2 AND [FirstPaymentDate] > [CommencementDate])");
                table.CheckConstraint("CK_LeaseContracts_EndDate_Valid", "[ContractEndDate] >= [FirstPaymentDate]");
                table.CheckConstraint("CK_LeaseContracts_Accounts_Distinct",
                    "[LeaseLiabilityAccountId] <> [InterestExpenseAccountId] AND [LeaseLiabilityAccountId] <> [PaymentAccountId] AND [InterestExpenseAccountId] <> [PaymentAccountId]");
            });

        migrationBuilder.CreateIndex(
            name: "UX_LeaseContracts_Book_ContractNumber",
            table: "LeaseContracts",
            columns: new[] { "FinancialBookId", "ContractNumber" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_LeaseContracts_Book_Status_EndDate",
            table: "LeaseContracts",
            columns: new[] { "FinancialBookId", "Status", "ContractEndDate" });
        migrationBuilder.CreateIndex(
            name: "IX_LeaseContracts_FinancialBookId_LeaseLiabilityAccountId",
            table: "LeaseContracts",
            columns: new[] { "FinancialBookId", "LeaseLiabilityAccountId" });
        migrationBuilder.CreateIndex(
            name: "IX_LeaseContracts_FinancialBookId_InterestExpenseAccountId",
            table: "LeaseContracts",
            columns: new[] { "FinancialBookId", "InterestExpenseAccountId" });
        migrationBuilder.CreateIndex(
            name: "IX_LeaseContracts_FinancialBookId_PaymentAccountId",
            table: "LeaseContracts",
            columns: new[] { "FinancialBookId", "PaymentAccountId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LeaseContracts");
    }
}
