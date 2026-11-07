using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261107170000_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinancialBooks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                BaseCurrency = table.Column<string>(type: "char(3)", fixedLength: true, maxLength: 3, nullable: false)
                    .Annotation("SqlServer: IsFixedLength", true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialBooks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Type = table.Column<byte>(type: "tinyint", nullable: false),
                NormalBalance = table.Column<byte>(type: "tinyint", nullable: false),
                Status = table.Column<byte>(type: "tinyint", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                DeactivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Accounts", x => x.Id);
                table.UniqueConstraint("AK_Accounts_FinancialBookId_Id", x => new { x.FinancialBookId, x.Id });
                table.ForeignKey(
                    name: "FK_Accounts_FinancialBooks_FinancialBookId",
                    column: x => x.FinancialBookId,
                    principalTable: "FinancialBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_Accounts_Type_Valid", "[Type] IN (1, 2, 3, 4, 5)");
                table.CheckConstraint("CK_Accounts_NormalBalance_Valid", "[NormalBalance] IN (1, 2)");
                table.CheckConstraint("CK_Accounts_Status_Valid", "[Status] IN (1, 2)");
            });

        migrationBuilder.CreateTable(
            name: "JournalEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JournalEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_JournalEntries_FinancialBooks_FinancialBookId",
                    column: x => x.FinancialBookId,
                    principalTable: "FinancialBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "JournalLines",
            columns: table => new
            {
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LineNumber = table.Column<int>(type: "int", nullable: false),
                FinancialBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Side = table.Column<byte>(type: "tinyint", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "char(3)", fixedLength: true, maxLength: 3, nullable: false)
                    .Annotation("SqlServer: IsFixedLength", true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JournalLines", x => new { x.JournalEntryId, x.LineNumber });
                table.ForeignKey(
                    name: "FK_JournalLines_Accounts_FinancialBookId_AccountId",
                    columns: x => new { x.FinancialBookId, x.AccountId },
                    principalTable: "Accounts",
                    principalColumns: new[] { "FinancialBookId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_JournalLines_JournalEntries_JournalEntryId",
                    column: x => x.JournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("CK_JournalLines_Amount_Positive", "[Amount] > 0");
                table.CheckConstraint("CK_JournalLines_Side_Valid", "[Side] IN (1, 2)");
            });

        migrationBuilder.CreateIndex(
            name: "UX_Accounts_FinancialBookId_Code",
            table: "Accounts",
            columns: new[] { "FinancialBookId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinancialBooks_OwnerId",
            table: "FinancialBooks",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_FinancialBookId",
            table: "JournalEntries",
            column: "FinancialBookId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalLines_FinancialBookId_AccountId",
            table: "JournalLines",
            columns: new[] { "FinancialBookId", "AccountId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "JournalLines");
        migrationBuilder.DropTable(name: "Accounts");
        migrationBuilder.DropTable(name: "JournalEntries");
        migrationBuilder.DropTable(name: "FinancialBooks");
    }
}
