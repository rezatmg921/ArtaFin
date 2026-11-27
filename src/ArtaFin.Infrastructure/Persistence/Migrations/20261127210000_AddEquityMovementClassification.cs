using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ArtaFin.Infrastructure.Persistence;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261127210000_AddEquityMovementClassification")]
public partial class AddEquityMovementClassification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "EquityMovementType",
            table: "JournalLines",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_JournalLines_EquityMovementType_Valid",
            table: "JournalLines",
            sql: "[EquityMovementType] IN (0, 1, 2, 3, 4)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_JournalLines_EquityMovementType_Valid",
            table: "JournalLines");

        migrationBuilder.DropColumn(
            name: "EquityMovementType",
            table: "JournalLines");
    }
}
