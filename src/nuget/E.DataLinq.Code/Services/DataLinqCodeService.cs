using E.DataLinq.Code.Extensions;
using E.DataLinq.Core.Security.Token.Models;
using E.DataLinq.Core.Services.Abstraction;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using E.DataLinq.Web.Api.Client;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace E.DataLinq.Code.Services;

public class DataLinqCodeService
{
    private readonly DataLinqCodeIndentityService _identity;
    private readonly DataLinqCodeOptions _options;

    private readonly CodeApiClient _client = null;
    private readonly string _instanceName = "";
    private readonly string _datalinqEngineUrl = null;
    private readonly string _logoutUrl = null;
    private readonly string _userDisplayName = null;
    private readonly string _accessToken = null;
    private readonly ICryptoService _crypto;

    public DataLinqCodeService(IHostUrlHelper urlHelper,
                               ICryptoService crypto,
                               DataLinqCodeIndentityService identity,
                               IOptions<DataLinqCodeOptions> options)
    {
        _identity = identity;
        _options = options.Value;
        _crypto = crypto;

        _options.Intialize(urlHelper, crypto);

        var cookieData = identity.IdentityData();

        if (cookieData.id.HasValue &&
            cookieData.id.Value < _options.DatalinqInstances.Count())
        {
            var dataLinqInstance = _options.DatalinqInstances[cookieData.id.Value];

            _client = new CodeApiClient(dataLinqInstance.CodeApiClientUrl, cookieData.accessToken);

            _instanceName = dataLinqInstance.Name;

            // _datalinqEngineUrl is the url that is reachable by the enduser
            // if the CodeApiClientUrl is the same as the LoginUrl, then the datalinqEngineUrl is the CodeApiClientUrl + /datalinq
            // otherwise, the _datalinqEngineUrl is the LoginUrl without the /DataLinqAuth part + /datalinq
            // 
            // because CodeApiClientUrl can be an internal url from DataLinq.Code to DataLinq.CodeApi 
            // eg, if the the Apps running in containers (Docker, Kubernetes) 

            _datalinqEngineUrl = (dataLinqInstance.CodeApiClientUrl, dataLinqInstance.LoginUrl) switch
            {
                (null, null) => throw new ArgumentException("CodeApiClientUrl and LoginUrl are null"),
                (null, _) => $"{dataLinqInstance.LoginUrl.RemoveAllAt("/DataLinqAuth")}/datalinq",
                (var codeApiClientUrl, var loginUrl) when loginUrl.StartsWith(codeApiClientUrl, StringComparison.OrdinalIgnoreCase) => $"{codeApiClientUrl}/datalinq",
                _ => $"{dataLinqInstance.LoginUrl.RemoveAllAt("/DataLinqAuth")}/datalinq"
            };

            _logoutUrl = dataLinqInstance.LogoutUrl;
            _userDisplayName = cookieData.userDisplayName;

            _accessToken = cookieData.accessToken;
        }
    }

    public CodeApiClient ApiClient => _client;

    public string LogoutUrl => _logoutUrl;
    public string UserDisplayName => _userDisplayName;

    public bool UseAppPrefixFilters => _options.UseAppPrefixFilters;

    public string DataLinqEngineUrl => _datalinqEngineUrl;

    public string InstanceName => _instanceName;

    public IEnumerable<DataLinqCodeOptions.DataLinqInstance> Instances => _options.DatalinqInstances.ToArray();

    public string AccessToken => _accessToken;

    public Payload AccessTokenPayload
    {
        get
        {
            if (String.IsNullOrEmpty(_accessToken) || _accessToken.Split('.').Length < 3)
            {
                return new Payload();
            }

            return JsonConvert.DeserializeObject<Payload>(Encoding.UTF8.GetString(Convert.FromBase64String(_accessToken.Split('.')[1])));
        }
    }
}
