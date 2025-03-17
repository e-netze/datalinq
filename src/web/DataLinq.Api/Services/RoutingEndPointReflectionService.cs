using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace DataLinq.Api.Services;

public class RoutingEndPointReflectionService : IRoutingEndPointReflectionProvider
{
    private readonly IEnumerable<Attribute>? _controllerAttributes;
    private readonly IEnumerable<Attribute>? _actionMethodAttributes;

    public RoutingEndPointReflectionService(IHttpContextAccessor context)
    {
        var controllerActionDescriptor = context.HttpContext?.GetEndpoint()?.Metadata?.GetMetadata<ControllerActionDescriptor>();

        _controllerAttributes = controllerActionDescriptor?.ControllerTypeInfo?.GetCustomAttributes();
        _actionMethodAttributes = controllerActionDescriptor?.MethodInfo?.GetCustomAttributes();
    }

    public T? GetControllerCustomAttribute<T>()
        where T : Attribute
    {
        var type = typeof(T);

        return (T?)_controllerAttributes?.Where(a => a.GetType().Equals(type)).FirstOrDefault();
    }

    public T? GetActionMethodCustomAttribute<T>()
        where T : Attribute
    {
        var type = typeof(T);

        return (T?)_actionMethodAttributes?.Where(a => a.GetType().Equals(type)).FirstOrDefault();
    }
}
