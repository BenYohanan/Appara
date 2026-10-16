using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class beadAndPersonalAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonalAccountBank",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalAccountNumber",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalBalance",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalHoldersName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalSafeHavenAccountId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BeadTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ContributionId = table.Column<int>(type: "int", nullable: true),
                    BeadsChange = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeadTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeadTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BeadTransactions_Contributions_ContributionId",
                        column: x => x.ContributionId,
                        principalTable: "Contributions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BeadTransactions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PersonalTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RelatedProjectId = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PersonalTransactions_Projects_RelatedProjectId",
                        column: x => x.RelatedProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeadTransactions_ContributionId",
                table: "BeadTransactions",
                column: "ContributionId");

            migrationBuilder.CreateIndex(
                name: "IX_BeadTransactions_ProjectId",
                table: "BeadTransactions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BeadTransactions_UserId",
                table: "BeadTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalTransactions_RelatedProjectId",
                table: "PersonalTransactions",
                column: "RelatedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalTransactions_UserId",
                table: "PersonalTransactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeadTransactions");

            migrationBuilder.DropTable(
                name: "PersonalTransactions");

            migrationBuilder.DropColumn(
                name: "PersonalAccountBank",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalAccountNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalBalance",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalHoldersName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalSafeHavenAccountId",
                table: "AspNetUsers");
        }
    }
}
