using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardingQuotaAndIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWaitlisted",
                table: "LunchRegistrations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BoardingIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    IncidentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardingIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardingIncidents_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BoardingIncidents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardingIncidents_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LunchMonthlyQuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Quota = table.Column<int>(type: "int", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunchMonthlyQuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LunchMonthlyQuotas_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardingIncidents_ClassId",
                table: "BoardingIncidents",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardingIncidents_RecordedByUserId",
                table: "BoardingIncidents",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardingIncidents_StudentId",
                table: "BoardingIncidents",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LunchMonthlyQuotas_Month",
                table: "LunchMonthlyQuotas",
                column: "Month",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LunchMonthlyQuotas_UpdatedByUserId",
                table: "LunchMonthlyQuotas",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardingIncidents");

            migrationBuilder.DropTable(
                name: "LunchMonthlyQuotas");

            migrationBuilder.DropColumn(
                name: "IsWaitlisted",
                table: "LunchRegistrations");
        }
    }
}
