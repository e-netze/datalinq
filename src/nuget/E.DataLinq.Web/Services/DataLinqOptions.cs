using E.DataLinq.Core;
using E.DataLinq.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace E.DataLinq.Web.Services;

public class DataLinqOptions
{
    public DataLinqOptions()
    {
        this.CssInPerstanceStorage = true;
        this.JavascriptInPerstanceStorage = true;
        this.RunGarbageCollectAfterCompile = true;
        this.AllowAccessControlAllowWildcards = false;

        this.AddToRazorBlackList(new[] {
            "System.",
            "Microsoft.",
            //"System.IO",
            //"System.Environment",
            //"System.Diagnostics",
            //"System.Drawing",
            //"System.Task",
            //"System.Threading"
        });

        //this.AddToRazorWhiteList(new[]
        //{
        //    "DXImageTransform.Microsoft.BasicImage"
        //});

        this.AllowUsingNamespaces = false;

        this.AddRazorNamespace("System.Text");

        this.AddSupportedEndPointTypes<DefaultEndPointTypes>();
    }

    public DataLinqEnvironmentType EnvironmentType { get; set; } = DataLinqEnvironmentType.Default;

    public bool CssInPerstanceStorage { get; set; }
    public bool JavascriptInPerstanceStorage { get; set; }

    public bool RunGarbageCollectAfterCompile { get; set; }

    public string TempPath { get; set; }

    public bool AllowAccessControlAllowWildcards { get; set; }

    public string EngineId { get; set; } = RazorEngineIds.DataLinqLanguageEngineRazor;

    private IEnumerable<string> _customReportCssUrls;
    public IEnumerable<string> CustomReportCssUrls
    {
        get { return _customReportCssUrls ?? new string[0]; }
        set { _customReportCssUrls = value; }
    }

    private IEnumerable<string> _customReportJavascriptUrls;
    public IEnumerable<string> CustomReportJavascriptUrls
    {
        get { return _customReportJavascriptUrls ?? new string[0]; }
        set { _customReportJavascriptUrls = value; }
    }

    private List<string> _razorNamespaces = new List<string>();
    public IEnumerable<string> RazorNamespaces => _razorNamespaces.ToArray();

    public void AddRazorNamespace(string razorNameSpace)
    {
        if (!_razorNamespaces.Contains(razorNameSpace))
        {
            _razorNamespaces.Add(razorNameSpace);
        }
    }
    public void ResetRazorNamespaces()
    {
        _razorNamespaces.Clear();
    }

    private List<Assembly> _assemblyReferences = new List<Assembly>();
    public void AddAssemblyReferene(Assembly assembly)
    {
        _assemblyReferences.Add(assembly);
    }
    public IEnumerable<Assembly> AssemblyReferences => _assemblyReferences.ToArray();

    private List<Type> _endPointTypes = new List<Type>();
    internal IEnumerable<Type> SupportedEndPointTypes => _endPointTypes.ToArray();
    public void ResetSupportedEndPointTypes()
    {
        _endPointTypes.Clear();
    }
    public void AddSupportedEndPointTypes<T>()
        where T : Enum
    {
        _endPointTypes.Add(typeof(T));
    }

    #region BlackList 

    public ConcurrentBag<string> _razorBlackList = new ConcurrentBag<string>();
    public void AddToRazorBlackList(IEnumerable<string> forbiddenPhrases)
    {
        foreach (var forbiddenPhrase in forbiddenPhrases)
        {
            _razorBlackList.Add(forbiddenPhrase);
        }
    }

    public void ClearRazorBlackList()
    {
        _razorBlackList.Clear();
    }

    public IEnumerable<string> RazorBlackList => _razorBlackList.ToArray();

    public bool AllowUsingNamespaces { get; set; }

    #endregion

    #region WhiteList

    public ConcurrentBag<string> _razorWhiteList = new ConcurrentBag<string>();
    public void AddToRazorWhiteList(IEnumerable<string> allowedPhrases)
    {
        foreach (var allowedPhrase in allowedPhrases)
        {
            _razorWhiteList.Add(allowedPhrase);
        }
    }

    public void ClearRazorWhiteList()
    {
        _razorWhiteList.Clear();
    }

    public IEnumerable<string> RazorWhiteList => _razorWhiteList.ToArray();

    #endregion
}
