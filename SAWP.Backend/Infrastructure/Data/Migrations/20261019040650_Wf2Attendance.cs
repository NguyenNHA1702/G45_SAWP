using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Wf2Attendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_LunchRegistrations_StudentId",
                table: "LunchRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_ClassId",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_StudentId",
                table: "AttendanceRecords");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceKey",
                table: "Notifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "LunchRegistrations",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "LunchRegistrations",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancellationSource",
                table: "LunchRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "LunchRegistrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByUserId",
                table: "LunchRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "AttendanceRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "AttendanceRecords",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "CorrectionReason",
                table: "AttendanceRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeaveRequestId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    SchoolYearId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<byte>(type: "tinyint", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TeacherStaffId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_SchoolYears_SchoolYearId",
                        column: x => x.SchoolYearId,
                        principalTable: "SchoolYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_StaffProfiles_TeacherStaffId",
                        column: x => x.TeacherStaffId,
                        principalTable: "StaffProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReferenceKey",
                table: "Notifications",
                columns: new[] { "UserId", "ReferenceKey" },
                unique: true,
                filter: "[ReferenceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LunchRegistrations_CancelledByUserId",
                table: "LunchRegistrations",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LunchRegistrations_StudentId_Date",
                table: "LunchRegistrations",
                columns: new[] { "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ClassId_Date",
                table: "AttendanceRecords",
                columns: new[] { "ClassId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_LeaveRequestId",
                table: "AttendanceRecords",
                column: "LeaveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentId_Date",
                table: "AttendanceRecords",
                columns: new[] { "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_UpdatedByUserId",
                table: "AttendanceRecords",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_ClassId_DayOfWeek_Period_EffectiveFrom",
                table: "ClassSchedules",
                columns: new[] { "ClassId", "DayOfWeek", "Period", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_SchoolYearId",
                table: "ClassSchedules",
                column: "SchoolYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_TeacherStaffId_DayOfWeek",
                table: "ClassSchedules",
                columns: new[] { "TeacherStaffId", "DayOfWeek" });

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_LeaveRequests_LeaveRequestId",
                table: "AttendanceRecords",
                column: "LeaveRequestId",
                principalTable: "LeaveRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_Users_UpdatedByUserId",
                table: "AttendanceRecords",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LunchRegistrations_Users_CancelledByUserId",
                table: "LunchRegistrations",
                column: "CancelledByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_LeaveRequests_LeaveRequestId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_Users_UpdatedByUserId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LunchRegistrations_Users_CancelledByUserId",
                table: "LunchRegistrations");

            migrationBuilder.DropTable(
                name: "ClassSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_ReferenceKey",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_LunchRegistrations_CancelledByUserId",
                table: "LunchRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_LunchRegistrations_StudentId_Date",
                table: "LunchRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_ClassId_Date",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_LeaveRequestId",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_StudentId_Date",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_UpdatedByUserId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ReferenceKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "LunchRegistrations");

            migrationBuilder.DropColumn(
                name: "CancellationSource",
                table: "LunchRegistrations");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "LunchRegistrations");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "LunchRegistrations");

            migrationBuilder.DropColumn(
                name: "CorrectionReason",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "LeaveRequestId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "AttendanceRecords");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "LunchRegistrations",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "AttendanceRecords",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LunchRegistrations_StudentId",
                table: "LunchRegistrations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ClassId",
                table: "AttendanceRecords",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentId",
                table: "AttendanceRecords",
                column: "StudentId");
        }
    }
}
