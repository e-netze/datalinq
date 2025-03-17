using E.DataLinq.Web.Services;
using System;
using System.Linq;

namespace E.DataLinq.Web.Extensions;

static class DataLinqCodeApiOptionsExtensions
{
    static public void CheckDataLinqCodeClientUrl(this DataLinqCodeApiOptions options, string redirectUrl)
    {
        if (options?.DataLinqCodeClients != null && options.DataLinqCodeClients.Length > 0)
        {
            redirectUrl = redirectUrl.ToLower();

            if (options.DataLinqCodeClients.Where(c => redirectUrl.StartsWith(c.ToLower())).Any() == false)
            {
                throw new Exception($"Invalid client {redirectUrl}");
            }
        }
    }
}
