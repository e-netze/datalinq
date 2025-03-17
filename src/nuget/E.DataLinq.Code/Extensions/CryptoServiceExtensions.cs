using E.DataLinq.Core.Services.Crypto.Abstraction;
using System;

namespace E.DataLinq.Code.Extensions;
static internal class CryptoServiceExtensions
{
    const string Separator = "$";

    static public string ToSessionString(this ICryptoService crypto, params string[] data)
        => crypto.EncryptTextDefault($"{Guid.NewGuid().ToString()}:{string.Join(Separator, data)}", Core.Services.Crypto.CryptoResultStringType.Hex);

    static public string[] GetSessionData(this ICryptoService crypto, string sessionString)
    {
        if (String.IsNullOrEmpty(sessionString))
        {
            return Array.Empty<string>();
        }

        var str = crypto.DecryptTextDefault(sessionString);
        if (!str.Contains(":"))
        {
            throw new Exception("Invalid session string");
        }

        return str.Substring(str.IndexOf(":") + 1).Split(Separator);
    }
}
