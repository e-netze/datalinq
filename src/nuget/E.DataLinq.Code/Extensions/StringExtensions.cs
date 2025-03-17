#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace E.DataLinq.Code.Extensions;
static internal class StringExtensions
{
    public static string IfNullOrEmpty(this string? str, string candidate)
        => !String.IsNullOrEmpty(str)
            ? str
            : candidate;

    public static string RemoveAllAt(this string str, string removeAt)
        => str.Contains(removeAt, StringComparison.OrdinalIgnoreCase)
            ? str.Substring(0, str.IndexOf(removeAt, StringComparison.OrdinalIgnoreCase))
            : str;
}
