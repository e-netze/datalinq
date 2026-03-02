using E.DataLinq.Core.Services.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

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

    public static IHtmlContent DisplayNameLabelFor<TModel, TValue>(this IHtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression)
    {
        var member = (expression?.Body as MemberExpression)?.Member;
        if (member == null) return HtmlString.Empty;

        var displayNameVar = member.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

        string displayName = displayNameVar switch
        {
            null or "" => member.Name,                          // No attribute → use property name
            _ when displayNameVar.StartsWith('#') =>            // "#key" → localize
                html.ViewContext.HttpContext.RequestServices.GetRequiredService<MarkdownLocalizer>().Get(displayNameVar[1..]),
            _ => displayNameVar                                 
        };

        return string.IsNullOrEmpty(displayName) ? HtmlString.Empty : new HtmlString($"<label class='display-name'>{displayName}</label>");
    }

    public static IHtmlContent DescriptionFor<TModel, TValue>(this IHtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression)
    {
        var descriptionVar = (expression?.Body as MemberExpression)?.Member?.GetDescription();

        if (string.IsNullOrEmpty(descriptionVar))
            return HtmlString.Empty;

        string description = descriptionVar.StartsWith('#') ? html.ViewContext.HttpContext.RequestServices.GetRequiredService<MarkdownLocalizer>().Get(descriptionVar[1..]) : descriptionVar;

        return string.IsNullOrEmpty(description) ? HtmlString.Empty : new HtmlString($"<p class='description'>{description}</p>");
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
