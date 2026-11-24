using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueActiveClassAssignmentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassAssignments_StudentId",
                table: "ClassAssignments");

            migrationBuilder.Sql(@"
                ;WITH Ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY StudentId, ClassId ORDER BY Id DESC) AS rn
                    FROM ClassAssignments
                    WHERE IsActive = 1
                )
                UPDATE ClassAssignments
                SET IsActive = 0,
                    EndDate = COALESCE(EndDate, CAST(GETUTCDATE() AS date))
                WHERE Id IN (SELECT Id FROM Ranked WHERE rn > 1);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ClassAssignments_StudentId_ClassId",
                table: "ClassAssignments",
                columns: new[] { "StudentId", "ClassId" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassAssignments_StudentId_ClassId",
                table: "ClassAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ClassAssignments_StudentId",
                table: "ClassAssignments",
                column: "StudentId");
        }
    }
}
