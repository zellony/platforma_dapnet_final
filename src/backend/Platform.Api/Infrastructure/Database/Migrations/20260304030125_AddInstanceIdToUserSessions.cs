using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceIdToUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "instance_id",
                schema: "app_core",
                table: "user_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE app_core.user_sessions " +
                "SET instance_id = substring(user_agent from ';instance=([^;\\s]+)') " +
                "WHERE instance_id IS NULL AND user_agent IS NOT NULL AND user_agent LIKE '%;instance=%';");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_instance_id",
                schema: "app_core",
                table: "user_sessions",
                column: "instance_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_sessions_instance_id",
                schema: "app_core",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "instance_id",
                schema: "app_core",
                table: "user_sessions");
        }
    }
}
