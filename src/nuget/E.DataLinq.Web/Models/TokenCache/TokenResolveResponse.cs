using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Models.TokenCache;

public class TokenResolveResponse
{
    public bool Success { get; set; }
    public string Payload { get; set; }
    public string DataLinqRoute { get; set; }
    public string ErrorMessage { get; set; }
}
