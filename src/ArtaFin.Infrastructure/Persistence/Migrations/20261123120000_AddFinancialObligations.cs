using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261123120000_AddFinancialObligations")]
public sealed class AddFinancialObligations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinancialObligations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ControlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Direction = table.Column<byte>(type: "tinyint", nullable: false),
                Kind = table.Column<byte>(type: "tinyint", nullable: false),
                OriginalAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "char(3)", fixedLength: true, unicode: false, maxLength: 3, nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Status = table.Column<byte>(type: "tinyint", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialObligations", x => x.Id);
                table.CheckConstraint("CK_FinancialObligations_Direction_Valid", "[Direction] IN (1, 2)");
                table.CheckConstraint("CK_FinancialObligations_Kind_Valid", "[Kind] IN (1, 2)");
                table.CheckConstraint("CK_FinancialObligations_Status_Valid", "[Status] IN (1, 2, 3, 4)");
                table.CheckConstraint("CK_FinancialObligations_Amount_Positive", "[OriginalAmount] > 0");
                table.CheckConstraint("CK_FinancialObligations_Dates_Valid", "[DueDate] >= [EffectiveDate]");
                table.ForeignKey(
                    name: "FK_FinancialObligations_FinancialBooks_FinancialBookId",
                    column: x => x.FinancialBookId,
                    principalTable: "FinancialBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinancialObligations_Contacts_ContactId",
                    column: x => x.ContactId,
                    principalTable: "Contacts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinancialObligations_Accounts_FinancialBookId_ControlAccountId",
                    columns: x => new { x.FinancialBookId, x.ControlAccountId },
                    principalTable: "Accounts",
                    principalColumns: new[] { "FinancialBookId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FinancialObligations_Book_Direction_Status_DueDate",
            table: "FinancialObligations",
            columns: new[] { "FinancialBookId", "Direction", "Status", "DueDate" });

        migrationBuilder.CreateIndex(
            name: "IX_FinancialObligations_Book_Contact_EffectiveDate",
            table: "FinancialObligations",
            columns: new[] { "FinancialBookId", "ContactId", "EffectiveDate" });

        migrationBuilder.CreateIndex(
            name: "IX_FinancialObligations_ContactId",
            table: "FinancialObligations",
            column: "ContactId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FinancialObligations");
    }
}
