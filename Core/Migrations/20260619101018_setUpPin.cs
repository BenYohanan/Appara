using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class setUpPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientCode",
                table: "Withdrawals");

            migrationBuilder.AddColumn<string>(
                name: "TransactionPin",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TransactionPinSet",
                table: "AspNetUsers",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionPin",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TransactionPinSet",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "RecipientCode",
                table: "Withdrawals",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
