using System.Collections.Specialized;
using System.Xml;

namespace E.DataLinq.Web.Extensions;

static class XmlDocumentExtensions
{
    static public NameValueCollection RazorConstants(this XmlDocument config)
    {
        NameValueCollection ret = new NameValueCollection();

        if (config != null)
        {
            foreach (XmlNode constNode in config.SelectNodes("configuration/const/add[@name and @value]"))
            {
                ret[constNode.Attributes["name"].Value] = constNode.Attributes["value"].Value;
            }
        }

        return ret;
    }
}
