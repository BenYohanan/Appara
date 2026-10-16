using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class addedCustomFeildTblModify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectSupporterId",
                table: "ProjectCustomFieldValues",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomFieldValues_ProjectSupporterId",
                table: "ProjectCustomFieldValues",
                column: "ProjectSupporterId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCustomFieldValues_ProjectSupporters_ProjectSupporterId",
                table: "ProjectCustomFieldValues",
                column: "ProjectSupporterId",
                principalTable: "ProjectSupporters",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCustomFieldValues_ProjectSupporters_ProjectSupporterId",
                table: "ProjectCustomFieldValues");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomFieldValues_ProjectSupporterId",
                table: "ProjectCustomFieldValues");

            migrationBuilder.DropColumn(
                name: "ProjectSupporterId",
                table: "ProjectCustomFieldValues");
        }
    }
}
