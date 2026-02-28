using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAltegioTransactionSnapshotDeletionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedDetectedAtUtc",
                table: "AltegioTransactionSnapshots",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeletedInSource",
                table: "AltegioTransactionSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedDetectedAtUtc",
                table: "AltegioTransactionSnapshots");

            migrationBuilder.DropColumn(
                name: "IsDeletedInSource",
                table: "AltegioTransactionSnapshots");
        }
    }
}
