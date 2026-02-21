using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAltegioTransactionStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AltegioTransactionRaws",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltegioTransactionRaws", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AltegioTransactionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AltegioCreateDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastChangeDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    AccountTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsCash = table.Column<bool>(type: "bit", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltegioTransactionSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AltegioTransactionRaws_ExternalId_PayloadHash",
                table: "AltegioTransactionRaws",
                columns: new[] { "ExternalId", "PayloadHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AltegioTransactionRaws_FetchedAtUtc",
                table: "AltegioTransactionRaws",
                column: "FetchedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AltegioTransactionSnapshots_ExternalId",
                table: "AltegioTransactionSnapshots",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AltegioTransactionRaws");

            migrationBuilder.DropTable(
                name: "AltegioTransactionSnapshots");
        }
    }
}
