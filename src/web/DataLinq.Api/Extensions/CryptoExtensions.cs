using System.Security.Cryptography;

namespace DataLinq.Api.Extensions;

internal static class CryptoExtensions
{
    static public string OrRandomPassword(this string? password)
        => String.IsNullOrEmpty(password)
            ? ToRandomBase64String(64)
            : password;

    static public string OrRandomSaltBase64(this string? saltBase64)
        => String.IsNullOrEmpty(saltBase64)
            ? ToRandomBase64String(8)
            : saltBase64;

    private static string ToRandomBase64String(int length)
    {
        byte[] ba = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(ba);
        }
        return Convert.ToBase64String(ba);
    }
}