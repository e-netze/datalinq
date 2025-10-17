using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services.Abstraction
{
    public interface IMonacoSnippetService
    {
        string BuildSnippetJson(string lang);
    }
}
