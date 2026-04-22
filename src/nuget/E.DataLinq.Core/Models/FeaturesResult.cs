using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Models;

public class FeaturesResult
{
    public bool VersionControl { get; set; } = false;
    public bool Sandbox { get; set; } = false;
    public bool Copilot { get; set; } = false;
}
