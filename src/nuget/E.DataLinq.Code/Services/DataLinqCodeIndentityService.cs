using Microsoft.AspNetCore.Http;
using System.Linq;

namespace E.DataLinq.Code.Services;

public class DataLinqCodeIndentityService
{
    internal const string InstanceIdClaimType = "instance-id";
    internal const string AccessTokenClaimTyp = "access-token";

    private readonly HttpContext _httpContext;

    public DataLinqCodeIndentityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContext = httpContextAccessor.HttpContext;
    }

    public (int? id, string userDisplayName, string accessToken) IdentityData()
    {
        try
        {
            if (_httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var instanceId =
                    _httpContext.User.Claims.Where(c => c.Type == InstanceIdClaimType).First().Value;
                var accessToken =
                    _httpContext.User.Claims.Where(c => c.Type == AccessTokenClaimTyp).First().Value;

                return (
                        int.Parse(instanceId),
                        _httpContext.User.Identity.Name,
                        accessToken
                    );
            }
        }
        catch { }

        return (null, null, null);
    }
}
