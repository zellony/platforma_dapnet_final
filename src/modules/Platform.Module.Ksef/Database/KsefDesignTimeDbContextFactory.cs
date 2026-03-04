using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Api.Modules.KSeF.Database;

namespace Platform.Module.Ksef.Database;

public sealed class KsefDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<KsefDbContext>
{
    public KsefDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("PLATFORM_DB");
        if (string.IsNullOrWhiteSpace(cs))
            throw new Exception("Set env var PLATFORM_DB with postgres connection string.");

        var opt = new DbContextOptionsBuilder<KsefDbContext>()
            .UseNpgsql(cs, x => x.MigrationsHistoryTable("__EFMigrationsHistory_Ksef", "app_core"))
            .Options;

        return new KsefDbContext(opt);
    }
}