using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261123130000_AlignFinancialObligationModel")]
public sealed class AlignFinancialObligationModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_FinancialObligations_FinancialBookId_ControlAccountId",
            table: "FinancialObligations",
            columns: new[] { "FinancialBookId", "ControlAccountId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FinancialObligations_FinancialBookId_ControlAccountId",
            table: "FinancialObligations");
    }
}
