using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class modifiedUserTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KycVerified",
                table: "AspNetUsers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycVerifiedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafeHavenIdentityId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafeHavenIdentityType",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KycVerified",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KycVerifiedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SafeHavenIdentityId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SafeHavenIdentityType",
                table: "AspNetUsers");
        }
    }
}
