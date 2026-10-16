using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class tblContributionModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "ProjectSupporters");

            migrationBuilder.RenameColumn(
                name: "PaystackReference",
                table: "Contributions",
                newName: "Reference");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedDate",
                table: "Contributions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "Contributions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProofOfPayment",
                table: "Contributions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedDate",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "ProofOfPayment",
                table: "Contributions");

            migrationBuilder.RenameColumn(
                name: "Reference",
                table: "Contributions",
                newName: "PaystackReference");

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

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "ProjectSupporters",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
