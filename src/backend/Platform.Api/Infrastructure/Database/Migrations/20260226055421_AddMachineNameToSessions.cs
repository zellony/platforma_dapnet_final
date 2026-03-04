using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineNameToSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "machine_name",
                schema: "app_core",
                table: "user_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "app_core",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_roles_role_id",
                schema: "app_core",
                table: "user_roles",
                column: "role_id",
                principalSchema: "app_core",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_roles_role_id",
                schema: "app_core",
                table: "user_roles");

            migrationBuilder.DropIndex(
                name: "IX_user_roles_role_id",
                schema: "app_core",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "machine_name",
                schema: "app_core",
                table: "user_sessions");
        }
    }
}
