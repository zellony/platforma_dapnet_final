using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleNameColumnToPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "module_name",
                schema: "app_core",
                table: "permissions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "module_name",
                schema: "app_core",
                table: "permissions");
        }
    }
}
