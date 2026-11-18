using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseInstallmentPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "LeasePaymentScheduleLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "LeasePaymentScheduleLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "LeasePaymentScheduleLines",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidBy",
                table: "LeasePaymentScheduleLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_LeasePaymentScheduleLines_JournalEntry",
                table: "LeasePaymentScheduleLines",
                column: "JournalEntryId",
                unique: true,
                filter: "[JournalEntryId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeasePaymentScheduleLines_Payment_Consistent",
                table: "LeasePaymentScheduleLines",
                sql: "([IsPaid] = 0 AND [JournalEntryId] IS NULL AND [PaidBy] IS NULL AND [PaidAt] IS NULL) OR ([IsPaid] = 1 AND [JournalEntryId] IS NOT NULL AND [PaidBy] IS NOT NULL AND [PaidAt] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LeasePaymentScheduleLines_JournalEntry",
                table: "LeasePaymentScheduleLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeasePaymentScheduleLines_Payment_Consistent",
                table: "LeasePaymentScheduleLines");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "LeasePaymentScheduleLines");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "LeasePaymentScheduleLines");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "LeasePaymentScheduleLines");

            migrationBuilder.DropColumn(
                name: "PaidBy",
                table: "LeasePaymentScheduleLines");
        }
    }
}