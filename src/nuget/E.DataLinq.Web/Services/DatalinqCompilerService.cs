using E.DataLinq.Core;
using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Models;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace E.DataLinq.Web.Services;

public class DataLinqCompilerService
{
    private readonly IEnumerable<IRazorCompileEngineService> _razorEngines;
    private readonly DataLinqOptions _options;

    public DataLinqCompilerService(IEnumerable<IRazorCompileEngineService> razorEngines,
                                   IOptionsMonitor<DataLinqOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;
        _razorEngines = razorEngines;
    }

    public Task ValidateRazorCode(DataLinqEndPointQueryView view)
    {
        if (String.IsNullOrEmpty(view?.Code))
        {
            return Task.CompletedTask;
        }

        CheckRazorBlackAndWhiteList(view.Code);

        string constants = ConfigXmlDocument("datalinq")
                                .RazorConstants()
                                .ToCSharpConstants("Const");

        StringBuilder code = CreateCodeStringBuilder(constants);

        code.Append(view.Code);

        string tempalteId = Guid.NewGuid().ToString();

        return _razorEngines
                .GetRazorEngineService(_options, view.Code)
                .RunCompile<SelectResult>(code.ToString(), tempalteId);
    }

    async public Task<string> RenderRazorView(
            DataLinqService datalinq,
            HttpContext httpContext,
            DataLinqEndPointQueryView view,
            string id,
            object[] records,
            IDataLinqUser ui,
            DateTime startTime)
    {
        if (String.IsNullOrEmpty(view?.Code))
        {
            return String.Empty;
        }

        var razorEngineService = _razorEngines.GetRazorEngineService(_options, view.Code);

        var model = new SelectResult(httpContext, razorEngineService, datalinq, httpContext.Request, startTime, records, ui);

        string htmlResultString = String.Empty;
        string constants = ConfigXmlDocument("datalinq")?
                                .RazorConstants()?
                                .ToCSharpConstants("Const");

        var code = CreateCodeStringBuilder(constants);
        code.Append(view.Code);

        try
        {
            string razorCacheId = $"{id}-{view.Changed.Ticks.ToString()}";

            if (!razorEngineService.IsCompilationCached(razorCacheId, typeof(SelectResult)))
            {
                CheckRazorBlackAndWhiteList(view.Code);
            }

            // just for checking cuncurrency issues
            //var tasks = new Task<string>[] {
            //    razorEngineService.RunCompile<SelectResult>(code.ToString(), razorCacheId, model),
            //    razorEngineService.RunCompile<SelectResult>(code.ToString(), razorCacheId, model),
            //    razorEngineService.RunCompile<SelectResult>(code.ToString(), razorCacheId, model)
            //};

            //tasks[0].Wait();
            //htmlResultString = tasks[0].Result;

            htmlResultString = await razorEngineService.RunCompile<SelectResult>(code.ToString(), razorCacheId, model);
        }
        catch (RazorCompileException ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div style='background-color:#efefaa'>");
            foreach (var error in ex.CompilerErrors)
            {
                if (error.IsWarning)
                {
                    continue;
                }

                sb.Append($"<div>ERROR: {HttpUtility.HtmlEncode(error.ErrorText)}</div>");
                sb.Append($"<div>{HttpUtility.HtmlEncode(error.CodeLine)}</div>");
                sb.Append($"<div>Line: {error.Line} Column: {error.Column}</div>");
            }
            sb.Append("</div>");
            htmlResultString = sb.ToString();
        }

        return htmlResultString;
    }

    public IRazorCompileEngineService RazorEngine => _razorEngines.GetRazorEngineService(_options, "");

    #region Config

    private System.Xml.XmlDocument ConfigXmlDocument(string name)
    {
        if (!name.Contains("."))
        {
            name += ".config";
        }

        try
        {
            var fi = new FileInfo($"{System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/_config/" + name);
            if (fi.Exists)
            {
                System.Xml.XmlDocument config = new System.Xml.XmlDocument();
                config.Load(fi.FullName);
                return config;
            }
        }
        catch
        {

        }

        return null;
    }

    #endregion

    #region Helper

    private StringBuilder CreateCodeStringBuilder(string constants)
    {
        StringBuilder razorCode = new StringBuilder();

        razorCode.Append("@using E.DataLinq.Web.Razor;");
        razorCode.Append(Environment.NewLine);

        foreach (var razorNamespace in _options.RazorNamespaces)
        {
            razorCode.Append($"@using {razorNamespace};");
            razorCode.Append(Environment.NewLine);
        }

        razorCode.Append("@{ var IntegratorHelper=Model?.CreateDataLinqHelper(); var IH=IntegratorHelper; var DataLinqHelper=IH; var DLH=IH;");
        if (!String.IsNullOrWhiteSpace(constants))
        {
            razorCode.Append(constants);
        }
        razorCode.Append("}");
        razorCode.Append(Environment.NewLine);

        return razorCode;
    }

    private void CheckRazorBlackAndWhiteList(string code, bool cleanedCode = false)
    {
        var blackList = _options.RazorBlackList;
        var whiteList = _options.RazorWhiteList;

        StringBuilder uncommentedCode = new();

        using (var stringReader = new StringReader(code))
        {
            string line;
            int i = 1;

            while ((line = stringReader.ReadLine()) != null)
            {
                if (_options.AllowUsingNamespaces == false)
                {
                    if (line.Contains("@using "))
                    {
                        string usingLine = line.Substring(line.IndexOf("@using"));

                        int posEqual = line.IndexOf("=");
                        int posBracket = line.IndexOf("(");

                        if (posEqual < 0 ||
                            posBracket < 0 ||
                            posBracket > posEqual)
                        {
                            throw new RazorCompileException("Blacklist Exception")
                            {
                                CompilerErrors = new RazorCompileError[]
                                {
                                    cleanedCode
                                    ? new RazorCompileError()
                                    {
                                        CodeLine = line,
                                        ErrorText = $"Suspicious code detected: @using namespaces is not allowed ({line})"
                                    }
                                    : new RazorCompileError()
                                    {
                                        Line = i,
                                        CodeLine = line,
                                        ErrorText = $"@using namespaces is not allowed"
                                    }
                                }
                            };
                        }
                    }
                }

                string whiteListedLine = line;
                foreach (var item in whiteList)
                {
                    whiteListedLine = whiteListedLine.Replace(item, ""); // remove/ignore white list items
                }

                foreach (var item in blackList)
                {
                    if (whiteListedLine.Contains(item))
                    {
                        throw new RazorCompileException("Blacklist Exception")
                        {
                            CompilerErrors = new RazorCompileError[]
                            {
                                cleanedCode
                                ? new RazorCompileError()
                                {
                                    CodeLine = line,
                                    ErrorText = $"Suspicious code detected: {item} is not allowed with DataLinq Pages ({line})"
                                }
                                : new RazorCompileError()
                                {
                                    Line = i,
                                    Column = line.IndexOf(item),
                                    CodeLine = line,
                                    ErrorText = $"{ item } is not allowed with DataLinq Pages"
                                }
                            }
                        };
                    }
                }

                i++;
            }
        }

        if (!cleanedCode)
        {
            code = code.CleanRazorString();

            CheckRazorBlackAndWhiteList(code, true);
        }
    }



    #endregion
}
