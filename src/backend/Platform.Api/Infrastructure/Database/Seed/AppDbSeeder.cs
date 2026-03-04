using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Platform.Kernel.Core.RBAC.Entities;

namespace Platform.Api.Infrastructure.Database.Seed;

public sealed class AppDbSeeder
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AppDbSeeder(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task SeedAsync()
    {
        // 1. ROLE
        if (!await _db.Roles.AnyAsync())
        {
            _db.Roles.AddRange(new Role { Name = "Admin" }, new Role { Name = "User" });
            await _db.SaveChangesAsync();
        }
        var adminRole = await _db.Roles.FirstAsync(r => r.Name == "Admin");

        // 2. PERMISSIONS (Rdzeń + KSeF)
        var allPermissions = new[]
        {
            new { Code = "platform.admin", Module = "ADMINISTRACJA", Desc = "Pełny dostęp administracyjny" },
            new { Code = "platform.users.read", Module = "ADMINISTRACJA", Desc = "Odczyt listy użytkowników" },
            new { Code = "platform.users.write", Module = "ADMINISTRACJA", Desc = "Zarządzanie użytkownikami" },
            new { Code = "ksef.view", Module = "KSEF", Desc = "Widok modułu KSeF" },
            new { Code = "ksef.import", Module = "KSEF", Desc = "Import faktur z KSeF" }
        };

        // OPTYMALIZACJA: Pobieramy wszystkie uprawnienia jednym zapytaniem
        var existingPermissions = await _db.Permissions.ToDictionaryAsync(p => p.Code);
        bool anyChanges = false;

        foreach (var pDef in allPermissions)
        {
            if (!existingPermissions.TryGetValue(pDef.Code, out var existing))
            {
                _db.Permissions.Add(new Permission { Code = pDef.Code, ModuleName = pDef.Module, Description = pDef.Desc });
                anyChanges = true;
            }
            else
            {
                bool changed = false;
                if (existing.ModuleName != pDef.Module) { existing.ModuleName = pDef.Module; changed = true; }
                if (existing.Description != pDef.Desc) { existing.Description = pDef.Desc; changed = true; }
                if (changed) { _db.Permissions.Update(existing); anyChanges = true; }
            }
        }
        
        if (anyChanges) await _db.SaveChangesAsync();

        // 3. ADMIN -> ALL PERMS
        var allPermIds = await _db.Permissions.Select(p => p.Id).ToListAsync();
        var existingRP = await _db.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var missingRP = allPermIds
            .Where(id => !existingRP.Contains(id))
            .Select(id => new RolePermission { RoleId = adminRole.Id, PermissionId = id })
            .ToList();

        if (missingRP.Count > 0)
        {
            _db.RolePermissions.AddRange(missingRP);
            await _db.SaveChangesAsync();
        }
    }
}
