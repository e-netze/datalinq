using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace E.DataLinq.Code.Extensions;

static class MemberInfoExtensions
{
    public static string GetDescription(this MemberInfo memInfo)
    {
        return memInfo?.GetCustomAttribute<DisplayAttribute>()?.Description ??
               memInfo?.GetCustomAttribute<DescriptionAttribute>()?.Description;
    }
}
