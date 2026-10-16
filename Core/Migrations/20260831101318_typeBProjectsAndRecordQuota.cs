using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class typeBProjectsAndRecordQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDate",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedImageUrl",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Nullable because ApplicationUser sits in a TPH hierarchy under
            // IdentityUser, same as PersonalBalance and the rest. The CLR
            // property is a plain int, so existing rows are backfilled rather
            // than left NULL for EF to choke on when it materialises a user.
            migrationBuilder.AddColumn<int>(
                name: "RecordQuota",
                table: "AspNetUsers",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE AspNetUsers SET RecordQuota = 0 WHERE RecordQuota IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RelatedImageUrl",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RecordQuota",
                table: "AspNetUsers");
        }
    }
}
