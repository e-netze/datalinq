using E.DataLinq.Web.Models.TokenCache;
using E.DataLinq.Web.Services.Abstraction;
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
    private readonly IGitService _gitService;

    public DataLinqCacheController(
        IDataLinqCacheTokenService tokenService,
        IGitService gitService
        )
    {
        _tokenService = tokenService;
        _gitService = gitService;
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

    [HttpGet("gittest")]
    public async Task<IActionResult> GitTest()
    {
        //var clone = await _gitService.CloneAsync();
        var pushush = await _gitService.PushAsync();
        return JsonObject(pushush);
    }
}

#endif