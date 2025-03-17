using E.DataLinq.Core.Models.Abstraction;
using E.DataLinq.Core.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace E.DataLinq.Code.Extensions;

static class FormCollectionExtensions
{
    async static public Task AddAuthProperties(this IFormCollection form,
                                               IDataLinqAuthProperties authProperties,
                                               IDataLinqAccessTreeService accessTree)
    {
        var token0 = form["datalinq_token0"];
        var token1 = form["datalinq_token1"];

        if (!String.IsNullOrEmpty(token0) && !String.IsNullOrEmpty(token1))
        {
            authProperties.AccessTokens = new string[] { token0, token1 };
        }
        else if (!String.IsNullOrEmpty(token0) || !String.IsNullOrEmpty(token1))
        {
            authProperties.AccessTokens = new string[] { token0, token1 };
            throw new Exception("Invalid access tokens: both one or none of them must be a valid token");
        }

        var accessString = form["access_string"].ToString();
        if (!String.IsNullOrEmpty(accessString))
        {
            authProperties.Access = accessString.Split(',');
        }

        var tree = form["access_tree"].ToString();
        await accessTree.SetSelectedTreeNodes(authProperties.Route,
                                              tree?.Split(",") ?? Array.Empty<string>());
    }
}
