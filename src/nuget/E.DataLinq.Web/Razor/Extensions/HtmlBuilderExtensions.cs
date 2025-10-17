using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Html.Abstractions;
using E.DataLinq.Web.Html.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Razor.Extensions;

static internal class HtmlBuilderExtensions
{
    static public T AppendRefreshViewClickButtton<T>(this IHtmlParentElementBuilder<T> htmlBuilder, string label = "Aktualisieren", object htmlAttributes = null)
        => htmlBuilder.AppendButton(button =>
                        button.AddClass("datalinq-button apply")
                              .AddAttributes(htmlAttributes)
                              .AddAttribute("onclick", "dataLinq.refresh(this)")
                              .Content(label)
                    );

    static public T ComboFor<T>(this IHtmlParentElementBuilder<T> htmlBuilder,
                                object val,
                                string name,
                                object htmlAttributes = null,
                                object source = null)
    {
        return htmlBuilder.AppendSelect(select =>
        {
            select.AddClass("datalinq-include-combo");
            select.AddAttributes(htmlAttributes);
            select.AddAttribute("name", name);

            if (source != null && source.GetType().GetProperty("source") != null)
            {
                var sourceProperty = source.GetType().GetProperty("source");
                var sourceValue = sourceProperty.GetValue(source);
                bool prependEmpty = Convert.ToBoolean(source.ToDictionary().GetDefaultValueFromRecord("prependEmpty", false));

                select.AddAttribute("data-prepend-empty", prependEmpty.ToString().ToLower());
                select.AddAttribute("data-defaultvalue", val == null ? "" : val.ToString());

                var dependsOn = source.ToDictionary().GetDefaultValueFromRecord("dependsOn", null) as string[];
                if (dependsOn != null && dependsOn.Length > 0)
                {
                    select.AddAttribute("data-depends-on", String.Join(",", dependsOn));
                }

                if (sourceValue is string[])
                {
                    foreach (var optionValue in (string[])sourceValue)
                    {
                        select.AppendSelectOption(option =>
                        {
                            option
                                .WithValue(optionValue)
                                .AsSelectedIf(optionValue == val?.ToString())
                                .Content(optionValue);
                        });
                    }
                }
                else if (sourceValue is Dictionary<object, string>)
                {
                    foreach (var kvp in (Dictionary<object, string>)sourceValue)
                    {
                        select.AppendSelectOption(option =>
                        {
                            option
                                .WithValue(kvp.Key?.ToString() ?? "")
                                .AsSelectedIf(kvp.Key?.ToString() == val?.ToString())
                                .Content(kvp.Value ?? "");
                        });
                    }
                }
                else if (sourceValue is string)
                {
                    var valueFieldProperty = source.GetType().GetProperty("valueField");
                    var nameFieldProperty = source.GetType().GetProperty("nameField");

                    if (valueFieldProperty == null || nameFieldProperty == null)
                    {
                        valueFieldProperty.SetValue(source, "VALUE");
                        nameFieldProperty.SetValue(source, "NAME");
                    }

                    if (valueFieldProperty.PropertyType == typeof(string) && nameFieldProperty.PropertyType == typeof(string))
                    {
                        select.AddAttribute("data-url", sourceProperty.GetValue(source).ToString());
                        select.AddAttribute("data-valuefield", valueFieldProperty.GetValue(source)?.ToString() ?? "");
                        select.AddAttribute("data-namefield", nameFieldProperty.GetValue(source)?.ToString() ?? "");
                    }
                    else
                    {
                        throw new ArgumentException("valueField and nameField have to be typeof(string)");
                    }
                }
            }
        });
    }

    static public IHtmlElementBuilder AsSelectedIf(this IHtmlElementBuilder htmlBuilder, bool isSelected)
    {
        if (isSelected)
        {
            htmlBuilder.AddAttribute("selected", "selected");
        }
        return htmlBuilder;
    }
}