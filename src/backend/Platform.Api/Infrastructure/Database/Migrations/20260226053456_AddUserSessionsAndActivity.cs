using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSessionsAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WYCZYSZCZENIE TABELI POWIĄZAŃ PRZED NAŁOŻENIEM KLUCZY OBCYCH
            migrationBuilder.Sql("DELETE FROM app_core.user_roles;");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_at_utc",
                schema: "app_core",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_sessions",
                schema: "app_core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    logout_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    logout_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app_core",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_user_id",
                schema: "app_core",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_users_user_id",
                schema: "app_core",
                table: "user_roles",
                column: "user_id",
                principalSchema: "app_core",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_users_user_id",
                schema: "app_core",
                table: "user_roles");

            migrationBuilder.DropTable(
                name: "user_sessions",
                schema: "app_core");

            migrationBuilder.DropColumn(
                name: "last_activity_at_utc",
                schema: "app_core",
                table: "users");
        }
    }
}
