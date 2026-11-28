using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261118160000_AddLeasePaymentSchedules")]
public sealed class AddLeasePaymentSchedules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_LeaseContracts_FinancialBookId_Id",
            table: "LeaseContracts",
            columns: new[] { "FinancialBookId", "Id" });

        migrationBuilder.CreateTable(
            name: "LeasePaymentSchedules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LeaseContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                SupersedesScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<byte>(type: "tinyint", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                FinalizedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeasePaymentSchedules", x => x.Id);
                table.ForeignKey("FK_LeasePaymentSchedules_FinancialBooks_FinancialBookId",
                    x => x.FinancialBookId, "FinancialBooks", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_LeasePaymentSchedules_LeaseContracts_FinancialBookId_LeaseContractId",
                    x => new { x.FinancialBookId, x.LeaseContractId },
                    "LeaseContracts", new[] { "FinancialBookId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_LeasePaymentSchedules_LeasePaymentSchedules_SupersedesScheduleId",
                    x => x.SupersedesScheduleId, "LeasePaymentSchedules", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_LeasePaymentSchedules_Version_Positive", "[Version] > 0");
                table.CheckConstraint("CK_LeasePaymentSchedules_Status_Valid", "[Status] IN (1, 2, 3)");
                table.CheckConstraint("CK_LeasePaymentSchedules_Version_Supersedes",
                    "([Version] = 1 AND [SupersedesScheduleId] IS NULL) OR ([Version] > 1 AND [SupersedesScheduleId] IS NOT NULL)");
                table.CheckConstraint("CK_LeasePaymentSchedules_Finalization_Consistent",
                    "([Status] = 1 AND [FinalizedBy] IS NULL AND [FinalizedAt] IS NULL) OR ([Status] IN (2, 3) AND [FinalizedBy] IS NOT NULL AND [FinalizedAt] IS NOT NULL)");
            });

        migrationBuilder.CreateTable(
            name: "LeasePaymentScheduleLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PeriodNumber = table.Column<int>(type: "int", nullable: false),
                PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                PaymentAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                EffectivePeriodicRate = table.Column<decimal>(type: "decimal(19,10)", precision: 19, scale: 10, nullable: false),
                IsPaid = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PaidBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeasePaymentScheduleLines", x => x.Id);
                table.ForeignKey("FK_LeasePaymentScheduleLines_LeasePaymentSchedules_ScheduleId",
                    x => x.ScheduleId, "LeasePaymentSchedules", "Id", onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("CK_LeasePaymentScheduleLines_Period_Positive", "[PeriodNumber] > 0");
                table.CheckConstraint("CK_LeasePaymentScheduleLines_Amount_NonNegative", "[PaymentAmount] >= 0");
                table.CheckConstraint("CK_LeasePaymentScheduleLines_Rate_Valid",
                    "[EffectivePeriodicRate] BETWEEN 0 AND 999999999.9999999999");
                table.CheckConstraint("CK_LeasePaymentScheduleLines_Payment_Consistent",
                    "([IsPaid] = 0 AND [JournalEntryId] IS NULL AND [PaidBy] IS NULL AND [PaidAt] IS NULL) OR ([IsPaid] = 1 AND [JournalEntryId] IS NOT NULL AND [PaidBy] IS NOT NULL AND [PaidAt] IS NOT NULL)");
            });

        migrationBuilder.CreateIndex("IX_LeasePaymentSchedules_SupersedesScheduleId",
            "LeasePaymentSchedules", "SupersedesScheduleId");
        migrationBuilder.CreateIndex("UX_LeasePaymentSchedules_Contract_Version",
            "LeasePaymentSchedules", new[] { "LeaseContractId", "Version" }, unique: true);
        migrationBuilder.CreateIndex("IX_LeasePaymentSchedules_Book_Contract_Status",
            "LeasePaymentSchedules", new[] { "FinancialBookId", "LeaseContractId", "Status" });
        migrationBuilder.CreateIndex("UX_LeasePaymentScheduleLines_Schedule_Period",
            "LeasePaymentScheduleLines", new[] { "ScheduleId", "PeriodNumber" }, unique: true);
        migrationBuilder.CreateIndex("UX_LeasePaymentScheduleLines_Schedule_Date",
            "LeasePaymentScheduleLines", new[] { "ScheduleId", "PaymentDate" }, unique: true);
        migrationBuilder.CreateIndex("UX_LeasePaymentScheduleLines_JournalEntry",
            "LeasePaymentScheduleLines", "JournalEntryId",
            unique: true, filter: "[JournalEntryId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LeasePaymentScheduleLines");
        migrationBuilder.DropTable(name: "LeasePaymentSchedules");
        migrationBuilder.DropUniqueConstraint(
            name: "AK_LeaseContracts_FinancialBookId_Id",
            table: "LeaseContracts");
    }
}
