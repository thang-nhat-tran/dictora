using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictora.Modules.Identity;

/// <summary>
/// Entry point of the Identity module. The host (Dictora.Api) only calls these two methods;
/// everything else in this project should be <c>internal</c>.
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register this module's DbContext, services and handlers here.
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Identity");

        // Map each feature's endpoint on the group, e.g. group.MapImportLesson();
        _ = group;

        return endpoints;
    }
}
