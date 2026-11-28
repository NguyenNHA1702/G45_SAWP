using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsenceContactNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_Users_AbsenceNoticeSentByUserId",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_AbsenceNoticeSentByUserId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "AbsenceNoticeSentAt",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "AbsenceNoticeSentByUserId",
                table: "AttendanceRecords");

            migrationBuilder.CreateTable(
                name: "AbsenceContactNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: false),
                    ContactedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededByUserId = table.Column<int>(type: "int", nullable: true),
                    SupersedeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsenceContactNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbsenceContactNotes_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbsenceContactNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceContactNotes_Users_SupersededByUserId",
                        column: x => x.SupersededByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceContactNotes_AttendanceRecordId",
                table: "AbsenceContactNotes",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceContactNotes_CreatedByUserId",
                table: "AbsenceContactNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceContactNotes_SupersededByUserId",
                table: "AbsenceContactNotes",
                column: "SupersededByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbsenceContactNotes");

            migrationBuilder.AddColumn<DateTime>(
                name: "AbsenceNoticeSentAt",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AbsenceNoticeSentByUserId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AbsenceNoticeSentByUserId",
                table: "AttendanceRecords",
                column: "AbsenceNoticeSentByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_Users_AbsenceNoticeSentByUserId",
                table: "AttendanceRecords",
                column: "AbsenceNoticeSentByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
