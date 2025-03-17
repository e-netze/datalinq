using E.DataLinq.Core.Security.Token.Models;
using E.DataLinq.Core.Services.Crypto;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;
using System.Text;

namespace E.DataLinq.Core.Security.Token;

public class AccessTokenService
{
    private readonly CryptoServiceOptions _options;

    public AccessTokenService(IOptionsMonitor<CryptoServiceOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;
    }

    #region Properties

    public Header Header { get; private set; }
    public Payload Payload { get; private set; }

    #endregion

    public AccessToken CreateAccessToken(Header header, Payload payload)
    {
        return new AccessToken(this,
            header ?? throw new ArgumentNullException(nameof(header)),
            payload ?? throw new ArgumentNullException(nameof(payload)));
    }

    public AccessToken CreateAccessToken(string token)
    {
        var accessToken = new AccessToken(this, token);

        return accessToken;
    }

    internal byte[] CalcSignuature(string headerBase64, string payloadBase64)
    {
        using (var hmacSha512 = new HMACSHA512(Encoding.UTF8.GetBytes(_options.DefaultPassword)))
        {
            return hmacSha512.ComputeHash(Combine(Convert.FromBase64String(headerBase64), Convert.FromBase64String(payloadBase64)));
        }
    }

    private byte[] Combine(byte[] array1, byte[] array2)
    {
        byte[] ret = new byte[array1.Length + array2.Length];
        Buffer.BlockCopy(array1, 0, ret, 0, array1.Length);
        Buffer.BlockCopy(array2, 0, ret, array1.Length, array2.Length);
        return ret;
    }
}
