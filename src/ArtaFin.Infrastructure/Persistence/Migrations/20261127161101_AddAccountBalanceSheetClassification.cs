using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBalanceSheetClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "BalanceSheetClassification",
                table: "Accounts",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_BalanceSheetClassification_EligibleType",
                table: "Accounts",
                sql: "[BalanceSheetClassification] = 0 OR [Type] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_BalanceSheetClassification_Valid",
                table: "Accounts",
                sql: "[BalanceSheetClassification] IN (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_BalanceSheetClassification_EligibleType",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_BalanceSheetClassification_Valid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BalanceSheetClassification",
                table: "Accounts");
        }
    }
}
