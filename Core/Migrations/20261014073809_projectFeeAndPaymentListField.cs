using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class projectFeeAndPaymentListField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccountFeePaidAt",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountFeePaidFrom",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountFeeStatus",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaymentListFieldId",
                table: "Projects",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountFeePaidAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AccountFeePaidFrom",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AccountFeeStatus",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PaymentListFieldId",
                table: "Projects");
        }
    }
}
