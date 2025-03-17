using E.DataLinq.Core.Models.Authentication;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Web.Extensions;
using Microsoft.AspNetCore.Http;

namespace E.DataLinq.Web.Services;

class DataLinqCodeIdentityService : IDataLinqCodeIdentityService
{
    private readonly IHttpContextAccessor _contextAccessor;

    public DataLinqCodeIdentityService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public DataLinqCodeIdentity CurrentIdentity()
    {
        if (_contextAccessor?.HttpContext?.User?.Identity == null)
        {
            return null;
        }

        return new DataLinqCodeIdentity()
        {
            Id = _contextAccessor.HttpContext.User.GetUserId(),
            Name = _contextAccessor.HttpContext.User.GetUsername(),
            Roles = _contextAccessor.HttpContext.User.GetRoles()
        };
    }
}
