using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261116120000_AddAccountBalancePolicy")]
public sealed class AddAccountBalancePolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "BalancePolicy",
            table: "Accounts",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)1);

        migrationBuilder.Sql(
            "UPDATE [Accounts] SET [BalancePolicy] = 2 WHERE [Type] <> 1;");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Accounts_BalancePolicy_Valid",
            table: "Accounts",
            sql: "[BalancePolicy] IN (1, 2)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BalancePolicy",
            table: "Accounts");
    }
}
