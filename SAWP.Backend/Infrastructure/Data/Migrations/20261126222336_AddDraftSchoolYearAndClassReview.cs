using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftSchoolYearAndClassReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SchoolYears",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<bool>(
                name: "IsReviewed",
                table: "Classes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Classes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "Classes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "ClassAssignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing years (BR-14, BR-72): the active year stays Active; an inactive year that has ended is Archived,
            // any other inactive year is a Draft, whose assignments are not anyone's current class yet.
            migrationBuilder.Sql(@"
UPDATE [SchoolYears]
SET [Status] = CASE WHEN [IsActive] = 1 THEN N'Active'
                    WHEN [EndDate] < CAST(SYSUTCDATETIME() AS date) THEN N'Archived'
                    ELSE N'Draft' END;

UPDATE ca
SET ca.[IsDraft] = 1, ca.[IsActive] = 0
FROM [ClassAssignments] ca
JOIN [Classes] c ON c.[Id] = ca.[ClassId]
JOIN [SchoolYears] sy ON sy.[Id] = c.[SchoolYearId]
WHERE sy.[Status] = N'Draft' AND ca.[IsActive] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [ClassAssignments] SET [IsActive] = 1 WHERE [IsDraft] = 1;");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SchoolYears");

            migrationBuilder.DropColumn(
                name: "IsReviewed",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "ClassAssignments");
        }
    }
}
