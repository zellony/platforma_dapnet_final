using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class LowercaseRbacColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // users
            migrationBuilder.RenameColumn(name: "Id", schema: "app_core", table: "users", newName: "id");
            migrationBuilder.RenameColumn(name: "Login", schema: "app_core", table: "users", newName: "login");
            migrationBuilder.RenameColumn(name: "PasswordHash", schema: "app_core", table: "users", newName: "password_hash");
            migrationBuilder.RenameColumn(name: "IsActive", schema: "app_core", table: "users", newName: "is_active");
            migrationBuilder.RenameColumn(name: "ExternalUserId", schema: "app_core", table: "users", newName: "external_user_id");
            migrationBuilder.RenameColumn(name: "AdUpn", schema: "app_core", table: "users", newName: "ad_upn");
            migrationBuilder.RenameColumn(name: "CreatedAtUtc", schema: "app_core", table: "users", newName: "created_at_utc");

            // roles
            migrationBuilder.RenameColumn(name: "Id", schema: "app_core", table: "roles", newName: "id");
            migrationBuilder.RenameColumn(name: "Name", schema: "app_core", table: "roles", newName: "name");

            // permissions
            migrationBuilder.RenameColumn(name: "Id", schema: "app_core", table: "permissions", newName: "id");
            migrationBuilder.RenameColumn(name: "Code", schema: "app_core", table: "permissions", newName: "code");
            migrationBuilder.RenameColumn(name: "Description", schema: "app_core", table: "permissions", newName: "description");

            // user_roles
            migrationBuilder.RenameColumn(name: "UserId", schema: "app_core", table: "user_roles", newName: "user_id");
            migrationBuilder.RenameColumn(name: "RoleId", schema: "app_core", table: "user_roles", newName: "role_id");

            // role_permissions
            migrationBuilder.RenameColumn(name: "RoleId", schema: "app_core", table: "role_permissions", newName: "role_id");
            migrationBuilder.RenameColumn(name: "PermissionId", schema: "app_core", table: "role_permissions", newName: "permission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // users
            migrationBuilder.RenameColumn(name: "id", schema: "app_core", table: "users", newName: "Id");
            migrationBuilder.RenameColumn(name: "login", schema: "app_core", table: "users", newName: "Login");
            migrationBuilder.RenameColumn(name: "password_hash", schema: "app_core", table: "users", newName: "PasswordHash");
            migrationBuilder.RenameColumn(name: "is_active", schema: "app_core", table: "users", newName: "IsActive");
            migrationBuilder.RenameColumn(name: "external_user_id", schema: "app_core", table: "users", newName: "ExternalUserId");
            migrationBuilder.RenameColumn(name: "ad_upn", schema: "app_core", table: "users", newName: "AdUpn");
            migrationBuilder.RenameColumn(name: "created_at_utc", schema: "app_core", table: "users", newName: "CreatedAtUtc");

            // roles
            migrationBuilder.RenameColumn(name: "id", schema: "app_core", table: "roles", newName: "Id");
            migrationBuilder.RenameColumn(name: "name", schema: "app_core", table: "roles", newName: "Name");

            // permissions
            migrationBuilder.RenameColumn(name: "id", schema: "app_core", table: "permissions", newName: "Id");
            migrationBuilder.RenameColumn(name: "code", schema: "app_core", table: "permissions", newName: "Code");
            migrationBuilder.RenameColumn(name: "description", schema: "app_core", table: "permissions", newName: "Description");

            // user_roles
            migrationBuilder.RenameColumn(name: "user_id", schema: "app_core", table: "user_roles", newName: "UserId");
            migrationBuilder.RenameColumn(name: "role_id", schema: "app_core", table: "user_roles", newName: "RoleId");

            // role_permissions
            migrationBuilder.RenameColumn(name: "role_id", schema: "app_core", table: "role_permissions", newName: "RoleId");
            migrationBuilder.RenameColumn(name: "permission_id", schema: "app_core", table: "role_permissions", newName: "PermissionId");
        }
    }
}
