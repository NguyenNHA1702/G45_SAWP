using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "DocumentRequestDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "CertificateNumber",
                table: "DocumentRequestDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DigitalSignatureInfo",
                table: "DocumentRequestDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentContentDraft",
                table: "DocumentRequestDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuedDocumentUrl",
                table: "DocumentRequestDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffEndorsement",
                table: "DocumentRequestDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TransferReason",
                table: "ClassTransferDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutedAt",
                table: "ClassTransferDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExecuted",
                table: "ClassTransferDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StaffRecommendation",
                table: "ClassTransferDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetClassCapacitySnapshot",
                table: "ClassTransferDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReviewerNotes",
                table: "AdministrativeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmendmentNotes",
                table: "AdministrativeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "AdministrativeRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAmendment",
                table: "AdministrativeRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SchoolBoardApprovedAt",
                table: "AdministrativeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolBoardNotes",
                table: "AdministrativeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificateNumber",
                table: "DocumentRequestDetails");

            migrationBuilder.DropColumn(
                name: "DigitalSignatureInfo",
                table: "DocumentRequestDetails");

            migrationBuilder.DropColumn(
                name: "DocumentContentDraft",
                table: "DocumentRequestDetails");

            migrationBuilder.DropColumn(
                name: "IssuedDocumentUrl",
                table: "DocumentRequestDetails");

            migrationBuilder.DropColumn(
                name: "StaffEndorsement",
                table: "DocumentRequestDetails");

            migrationBuilder.DropColumn(
                name: "ExecutedAt",
                table: "ClassTransferDetails");

            migrationBuilder.DropColumn(
                name: "IsExecuted",
                table: "ClassTransferDetails");

            migrationBuilder.DropColumn(
                name: "StaffRecommendation",
                table: "ClassTransferDetails");

            migrationBuilder.DropColumn(
                name: "TargetClassCapacitySnapshot",
                table: "ClassTransferDetails");

            migrationBuilder.DropColumn(
                name: "AmendmentNotes",
                table: "AdministrativeRequests");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "AdministrativeRequests");

            migrationBuilder.DropColumn(
                name: "RequiresAmendment",
                table: "AdministrativeRequests");

            migrationBuilder.DropColumn(
                name: "SchoolBoardApprovedAt",
                table: "AdministrativeRequests");

            migrationBuilder.DropColumn(
                name: "SchoolBoardNotes",
                table: "AdministrativeRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "DocumentRequestDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "TransferReason",
                table: "ClassTransferDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "ReviewerNotes",
                table: "AdministrativeRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
