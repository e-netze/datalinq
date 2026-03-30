using E.DataLinq.Web.Models.TokenCache;
using E.DataLinq.Web.Services.TokenCache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Controllers;

#if DEBUG

[ApiController]
[Route("datalinqcache")]
public class DataLinqCacheController : ApiBaseController
{
    private readonly IDataLinqCacheTokenService _tokenService;

    public DataLinqCacheController(
        IDataLinqCacheTokenService tokenService
        )
    {
        _tokenService = tokenService;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateCacheEntry([FromBody] TokenCreateRequest request)
    {
        var response = await _tokenService.CreateTokenAsync(request);

        return Ok(response);
    }

    [HttpGet("test")]
    public async Task<IActionResult> TestToken(string token)
    {
        var meta = await _tokenService.ResolveTokenAsync(token,true);
        return JsonObject(new { success = meta.Success });
    }
}

#endif