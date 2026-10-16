using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class addedCustomFeildTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCustomFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FieldType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCustomFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCustomFields_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectCustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjectCustomFieldId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCustomFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCustomFieldValues_ProjectCustomFields_ProjectCustomFieldId",
                        column: x => x.ProjectCustomFieldId,
                        principalTable: "ProjectCustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCustomFieldValues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomFields_ProjectId",
                table: "ProjectCustomFields",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomFieldValues_ProjectCustomFieldId",
                table: "ProjectCustomFieldValues",
                column: "ProjectCustomFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomFieldValues_ProjectId",
                table: "ProjectCustomFieldValues",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectCustomFieldValues");

            migrationBuilder.DropTable(
                name: "ProjectCustomFields");
        }
    }
}
