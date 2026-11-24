using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestAndAuditIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdministrativeRequests_RequesterUserId",
                table: "AdministrativeRequests");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeRequests_RequesterUserId_RequestType_SubmissionDate",
                table: "AdministrativeRequests",
                columns: new[] { "RequesterUserId", "RequestType", "SubmissionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeRequests_RequestType_Status_SubmissionDate",
                table: "AdministrativeRequests",
                columns: new[] { "RequestType", "Status", "SubmissionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityName_EntityId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AdministrativeRequests_RequesterUserId_RequestType_SubmissionDate",
                table: "AdministrativeRequests");

            migrationBuilder.DropIndex(
                name: "IX_AdministrativeRequests_RequestType_Status_SubmissionDate",
                table: "AdministrativeRequests");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeRequests_RequesterUserId",
                table: "AdministrativeRequests",
                column: "RequesterUserId");
        }
    }
}
