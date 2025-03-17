using System.Reflection;

namespace E.DataLinq.Core;

public class DataLinqVersion
{
    private static string _versionString = Assembly
        .GetAssembly(typeof(Engines.DatabaseEngine))
        ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
        ?.Split('+')[0] ?? "";

    public static string Version => _versionString;

    public static string JsVersion
    {
        get { return Version; }
    }

    public static string CssVersion
    {
        get { return Version; }
    }
}
