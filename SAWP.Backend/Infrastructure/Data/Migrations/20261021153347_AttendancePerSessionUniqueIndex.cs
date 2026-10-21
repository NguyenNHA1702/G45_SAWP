using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttendancePerSessionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddLeaveRequestWorkflowAndSession added Session with default 0, which is not a valid
            // enum value. Rows recorded before per-session attendance were whole-day records:
            // treat them as the Morning session, and old leave requests as FullDay.
            migrationBuilder.Sql("UPDATE [AttendanceRecords] SET [Session] = 1 WHERE [Session] = 0;");
            migrationBuilder.Sql("UPDATE [LeaveRequests] SET [Session] = 3 WHERE [Session] = 0;");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_StudentId_Date",
                table: "AttendanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentId_Date_Session",
                table: "AttendanceRecords",
                columns: new[] { "StudentId", "Date", "Session" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_StudentId_Date_Session",
                table: "AttendanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentId_Date",
                table: "AttendanceRecords",
                columns: new[] { "StudentId", "Date" },
                unique: true);
        }
    }
}
