using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Models.TokenCache;

public class TokenMetadata
{
    public string Token { get; set; }
    public string DataLinqRoute { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? MaxUsage { get; set; }
    public int UsageCount { get; set; }
}
