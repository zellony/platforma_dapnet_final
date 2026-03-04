using Microsoft.EntityFrameworkCore;
using Platform.Api.Modules.KSeF.Database;

namespace Platform.Module.Ksef.Database.Seed;

public class KsefPermissionSeeder
{
    private readonly KsefDbContext _db;

    public KsefPermissionSeeder(KsefDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        // Aktualizacja istniejących lub wstawienie nowych
        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE app_core.permissions 
            SET module_name = 'KSEF', description = 'Widok modułu KSeF' 
            WHERE code = 'ksef.view' AND module_name IS NULL;

            INSERT INTO app_core.permissions (id, code, description, module_name)
            SELECT gen_random_uuid(), 'ksef.view', 'Widok modułu KSeF', 'KSEF'
            WHERE NOT EXISTS (SELECT 1 FROM app_core.permissions WHERE code = 'ksef.view');
        ");

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE app_core.permissions 
            SET module_name = 'KSEF', description = 'Import faktur z KSeF' 
            WHERE code = 'ksef.import' AND module_name IS NULL;

            INSERT INTO app_core.permissions (id, code, description, module_name)
            SELECT gen_random_uuid(), 'ksef.import', 'Import faktur z KSeF', 'KSEF'
            WHERE NOT EXISTS (SELECT 1 FROM app_core.permissions WHERE code = 'ksef.import');
        ");
    }
}
