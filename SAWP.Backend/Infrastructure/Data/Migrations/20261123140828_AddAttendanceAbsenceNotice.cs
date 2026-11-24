using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceAbsenceNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
