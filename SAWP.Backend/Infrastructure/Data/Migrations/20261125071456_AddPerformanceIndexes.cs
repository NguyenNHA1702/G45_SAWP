using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolCalendarDays_SchoolYearId",
                table: "SchoolCalendarDays");

            migrationBuilder.DropIndex(
                name: "IX_PlacementPlans_SchoolYearId",
                table: "PlacementPlans");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt_Id",
                table: "Users",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_CreatedAt",
                table: "Students",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Status_FullName",
                table: "Students",
                columns: new[] { "Status", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_IsActive_StartDate",
                table: "SchoolYears",
                columns: new[] { "IsActive", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolCalendarDays_SchoolYearId_Date",
                table: "SchoolCalendarDays",
                columns: new[] { "SchoolYearId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementPlans_SchoolYearId_Grade_CreatedAt",
                table: "PlacementPlans",
                columns: new[] { "SchoolYearId", "Grade", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_UserId_FullName",
                table: "Guardians",
                columns: new[] { "UserId", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassAssignments_IsActive_ClassId",
                table: "ClassAssignments",
                columns: new[] { "IsActive", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassAssignments_StudentId_IsActive",
                table: "ClassAssignments",
                columns: new[] { "StudentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeRequests_RequestType_SubmissionDate",
                table: "AdministrativeRequests",
                columns: new[] { "RequestType", "SubmissionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedAt_Id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Students_CreatedAt",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_Status_FullName",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_SchoolYears_IsActive_StartDate",
                table: "SchoolYears");

            migrationBuilder.DropIndex(
                name: "IX_SchoolCalendarDays_SchoolYearId_Date",
                table: "SchoolCalendarDays");

            migrationBuilder.DropIndex(
                name: "IX_PlacementPlans_SchoolYearId_Grade_CreatedAt",
                table: "PlacementPlans");

            migrationBuilder.DropIndex(
                name: "IX_Guardians_UserId_FullName",
                table: "Guardians");

            migrationBuilder.DropIndex(
                name: "IX_ClassAssignments_IsActive_ClassId",
                table: "ClassAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ClassAssignments_StudentId_IsActive",
                table: "ClassAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AdministrativeRequests_RequestType_SubmissionDate",
                table: "AdministrativeRequests");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolCalendarDays_SchoolYearId",
                table: "SchoolCalendarDays",
                column: "SchoolYearId");

            migrationBuilder.CreateIndex(
                name: "IX_PlacementPlans_SchoolYearId",
                table: "PlacementPlans",
                column: "SchoolYearId");
        }
    }
}
