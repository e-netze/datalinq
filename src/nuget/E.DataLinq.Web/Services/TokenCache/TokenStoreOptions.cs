using E.DataLinq.Web.Extensions.DependencyInjection;
using E.DataLinq.Web.Html.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Services.TokenCache;

public class TokenStoreOptions
{
    public const string Key = "TokenCache";
    public DataLinqCacheTokenStorageType StorageType { get; set; } = DataLinqCacheTokenStorageType.File;
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan DefaultTTL { get; set; } = TimeSpan.FromHours(1);
    public int? DefaultMaxUsage { get; set; } = 1;
    public bool EnableBackgroundCleanup { get; set; } = false;
    public TimeSpan CleanupIntervalMinutes { get; set; } = TimeSpan.FromMinutes(30);
}
