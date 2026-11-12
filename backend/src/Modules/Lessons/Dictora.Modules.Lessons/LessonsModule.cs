using Dictora.Modules.Lessons.Infrastructure;
using Dictora.Modules.Lessons.Infrastructure.YouTube;
using Dictora.Shared.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictora.Modules.Lessons;

/// <summary>
/// Entry point of the Lessons module. The host (Dictora.Api) only calls these two methods;
/// everything else in this project should be <c>internal</c>.
/// </summary>
public static class LessonsModule
{
    public static IServiceCollection AddLessonsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<LessonsDbContext>(LessonsDbContext.Schema);

        // Captions via YoutubeExplode (ADR-002). The forward proxy (#25) plugs in on this HttpClient.
        services.AddHttpClient<ICaptionSource, YoutubeExplodeCaptionSource>(YoutubeExplodeCaptionSource.HttpClientName);

        return services;
    }

    public static IEndpointRouteBuilder MapLessonsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/lessons").WithTags("Lessons");

        // Map each feature's endpoint on the group, e.g. group.MapImportLesson();
        _ = group;

        return endpoints;
    }
}
