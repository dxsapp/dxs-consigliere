using System.Text.Json.Serialization;

using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.Http.Connections;

namespace Dxs.Consigliere.Setup;

public static class SignalRSetup
{
    public static IServiceCollection AddSignalRForApp(this IServiceCollection services)
    {
        services
            .AddSignalR(o =>
            {
                o.DisableImplicitFromServicesParameters = false;
                o.EnableDetailedErrors = true;
                o.MaximumReceiveMessageSize = 10485760;
            })
            .AddJsonProtocol(options =>
            {
                options
                    .PayloadSerializerOptions
                    .Converters
                    .Add(new JsonStringEnumConverter());
            })
            .AddMessagePackProtocol(options =>
            {
                // options.SerializerOptions = MessagePackSerializerOptions.Standard
                //     .WithCompression(MessagePackCompression.Lz4BlockArray);

                // options.SerializerOptions = MessagePackSerializerOptions.Standard
                //     .WithResolver(MessagePack.Resolvers.NativeDecimalResolver.Instance);
            });

        return services;
    }

    public static IApplicationBuilder UseSignalR(this IApplicationBuilder app)
        => app.UseEndpoints(
            endpoints =>
            {
                endpoints.MapHub<WalletHub>(
                    WalletHub.Route,
                    options =>
                    {
                        options.Transports = HttpTransportType.WebSockets;
                    }
                );
            }
        );
}
