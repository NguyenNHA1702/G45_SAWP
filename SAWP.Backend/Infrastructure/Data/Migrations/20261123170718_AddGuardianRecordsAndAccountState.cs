using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianRecordsAndAccountState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guardians_Users_UserId",
                table: "Guardians");

            migrationBuilder.DropIndex(
                name: "IX_Guardians_UserId",
                table: "Guardians");

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Guardians",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Guardians",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Guardians",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Guardians",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Account state from the old IsActive flag: active -> Active (2), inactive -> Deactivated (3).
            migrationBuilder.Sql("UPDATE [Users] SET [Status] = CASE WHEN [IsActive] = 1 THEN 2 ELSE 3 END;");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            // Guardian contact details move from the linked account to the guardian record (BR-64, ERD row 6).
            // Placeholder login emails made by the old import (...@parent.sawp.edu.vn) are not real addresses, so
            // they are not copied; such guardians stay without an email until the school enters one.
            migrationBuilder.Sql(@"
UPDATE g
SET g.[FullName] = u.[FullName],
    g.[PhoneNumber] = u.[PhoneNumber],
    g.[Email] = CASE WHEN u.[Email] LIKE '%@parent.sawp.edu.vn' OR LTRIM(RTRIM(u.[Email])) = '' THEN NULL
                     ELSE LTRIM(RTRIM(u.[Email])) END
FROM [Guardians] g
JOIN [Users] u ON u.[Id] = g.[UserId];");

            // Login emails become unique (BR-64). An empty or duplicate address (the oldest account keeps it) is
            // replaced by a unique placeholder, and the old value is written to the audit log so that nothing is
            // lost and the school can enter the right address (data fix, B8).
            migrationBuilder.Sql(@"
;WITH ranked AS (
    SELECT [Id], [Email],
           ROW_NUMBER() OVER (PARTITION BY LOWER(LTRIM(RTRIM([Email]))) ORDER BY [Id]) AS rn
    FROM [Users])
INSERT INTO [AuditLogs] ([UserId], [Action], [EntityName], [EntityId], [OldValues], [NewValues], [IpAddress], [Timestamp])
SELECT NULL, 'USER_EMAIL_PLACEHOLDER', 'User', CAST([Id] AS nvarchar(20)),
       CONCAT('{""Email"":""', STRING_ESCAPE([Email], 'json'), '""}'),
       CONCAT('{""Email"":""user', [Id], '@email-missing.invalid""}'),
       NULL, SYSUTCDATETIME()
FROM ranked
WHERE rn > 1 OR LTRIM(RTRIM([Email])) = '';

;WITH ranked AS (
    SELECT [Id], [Email],
           ROW_NUMBER() OVER (PARTITION BY LOWER(LTRIM(RTRIM([Email]))) ORDER BY [Id]) AS rn
    FROM [Users])
UPDATE ranked
SET [Email] = CONCAT('user', [Id], '@email-missing.invalid')
WHERE rn > 1 OR LTRIM(RTRIM([Email])) = '';");

            migrationBuilder.CreateTable(
                name: "AccountRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    FollowsUpRequestId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRequests_AccountRequests_FollowsUpRequestId",
                        column: x => x.FollowsUpRequestId,
                        principalTable: "AccountRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VerificationTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NewEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountRequestId = table.Column<int>(type: "int", nullable: false),
                    GuardianId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CheckResult = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRequestItems_AccountRequests_AccountRequestId",
                        column: x => x.AccountRequestId,
                        principalTable: "AccountRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountRequestItems_Guardians_GuardianId",
                        column: x => x.GuardianId,
                        principalTable: "Guardians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountRequestItems_Users_CreatedUserId",
                        column: x => x.CreatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountRequestItems_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_Email",
                table: "Guardians",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_UserId",
                table: "Guardians",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequestItems_AccountRequestId_GuardianId",
                table: "AccountRequestItems",
                columns: new[] { "AccountRequestId", "GuardianId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequestItems_CreatedUserId",
                table: "AccountRequestItems",
                column: "CreatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequestItems_DecidedByUserId",
                table: "AccountRequestItems",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequestItems_GuardianId",
                table: "AccountRequestItems",
                column: "GuardianId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequests_FollowsUpRequestId",
                table: "AccountRequests",
                column: "FollowsUpRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequests_RequestedByUserId",
                table: "AccountRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequests_Status",
                table: "AccountRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationTokens_TokenHash",
                table: "VerificationTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationTokens_UserId_Purpose",
                table: "VerificationTokens",
                columns: new[] { "UserId", "Purpose" });

            migrationBuilder.AddForeignKey(
                name: "FK_Guardians_Users_UserId",
                table: "Guardians",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guardians_Users_UserId",
                table: "Guardians");

            migrationBuilder.DropTable(
                name: "AccountRequestItems");

            migrationBuilder.DropTable(
                name: "VerificationTokens");

            migrationBuilder.DropTable(
                name: "AccountRequests");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Guardians_Email",
                table: "Guardians");

            migrationBuilder.DropIndex(
                name: "IX_Guardians_UserId",
                table: "Guardians");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "Users");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE [Users] SET [IsActive] = CASE WHEN [Status] = 2 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");

            // The old model has no guardian records without an account; this fails if such records exist.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Guardians] WHERE [UserId] IS NULL) THROW 50000, 'Guardian records without an account exist; link or remove them before reverting this migration.', 1;");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Guardians");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Guardians");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Guardians");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Guardians",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_UserId",
                table: "Guardians",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Guardians_Users_UserId",
                table: "Guardians",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
