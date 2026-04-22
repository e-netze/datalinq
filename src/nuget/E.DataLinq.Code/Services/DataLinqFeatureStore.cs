using E.DataLinq.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace E.DataLinq.Code.Services;

public class DataLinqFeatureStore
{
    public FeaturesResult Features { get; private set; } = new FeaturesResult();

    public void SetFeatures(FeaturesResult features)
    {
        Features = features;
    }
}
