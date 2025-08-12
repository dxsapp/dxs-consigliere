using Microsoft.AspNetCore.Hosting;

namespace Dxs.Consigliere.Extensions;

public static class WebHostEnvironmentExtensions
{
    public static bool IsProduction(this IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.EnvironmentName.Equals("Production");
}