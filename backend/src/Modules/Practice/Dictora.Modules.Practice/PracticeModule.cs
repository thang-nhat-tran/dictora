using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictora.Modules.Practice;

/// <summary>
/// Entry point of the Practice module. The host (Dictora.Api) only calls these two methods;
/// everything else in this project should be <c>internal</c>.
/// </summary>
public static class PracticeModule
{
    public static IServiceCollection AddPracticeModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register this module's DbContext, services and handlers here.
        return services;
    }

    public static IEndpointRouteBuilder MapPracticeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/practice").WithTags("Practice");

        // Map each feature's endpoint on the group, e.g. group.MapImportLesson();
        _ = group;

        return endpoints;
    }
}
