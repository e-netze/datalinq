using System.Collections.Generic;

namespace E.DataLinq.Web.Models;

public class HelpModel
{
    public HelpModel()
    {
        this.Classes = new List<ClassHelp>();
    }

    public List<ClassHelp> Classes { get; }

    public string Selected { get; set; }
}
