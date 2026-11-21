using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261118130000_AddFinancingLiabilityClassification")]
public sealed class AddFinancingLiabilityClassification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "FinancingLiabilityType",
            table: "Accounts",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Accounts_FinancingLiabilityType_Valid",
            table: "Accounts",
            sql: "[FinancingLiabilityType] IN (0, 1, 2, 3)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Accounts_FinancingLiabilityType_LiabilityOnly",
            table: "Accounts",
            sql: "[FinancingLiabilityType] = 0 OR [Type] = 2");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Accounts_FinancingLiabilityType_LiabilityOnly",
            table: "Accounts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_Accounts_FinancingLiabilityType_Valid",
            table: "Accounts");
        migrationBuilder.DropColumn(name: "FinancingLiabilityType", table: "Accounts");
    }
}
