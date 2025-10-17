using E.DataLinq.Code.Extensions;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using System;

namespace E.DataLinq.Code.Services;

public class DataLinqCodeOptions
{
    public string LoginRedirectUrl { get; set; }

    public DataLinqInstance[] DatalinqInstances { get; set; }

    public string ProjectWebSite { get; set; } = "https://github.com/e-netze/datalinq";

    public bool UseAppPrefixFilters { get; set; }

    #region Classes

    public class DataLinqInstance
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string CodeApiClientUrl { get; set; }
        public string LoginUrl { get; set; }
        public string LogoutUrl { get; set; }
    }

    #endregion

    private bool _intialialized = false;
    public void Intialize(IHostUrlHelper urlHelper,
                         ICryptoService crypto)
    {
        if (_intialialized)
        {
            return;
        }

        _intialialized = true;

        var defaultApiUrl = urlHelper.HostAppRootUrl();
        string loginRedirectUrl = LoginRedirectUrl
                                          .IfNullOrEmpty(defaultApiUrl)
                                          .AppendLoginRedirectPath();

        if (this.DatalinqInstances == null)
        {
            this.DatalinqInstances = new DataLinqCodeOptions.DataLinqInstance[]
            {
                new DataLinqCodeOptions.DataLinqInstance()
                {
                    Name = "Local",
                    Description = "A local datalinq instance for testing and development",
                    LoginUrl = String.Format(defaultApiUrl.AppendCodeApiLoginPath(), String.Format(loginRedirectUrl,crypto.EncryptTextDefault("0", Core.Services.Crypto.CryptoResultStringType.Hex))),
                    LogoutUrl = defaultApiUrl.AppendCodeApiLogoutPath(),
                    CodeApiClientUrl = defaultApiUrl
                }
            };
        }
        else
        {
            int index = 0;

            foreach (var instance in this.DatalinqInstances)
            {
                instance.LoginUrl = String.Format(instance.LoginUrl
                                                          .Replace("~", defaultApiUrl)
                                                          .AppendCodeApiLoginPath(), String.Format(loginRedirectUrl, crypto.EncryptTextDefault((index++).ToString(), Core.Services.Crypto.CryptoResultStringType.Hex)));
                instance.LogoutUrl = String.Format(instance.LogoutUrl
                                                           .IfNullOrEmpty(instance.LoginUrl)
                                                           .Replace("~", defaultApiUrl)
                                                           .AppendCodeApiLogoutPath(), urlHelper.HostAppRootUrl());
                instance.CodeApiClientUrl = instance.CodeApiClientUrl.Replace("~", defaultApiUrl);
            }
        }
    }
}
