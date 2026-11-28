using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261123100000_AddContacts")]
public sealed class AddContacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Contacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<byte>(type: "tinyint", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                Phone = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Contacts", x => x.Id);
                table.CheckConstraint("CK_Contacts_Type_Valid", "[Type] IN (1, 2)");
                table.ForeignKey(
                    name: "FK_Contacts_FinancialBooks_FinancialBookId",
                    column: x => x.FinancialBookId,
                    principalTable: "FinancialBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_FinancialBookId_ArchivedAt",
            table: "Contacts",
            columns: new[] { "FinancialBookId", "ArchivedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_FinancialBookId_Email",
            table: "Contacts",
            columns: new[] { "FinancialBookId", "Email" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Contacts");
    }
}
