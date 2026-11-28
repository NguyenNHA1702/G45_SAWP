using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <summary>
    /// OVR-10 (BR-62): SAWP never notifies a Parent / Guardian of an absence. Deletes the absence notices
    /// ("ATTENDANCE_ABSENT") and their corrections ("ATTENDANCE_ABSENT_RETRACT") that the removed SSAWP-150
    /// feature sent to Parents, so they no longer appear in the notification bell.
    /// </summary>
    public partial class RemoveAbsenceNoticeNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [Notifications] WHERE [Type] IN (N'ATTENDANCE_ABSENT', N'ATTENDANCE_ABSENT_RETRACT');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The deleted notifications cannot be restored.
        }
    }
}
