#nullable enable

using System;

namespace E.DataLinq.Code.Extensions;
static internal class UrlStringExtensions
{
    public static string AppendCodeApiLoginPath(this string url)
        => url.AppendIfNotEndsWithPath("/DataLinqAuth?redirect={0}");

    public static string AppendCodeApiLogoutPath(this string url)
        => url.AppendIfNotEndsWithPath("/DataLinqAuth/Logout?redirect={0}");

    public static string AppendLoginRedirectPath(this string url)
        => url.AppendIfNotEndsWithPath("/DataLinqCode/Connect/{0}");

    private static string AppendIfNotEndsWithPath(this string? url, string path)
        => url?.EndsWith(path) == true
            ? url
            : AppendUrlPath(url ?? "", path);

    private static string AppendUrlPath(string url, string path)
        => $"{RemoveEndingSlash(url)}/{RemoveBeginningSlash(path)}";

    private static string RemoveEndingSlash(string? url)
        => url?.EndsWith("/") == true
            ? url.Substring(0, url.Length - 1)
            : url ?? "";

    private static string RemoveBeginningSlash(string? url)
        => url?.StartsWith("/") == true
            ? url.Substring(1)
            : url ?? "";
}
