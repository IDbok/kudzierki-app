using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegisterClosings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashRegisterClosings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CashBalanceFact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TerminalIncomeFact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DayBefore = table.Column<DateOnly>(type: "date", nullable: true),
                    CashBalanceDayBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashIncomeAltegio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransferIncomeAltegio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TerminalIncomeAltegio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashSpendingAdmin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InCashTransfer = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutCashTransfer = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisterClosings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisterClosings_Date",
                table: "CashRegisterClosings",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashRegisterClosings");
        }
    }
}
