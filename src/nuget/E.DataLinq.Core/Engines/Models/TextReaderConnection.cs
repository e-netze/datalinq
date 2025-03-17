using System;
using System.IO;
using System.Reflection;

namespace E.DataLinq.Core.Engines.Models;

internal class TextReaderConnection
{
    public string File { get; private set; } = String.Empty;
    public int MaxLines { get; private set; } = 1000;
    public ReadFrom From { get; private set; } = ReadFrom.Top;
    public string Filter { get; private set; } = String.Empty;

    static public TextReaderConnection FromQueryStatement(string queryStatement)
    {
        try
        {
            var textReaderConnection = new TextReaderConnection();

            using (var reader = new StringReader(queryStatement))
            {
                string statement;

                while ((statement = reader.ReadLine()?.Trim()) != null)
                {
                    if (string.IsNullOrEmpty(statement) || statement.StartsWith("//"))
                    {
                        continue;
                    }

                    if (String.IsNullOrEmpty(textReaderConnection.File) && !statement.Contains("="))
                    {
                        textReaderConnection.File = statement;
                        continue;
                    }

                    #region Parse Lines like From=Botton and set the property

                    string[] parts = statement.Split('=');
                    if (parts.Length != 2)
                    {
                        continue; // Skip lines that don't have a valid format
                    }

                    string propName = parts[0].Trim();
                    string propValue = parts[1].Trim();

                    // Use reflection to set the corresponding property value
                    PropertyInfo propInfo = typeof(TextReaderConnection).GetProperty(propName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (propInfo != null && propInfo.CanWrite)
                    {
                        object value;
                        if (propInfo.PropertyType.IsEnum)
                        {
                            value = Enum.Parse(propInfo.PropertyType, propValue, true);
                        }
                        else
                        {
                            value = Convert.ChangeType(propValue, propInfo.PropertyType);
                        }

                        propInfo.SetValue(textReaderConnection, value);
                    }

                    #endregion
                }
            }

            return textReaderConnection;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error parsing reader connection: {ex.Message}");
        }
    }
}
