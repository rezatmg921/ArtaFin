using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryReversalConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversesJournalEntryId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries",
                column: "ReversesJournalEntryId",
                unique: true,
                filter: "[ReversesJournalEntryId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries",
                column: "ReversesJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "UX_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReversesJournalEntryId",
                table: "JournalEntries");
        }
    }
}