using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Platform.Api.Modules.KSeF;

public sealed class KsefSchemaCompatibilityMvcOptionsSetup : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        // Add as a service filter so DI works
        options.Filters.AddService<KsefSchemaCompatibilityFilter>(order: int.MinValue);
    }
}
