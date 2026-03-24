using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace E.DataLinq.Web.Models.TokenCache;

public class TokenCreateRequest
{
    [Required]
    public string DataLinqRoute { get; set; } = string.Empty;
    [Required]
    public string Payload { get; set; } = string.Empty;
}
