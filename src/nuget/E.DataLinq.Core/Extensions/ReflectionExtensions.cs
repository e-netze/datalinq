using E.DataLinq.Core.Reflection;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Core.Services.Persistance;
using System.Reflection;

namespace E.DataLinq.Core.Extensions;

static public class ReflectionExtensions
{
    static public T EncryptSecureProperties<T>(this T o, ICryptoService cryptoService, EncryptionLevel encryptionLevel)
    {
        if (o == null)
        {
            return default(T);
        }

        foreach (var pi in o.GetType().GetProperties())
        {
            if (pi.PropertyType == typeof(string) && pi.GetCustomAttribute<SecureStringAttribute>() != null)
            {
                pi.SetValue(o, ((string)pi.GetValue(o))?.EncryptStringProperty(cryptoService, encryptionLevel));
            }
        }
        return o;
    }

    static public T DecryptSecureProperties<T>(this T o, ICryptoService cryptoService)
    {
        if (o == null)
        {
            return default(T);
        }

        foreach (var pi in o.GetType().GetProperties())
        {
            if (pi.PropertyType == typeof(string) && pi.GetCustomAttribute<SecureStringAttribute>() != null)
            {
                pi.SetValue(o, ((string)pi.GetValue(o))?.DecryptStringProperty(cryptoService));
            }
        }
        return o;
    }
}
