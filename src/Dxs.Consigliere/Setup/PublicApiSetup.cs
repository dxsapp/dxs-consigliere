using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Swagger;

using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class PublicApiSetup
{
    public static IServiceCollection AddPublicApiZoneServices(this IServiceCollection services)
        => services
            .AddHttpContextAccessor()
            .AddSignalRForApp()
            .AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Services
            .AddResponseCompression(x => { x.EnableForHttps = true; })
            .AddRequestDecompression()
            .AddEndpointsApiExplorer()
            .AddSwaggerGen(opts =>
            {
                // wave-A3 S6: populate `required` from CLR NRT
                // annotations so the generated TS loses optional
                // markers on non-nullable C# properties.
                opts.SchemaFilter<RequiredFromNrtFilter>();
            })
            .AddTransient<ITransactionQueryService, TransactionQueryService>();
}
