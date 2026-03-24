using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Models.TokenCache;

public class TokenCreateResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int? MaxUsage { get; set; }
}
