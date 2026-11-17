using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261117130000_AddCashFlowAccountClassification")]
public sealed class AddCashFlowAccountClassification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "CashFlowAccountType",
            table: "Accounts",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<int>(
            name: "CashEquivalentMaturityDays",
            table: "Accounts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "CashEquivalentRiskInsignificant",
            table: "Accounts",
            type: "bit",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Accounts_CashFlowClassification_Valid",
            table: "Accounts",
            sql: "([CashFlowAccountType] = 0 AND [CashEquivalentMaturityDays] IS NULL AND [CashEquivalentRiskInsignificant] IS NULL) OR ([CashFlowAccountType] = 1 AND [CashEquivalentMaturityDays] IS NULL AND [CashEquivalentRiskInsignificant] IS NULL) OR ([CashFlowAccountType] = 2 AND [CashEquivalentMaturityDays] BETWEEN 0 AND 90 AND [CashEquivalentRiskInsignificant] = 1)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Accounts_CashFlowClassification_AssetOnly",
            table: "Accounts",
            sql: "[CashFlowAccountType] = 0 OR [Type] = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Accounts_CashFlowClassification_Valid",
            table: "Accounts");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Accounts_CashFlowClassification_AssetOnly",
            table: "Accounts");

        migrationBuilder.DropColumn(name: "CashFlowAccountType", table: "Accounts");
        migrationBuilder.DropColumn(name: "CashEquivalentMaturityDays", table: "Accounts");
        migrationBuilder.DropColumn(name: "CashEquivalentRiskInsignificant", table: "Accounts");
    }
}
