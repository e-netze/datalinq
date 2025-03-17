using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Security.Token.Models;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;

namespace E.DataLinq.Core.Security.Token;

public class AccessToken
{
    private AccessTokenService _service;

    internal AccessToken(AccessTokenService service,
                         Header header, Payload payload)
    {
        _service = service;
        this.Header = header;
        this.Payload = payload;
    }

    internal AccessToken(AccessTokenService service, string token)
    {
        _service = service;
        Parse(token);
    }

    #region Properties

    public Header Header { get; private set; }
    public Payload Payload { get; private set; }

    #endregion

    public string ToTokenString()
    {
        string headerJson = JsonConvert.SerializeObject(this.Header);
        string payloadJson = JsonConvert.SerializeObject(this.Payload);

        string headerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(headerJson));
        string payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));

        string unsignedToken = $"{headerBase64}.{payloadBase64}";

        return $"{unsignedToken}.{Convert.ToBase64String(_service.CalcSignuature(headerBase64, payloadBase64))}";
    }

    #region Helper

    private void Parse(string token)
    {
        if (String.IsNullOrEmpty(token))
        {
            throw new InvalidTokenException("Token is empty");
        }

        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidTokenException("Missformed token: {header:base64}.{payload:base64}.{signature}");
        }

        try
        {
            this.Header = JsonConvert.DeserializeObject<Header>(Encoding.UTF8.GetString(Convert.FromBase64String(parts[0])));
        }
        catch
        {
            throw new InvalidTokenException("Invalid token header");
        }

        try
        {
            this.Payload = JsonConvert.DeserializeObject<Payload>(Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])));
        }
        catch
        {
            throw new InvalidTokenException("Invalid token Payload");
        }

        var signatureBytes = Convert.FromBase64String(parts[2]);
        var calcedSignitureBytes = _service.CalcSignuature(parts[0], parts[1]);

        if (!signatureBytes.SequenceEqual(calcedSignitureBytes))
        {
            throw new InvalidTokenException("corrupt signature");
        }
    }

    #endregion
}
