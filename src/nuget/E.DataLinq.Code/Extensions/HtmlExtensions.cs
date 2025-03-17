using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace E.DataLinq.Code.Extensions;

static public class HtmlExtensions
{
    public static IEnumerable<TItem> EnumToSelectList<TItem, TEnum>(this TEnum enumObj)
        where TEnum : struct, IComparable, IFormattable, IConvertible
    {
        List<TItem> items = new List<TItem>();

        foreach (var val in Enum.GetValues(typeof(TEnum)))
        {
            items.Add(CreateItem<TItem>(val.ToString(), val.ToString()));
        }

        return items;
    }

    public static IEnumerable<TItem> DictToSelectList<TItem>(this IDictionary<int, string> dict)
    {
        List<TItem> items = new List<TItem>();

        foreach (var key in dict.Keys)
        {
            items.Add(CreateItem<TItem>(dict[key], key.ToString()));
        }

        return items;
    }

    public static IHtmlContent DescriptionFor<TModel, TValue>(this IHtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression)
    {
        string description = (expression?.Body as MemberExpression)?.Member?.GetDescription();

        if (!String.IsNullOrEmpty(description))
        {
            return new HtmlString($"<p class='description'>{description}</p>");
        }

        return null;
    }

    #region Helper

    private static TItem CreateItem<TItem>(string text, string value)
    {
        var item = Activator.CreateInstance(typeof(TItem));

        var textProperty = item.GetType().GetProperty("Text");
        var valueProperty = item.GetType().GetProperty("Value");

        if (textProperty != null)
        {
            textProperty.SetValue(item, text);
        }

        if (valueProperty != null)
        {
            valueProperty.SetValue(item, value);
        }

        return (TItem)item;
    }

    #endregion
}
