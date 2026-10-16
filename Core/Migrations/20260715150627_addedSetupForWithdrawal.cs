using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class addedSetupForWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeWithdrawalsRemaining",
                table: "Wallets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BeadsUsed",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformAbsorbedCharge",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProjectBalanceFeeDeduction",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortfallAmount",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BeadsBalance",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeWithdrawalsRemaining",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "BeadsUsed",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PlatformAbsorbedCharge",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ProjectBalanceFeeDeduction",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ShortfallAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BeadsBalance",
                table: "AspNetUsers");
        }
    }
}
