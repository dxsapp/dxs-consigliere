using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

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
            .AddSwaggerGen()
            .AddTransient<ITransactionQueryService, TransactionQueryService>();
}
