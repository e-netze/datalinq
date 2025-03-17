using System;

namespace E.DataLinq.Web.Services.Abstraction;

public interface IRoutingEndPointReflectionProvider
{
    T GetControllerCustomAttribute<T>()
         where T : Attribute;

    T GetActionMethodCustomAttribute<T>()
        where T : Attribute;
}
