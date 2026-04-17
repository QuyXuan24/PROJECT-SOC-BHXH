using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHXH_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpUploadLookupModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('BhxhRecords', 'BhxhCodeHash') IS NULL
                BEGIN
                    ALTER TABLE [BhxhRecords]
                    ADD [BhxhCodeHash] nvarchar(64) NOT NULL CONSTRAINT [DF_BhxhRecords_BhxhCodeHash] DEFAULT N'';
                END
                ELSE
                BEGIN
                    UPDATE [BhxhRecords]
                    SET [BhxhCodeHash] = LEFT(ISNULL([BhxhCodeHash], N''), 64);

                    IF EXISTS (
                        SELECT 1
                        FROM sys.columns
                        WHERE object_id = OBJECT_ID('BhxhRecords')
                          AND name = 'BhxhCodeHash'
                          AND (
                                max_length = -1
                                OR max_length > 128
                                OR system_type_id IN (99, 35, 34)
                              )
                    )
                    BEGIN
                        ALTER TABLE [BhxhRecords] ALTER COLUMN [BhxhCodeHash] nvarchar(64) NOT NULL;
                    END
                END
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('BhxhRecords', 'CccdHash') IS NULL
                BEGIN
                    ALTER TABLE [BhxhRecords]
                    ADD [CccdHash] nvarchar(64) NOT NULL CONSTRAINT [DF_BhxhRecords_CccdHash] DEFAULT N'';
                END
                ELSE
                BEGIN
                    UPDATE [BhxhRecords]
                    SET [CccdHash] = LEFT(ISNULL([CccdHash], N''), 64);

                    IF EXISTS (
                        SELECT 1
                        FROM sys.columns
                        WHERE object_id = OBJECT_ID('BhxhRecords')
                          AND name = 'CccdHash'
                          AND (
                                max_length = -1
                                OR max_length > 128
                                OR system_type_id IN (99, 35, 34)
                              )
                    )
                    BEGIN
                        ALTER TABLE [BhxhRecords] ALTER COLUMN [CccdHash] nvarchar(64) NOT NULL;
                    END
                END
                """);

            migrationBuilder.CreateTable(
                name: "bhyt",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    card_number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    registered_hospital = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    valid_from = table.Column<DateTime>(type: "datetime2", nullable: true),
                    valid_to = table.Column<DateTime>(type: "datetime2", nullable: true),
                    benefit_rate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bhyt", x => x.id);
                    table.ForeignKey(
                        name: "FK_bhyt_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "files",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file_path = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    file_type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_files_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "otp_codes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    otp_code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    expire_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_used = table.Column<bool>(type: "bit", nullable: false),
                    purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_otp_codes_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "pending_registrations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    phone_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    bhxh_code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_registrations", x => x.id);
                });

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_BhxhRecords_BhxhCodeHash'
                      AND object_id = OBJECT_ID('BhxhRecords')
                )
                BEGIN
                    CREATE INDEX [IX_BhxhRecords_BhxhCodeHash] ON [BhxhRecords]([BhxhCodeHash]);
                END
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_BhxhRecords_CccdHash'
                      AND object_id = OBJECT_ID('BhxhRecords')
                )
                BEGIN
                    CREATE INDEX [IX_BhxhRecords_CccdHash] ON [BhxhRecords]([CccdHash]);
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_bhyt_user_id",
                table: "bhyt",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_files_user_id_created_at",
                table: "files",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_email_purpose_is_used_expire_time",
                table: "otp_codes",
                columns: new[] { "email", "purpose", "is_used", "expire_time" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_user_id",
                table: "otp_codes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_email",
                table: "pending_registrations",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_registrations_username",
                table: "pending_registrations",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bhyt");

            migrationBuilder.DropTable(
                name: "files");

            migrationBuilder.DropTable(
                name: "otp_codes");

            migrationBuilder.DropTable(
                name: "pending_registrations");

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_BhxhRecords_BhxhCodeHash'
                      AND object_id = OBJECT_ID('BhxhRecords')
                )
                BEGIN
                    DROP INDEX [IX_BhxhRecords_BhxhCodeHash] ON [BhxhRecords];
                END
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_BhxhRecords_CccdHash'
                      AND object_id = OBJECT_ID('BhxhRecords')
                )
                BEGIN
                    DROP INDEX [IX_BhxhRecords_CccdHash] ON [BhxhRecords];
                END
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('BhxhRecords', 'BhxhCodeHash') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.default_constraints
                        WHERE parent_object_id = OBJECT_ID('BhxhRecords')
                          AND name = 'DF_BhxhRecords_BhxhCodeHash'
                    )
                    BEGIN
                        ALTER TABLE [BhxhRecords] DROP CONSTRAINT [DF_BhxhRecords_BhxhCodeHash];
                    END
                    ALTER TABLE [BhxhRecords] DROP COLUMN [BhxhCodeHash];
                END
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('BhxhRecords', 'CccdHash') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.default_constraints
                        WHERE parent_object_id = OBJECT_ID('BhxhRecords')
                          AND name = 'DF_BhxhRecords_CccdHash'
                    )
                    BEGIN
                        ALTER TABLE [BhxhRecords] DROP CONSTRAINT [DF_BhxhRecords_CccdHash];
                    END
                    ALTER TABLE [BhxhRecords] DROP COLUMN [CccdHash];
                END
                """);
        }
    }
}
