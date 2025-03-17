using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace E.DataLinq.LanguageEngine.Razor.Extensions;

internal static class ObjectExtensions
{
    public static ExpandoObject ToExpando(this object obj)
    {
        ExpandoObject expando = new ExpandoObject();
        IDictionary<string, object> dictionary = expando!;

        foreach (PropertyInfo property in obj.GetType().GetProperties())
        {
            dictionary.Add(property.Name, property.GetValue(obj)!);
        }

        return expando;
    }

    public static bool IsAnonymous(this object obj)
    {
        Type type = obj.GetType();

        return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
               && type.IsGenericType && type.Name.Contains("AnonymousType")
                  && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))
               && type.Attributes.HasFlag(TypeAttributes.NotPublic);
    }

    public static async Task<long> ReadLong(this Stream stream)
    {
        byte[] buffer = new byte[8];
        _ = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));

        return BitConverter.ToInt64(buffer, 0);
    }

    public static async Task WriteLong(this Stream stream, long value)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        await stream.WriteAsync(buffer);
    }
}