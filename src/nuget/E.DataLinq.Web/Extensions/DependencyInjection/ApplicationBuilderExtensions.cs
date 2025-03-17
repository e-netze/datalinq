using E.DataLinq.Web.Middleware.Authentication;
using Microsoft.AspNetCore.Builder;

namespace E.DataLinq.Web.Extensions.DependencyInjection;

static public class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseDatalinqTokenAuthorization(this IApplicationBuilder app)
    {
        app.UseMiddleware<DataLinqAccessTokenAuthenticationMiddleware>();
        return app;
    }

    public static IApplicationBuilder AddDatalinqRouting(this IApplicationBuilder app)
    {
        app.UseEndpoints(endPoints =>
        {
            endPoints.AddDataLinqEndpoints();
        });

        return app;
    }
}
