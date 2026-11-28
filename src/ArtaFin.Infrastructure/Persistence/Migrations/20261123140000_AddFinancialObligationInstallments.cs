using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261123140000_AddFinancialObligationInstallments")]
public sealed class AddFinancialObligationInstallments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinancialObligationInstallments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialObligationInstallments", x => x.Id);
                table.CheckConstraint("CK_FinancialObligationInstallments_Number_Positive", "[InstallmentNumber] > 0");
                table.CheckConstraint("CK_FinancialObligationInstallments_Amount_Positive", "[Amount] > 0");
                table.ForeignKey(
                    name: "FK_FinancialObligationInstallments_FinancialObligations_FinancialObligationId",
                    column: x => x.FinancialObligationId,
                    principalTable: "FinancialObligations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_FinancialObligationInstallments_Obligation_Number",
            table: "FinancialObligationInstallments",
            columns: new[] { "FinancialObligationId", "InstallmentNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_FinancialObligationInstallments_Obligation_DueDate",
            table: "FinancialObligationInstallments",
            columns: new[] { "FinancialObligationId", "DueDate" },
            unique: true);

        migrationBuilder.Sql("""
            INSERT INTO [FinancialObligationInstallments]
                ([Id], [FinancialObligationId], [InstallmentNumber], [DueDate], [Amount])
            SELECT NEWID(), [Id], 1, [DueDate], [OriginalAmount]
            FROM [FinancialObligations];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FinancialObligationInstallments");
    }
}
