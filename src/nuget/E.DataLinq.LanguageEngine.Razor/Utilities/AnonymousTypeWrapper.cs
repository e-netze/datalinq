using E.DataLinq.LanguageEngine.Razor.Extensions;
using System.Collections;
using System.Dynamic;
using System.Reflection;

namespace E.DataLinq.LanguageEngine.Razor.Utilities;

class AnonymousTypeWrapper : DynamicObject
{
    private readonly object model;

    public AnonymousTypeWrapper(object model)
    {
        this.model = model;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        PropertyInfo? propertyInfo = model.GetType().GetProperty(binder.Name);

        if (propertyInfo == null)
        {
            result = null;
            return false;
        }

        result = propertyInfo.GetValue(model, null);

        if (result == null)
        {
            return true;
        }

        //var type = result.GetType();

        if (result.IsAnonymous())
        {
            result = new AnonymousTypeWrapper(result);
        }

        if (result is IDictionary dictionary)
        {
            List<object> keys = dictionary.Keys.Cast<object>().ToList();

            foreach (object key in keys)
            {
                if (dictionary[key]?.IsAnonymous() == true)
                {
                    dictionary[key] = new AnonymousTypeWrapper(dictionary[key]!);
                }
            }
        }
        else if (result is IEnumerable enumerable and not string)
        {
            result = enumerable.Cast<object>()
                    .Select(e => e.IsAnonymous() ? new AnonymousTypeWrapper(e) : e)
                    .ToList();
        }

        return true;
    }
}
