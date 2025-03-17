using E.DataLinq.Core.Models;
using System;

namespace E.DataLinq.Web.Extensions;
static internal class SuccessModelExtensions
{
    static public SuccessModel OnSuccess(this SuccessModel model, Action<SuccessModel> action)
    {
        if (model != null && model.Success == true)
        {
            action(model);
        }

        return model;
    }
}
