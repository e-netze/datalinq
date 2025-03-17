using System.Text;

namespace E.DataLinq.Core;

public interface IDataLinqHelper
{
    void AppendHtmlAttributes(StringBuilder sb, object htmlAttributes, string addClass = "");
    void AppendHtmlAttribute(StringBuilder sb, string attributeName, string attributeValue);
    object ToRawString(string str);
}
