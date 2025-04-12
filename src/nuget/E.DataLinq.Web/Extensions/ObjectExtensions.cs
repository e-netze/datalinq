using System;
using System.Collections.Generic;
using System.Reflection;

namespace E.DataLinq.Web.Extensions;

static internal class ObjectExtensions
{
    static public object GetDefaultValueFromRecord(this object record, string name, object defaultValue = null)
    {
        object val = defaultValue;

        if (record != null)
        {
            if (!(record is IDictionary<string, object>))
            {
                throw new ArgumentException("record is not an ExpandoObject");
            }

            var recordDictionary = (IDictionary<string, object>)record;
            // Wenn bei Queries Domains erstellt wurden (bspw. 0 durch "Nein" ersetzt; bspw. bei Select) => defaultValue sollte wieder Originalwert sein
            // bei DOMAINS wird ein Dictionary-Eintrag mit Namen + "_ORIGINAL" mit dem Originalwert gesetzt
            if (recordDictionary.ContainsKey(name + "_ORIGINAL") && !DBNull.Value.Equals(recordDictionary[name + "_ORIGINAL"]) && recordDictionary[name + "_ORIGINAL"] != null && !String.IsNullOrEmpty(recordDictionary[name + "_ORIGINAL"].ToString()))
            {
                val = recordDictionary[name + "_ORIGINAL"];
            }
            else if (recordDictionary.ContainsKey(name) && !DBNull.Value.Equals(recordDictionary[name]) && recordDictionary[name] != null && !String.IsNullOrEmpty(recordDictionary[name].ToString()))
            {
                val = recordDictionary[name];
            }
        }

        return val;
    }

    static public IDictionary<string, object> ToDictionary(this object anonymousObject)
    {
        if (anonymousObject == null)
        {
            return null;
        }

        if (anonymousObject is IDictionary<string, object>)
        {
            return (IDictionary<string, object>)anonymousObject;
        }

        Dictionary<string, object> dict = new Dictionary<string, object>();
        foreach (PropertyInfo pi in anonymousObject.GetType().GetProperties())
        {
            if (dict.ContainsKey(pi.Name))
            {
                continue;
            }

            dict.Add(pi.Name, pi.GetValue(anonymousObject));
        }

        return dict;
    }
}
