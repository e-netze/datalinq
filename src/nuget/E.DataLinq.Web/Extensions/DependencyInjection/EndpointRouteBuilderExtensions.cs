using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace E.DataLinq.Web.Extensions.DependencyInjection;

static public class EndPointRouteBuilderExtensions
{
    static public void AddDataLinqEndpoints(this IEndpointRouteBuilder endPoints)
    {
        endPoints.MapControllerRoute(
                "datalinq/action",
                "datalinq/{action}/{__dataLinqRoute}",
                new { controller = "DataLinq" });

        endPoints.MapControllerRoute(
                        "datalinq_css_proxy",
                        "datalinq/{__dataLinqRoute}/endpoint-css-proxy",
                         new { controller = "DataLinq", Action = "CssProxy" });
    }
}
