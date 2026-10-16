using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class addedVirtalAccountSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Contributions");

            migrationBuilder.RenameColumn(
                name: "Reference",
                table: "Contributions",
                newName: "VirtualAccountNumber");

            migrationBuilder.RenameColumn(
                name: "ProofOfPayment",
                table: "Contributions",
                newName: "VirtualAccountId");

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "Contributions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "Contributions");

            migrationBuilder.RenameColumn(
                name: "VirtualAccountNumber",
                table: "Contributions",
                newName: "Reference");

            migrationBuilder.RenameColumn(
                name: "VirtualAccountId",
                table: "Contributions",
                newName: "ProofOfPayment");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Contributions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
