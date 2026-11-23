using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropGuardianLinkDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The parent link request was removed (OVR-02, SSAWP-116); its remaining requests and details are deleted.
            migrationBuilder.DropTable(
                name: "GuardianLinkDetails");

            migrationBuilder.Sql("DELETE FROM [AdministrativeRequests] WHERE [RequestType] = 'ChildLink';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreates the empty table only; the deleted requests are not restored.
            migrationBuilder.CreateTable(
                name: "GuardianLinkDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdministrativeRequestId = table.Column<int>(type: "int", nullable: false),
                    AutoVerificationPassed = table.Column<bool>(type: "bit", nullable: false),
                    GuardianIdentityCard = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GuardianPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProofDocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Relationship = table.Column<int>(type: "int", nullable: false),
                    StudentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StudentDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardianLinkDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuardianLinkDetails_AdministrativeRequests_AdministrativeRequestId",
                        column: x => x.AdministrativeRequestId,
                        principalTable: "AdministrativeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianLinkDetails_AdministrativeRequestId",
                table: "GuardianLinkDetails",
                column: "AdministrativeRequestId",
                unique: true);
        }
    }
}
