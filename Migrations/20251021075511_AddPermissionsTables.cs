using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Decatron.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_channel_permissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    channel_owner_id = table.Column<long>(type: "bigint", nullable: false),
                    granted_user_id = table.Column<long>(type: "bigint", nullable: false),
                    access_level = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    granted_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_channel_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserChannelPermissions_ChannelOwner",
                        column: x => x.channel_owner_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChannelPermissions_GrantedBy",
                        column: x => x.granted_by,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserChannelPermissions_GrantedUser",
                        column: x => x.granted_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_access_level",
                table: "user_channel_permissions",
                column: "access_level");

            migrationBuilder.CreateIndex(
                name: "idx_channel_granted_user",
                table: "user_channel_permissions",
                columns: new[] { "channel_owner_id", "granted_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_channel_owner",
                table: "user_channel_permissions",
                column: "channel_owner_id");

            migrationBuilder.CreateIndex(
                name: "idx_granted_user",
                table: "user_channel_permissions",
                column: "granted_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_channel_permissions_granted_by",
                table: "user_channel_permissions",
                column: "granted_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_channel_permissions");
        }
    }
}
