using System.Runtime.InteropServices;

namespace E.DataLinq.Core;
internal class Platform
{
    static public bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    static public bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    static public bool IsOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}
