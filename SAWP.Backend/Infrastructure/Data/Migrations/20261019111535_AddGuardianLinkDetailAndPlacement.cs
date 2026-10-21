using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianLinkDetailAndPlacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlacementPublished",
                table: "Classes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GuardianLinkDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdministrativeRequestId = table.Column<int>(type: "int", nullable: false),
                    StudentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StudentDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Relationship = table.Column<int>(type: "int", nullable: false),
                    GuardianPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    GuardianIdentityCard = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProofDocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AutoVerificationPassed = table.Column<bool>(type: "bit", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuardianLinkDetails");

            migrationBuilder.DropColumn(
                name: "IsPlacementPublished",
                table: "Classes");
        }
    }
}
