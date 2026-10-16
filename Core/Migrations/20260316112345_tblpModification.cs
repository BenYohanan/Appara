using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class tblpModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentApprovedDate",
                table: "ProjectSupporters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "ProjectSupporters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentSubmittedDate",
                table: "ProjectSupporters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofOfPayment",
                table: "ProjectSupporters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentApprovedDate",
                table: "ProjectSupporters");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "ProjectSupporters");

            migrationBuilder.DropColumn(
                name: "PaymentSubmittedDate",
                table: "ProjectSupporters");

            migrationBuilder.DropColumn(
                name: "ProofOfPayment",
                table: "ProjectSupporters");
        }
    }
}
