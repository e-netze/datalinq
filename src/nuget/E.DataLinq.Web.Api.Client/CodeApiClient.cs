using E.DataLinq.Core.Exceptions;
using E.DataLinq.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace E.DataLinq.Web.Api.Client;

public class CodeApiClient
{
    private static HttpClient ReuseableHttpClient = null;

    private readonly HttpClient _httpClient;
    private readonly string _targetUrl;
    private readonly string _accessToken;
    private readonly string _apiPath;

    public CodeApiClient(string targetUrl,
                         string accessToken = "",
                         HttpClient httpClient = null)
    {
        if (httpClient == null && ReuseableHttpClient == null)
        {
            var handler = new HttpClientHandler()
            {
#pragma warning disable SYSLIB0039 // allow old protocols (tls, tls11)
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls,
#pragma warning restore SYSLIB0039 // allow old protocols (tls, tls11)
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };

            ReuseableHttpClient = new HttpClient(handler);
        }

        _httpClient = httpClient ?? ReuseableHttpClient;
        _targetUrl = targetUrl;
        _accessToken = accessToken;

        _apiPath = "datalinqcodeapi";
    }

    async public Task<DataLinqEndPoint> GetEndPoint(string endPointId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/get/{endPointId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<DataLinqEndPoint>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    async public Task<DataLinqEndPointQuery> GetEndPointQuery(string endPointId, string queryId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/get/{endPointId}/{queryId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var query = JsonConvert.DeserializeObject<DataLinqEndPointQuery>(await GetAndCheckHttpResponseAsync(httpResponse));
                query.EndPointId = String.IsNullOrEmpty(query.EndPointId) ? endPointId.ToLower() : query.EndPointId;

                return query;
            }
        }
    }

    async public Task<DataLinqEndPointQueryView> GetEndPointQueryView(string endPointId, string queryId, string viewId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/get/{endPointId}/{queryId}/{viewId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var view = JsonConvert.DeserializeObject<DataLinqEndPointQueryView>(await GetAndCheckHttpResponseAsync(httpResponse));
                view.EndPointId = String.IsNullOrEmpty(view.EndPointId) ? endPointId.ToLower() : view.EndPointId;
                view.QueryId = String.IsNullOrEmpty(view.QueryId) ? queryId.ToLower() : view.QueryId;

                return view;
            }
        }
    }

    async public Task<string> GetEndPointCss(string endPointId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/css/{endPointId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return await GetAndCheckHttpResponseAsync(httpResponse);
            }
        }
    }

    async public Task<string> GetViewCss(string id)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/css/view/{id}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return await GetAndCheckHttpResponseAsync(httpResponse);
            }
        }
    }

    async public Task<string> GetEndPointJavascript(string endPointId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/js/{endPointId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return await GetAndCheckHttpResponseAsync(httpResponse);
            }
        }
    }

    async public Task<string> GetViewJs(string id)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/js/view/{id}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return await GetAndCheckHttpResponseAsync(httpResponse);
            }
        }
    }

    async public Task<IDictionary<int, string>> GetEndPointTypes()
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/types/endpoint"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<IDictionary<int, string>>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    async public Task<IDictionary<string, IEnumerable<string>>> GetEndPointPrefixes()
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/endpointprefixes"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<IDictionary<string, IEnumerable<string>>>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    async public Task<IEnumerable<string>> GetEndPoints(IEnumerable<string> filters)
    {
        string filtersParameter = "";

        filters = filters?
            .Where(f => !String.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim());

        if (filters != null && filters.Count() > 0)
        {
            filtersParameter = $"?filters={String.Join(",", filters)}";
        }

        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/endpoints{filtersParameter}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<string[]>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    async public Task<IEnumerable<string>> GetEndPointQueries(string endPointId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/{endPointId}/queries"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<string[]>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    async public Task<IEnumerable<string>> GetEndPointQueryViews(string endPointId, string queryId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/{endPointId}/{queryId}/views"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<string[]>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    public async Task<string> GetMonacoSnippit(string lang)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/monacosnippit?lang={Uri.EscapeDataString(lang)}");
        ModifyHttpRequest(requestMessage);

        using var httpResponse = await _httpClient.SendAsync(requestMessage);
        httpResponse.EnsureSuccessStatusCode();

        return await httpResponse.Content.ReadAsStringAsync();
    }

    async public Task<IEnumerable<string>> GetAuthPrefixes()
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/auth/prefixes"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<string[]>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    async public Task<IEnumerable<string>> GetAuthAutocomplete(string prefix, string term)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/auth/autocomplete?prefix={HttpUtility.UrlEncode(prefix)}&term={HttpUtility.UrlEncode(term)}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<string[]>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    public async Task<string> AskDataLinqCopilot(string[] questions)
    {
        var requestPayload = new { questions = questions };
        var jsonContent = JsonConvert.SerializeObject(requestPayload);

        using (var content = new StringContent(jsonContent, Encoding.UTF8, "application/json"))
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/askdatalinqcopilot"))
        {
            requestMessage.Content = content;
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var responseText = await GetAndCheckHttpResponseAsync(httpResponse);
                return responseText;
            }
        }
    }

    async public Task<bool> StoreEndPoint(DataLinqEndPoint endPoint)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/post/endpoint"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new StringContent(
                JsonConvert.SerializeObject(endPoint),
                Encoding.UTF8,
                "application/json");

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> StoreEndPointCss(string endPointId, string css)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/post/endpointcss"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("endPointId", endPointId),
                new KeyValuePair<string, string>("css", css)
            });

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> StoreViewCss(string id, string css)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/post/viewcss"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", id),
                new KeyValuePair<string, string>("css", css)
            });

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> StoreEndPointJavascript(string endPointId, string javascript)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/post/endpointjs"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("endPointId", endPointId),
                new KeyValuePair<string, string>("js", javascript)
            });

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> StoreViewJs(string id, string js)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/post/viewjs"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", id),
                new KeyValuePair<string, string>("js", js)
            });

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> StoreEndPointQuery(DataLinqEndPointQuery query)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/post/{query.EndPointId}/query"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new StringContent(
                JsonConvert.SerializeObject(query),
                Encoding.UTF8,
                "application/json");

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> StoreEndPointQueryView(DataLinqEndPointQueryView view, bool verifyOnly = false)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_targetUrl}/{_apiPath}/post/{view.EndPointId}/{view.QueryId}/view?verifyOnly={verifyOnly}"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new StringContent(
                JsonConvert.SerializeObject(view),
                Encoding.UTF8,
                "application/json");

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                string response = await GetAndCheckHttpResponseAsync(httpResponse, false);
                var result = JsonConvert.DeserializeObject<SuccessModel>(response);

                if (result.Success == false)
                {
                    throw new RazorCompileException(result.ErrorMessage)
                    {
                        CompilerErrors = result.CompilerErrors
                    };
                }

                return true;
            }
        }
    }

    async public Task<bool> VerfifyEndPointQueryView(string endPointId, string queryId, string viewId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/verify/{endPointId}/{queryId}/{viewId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                string response = await GetAndCheckHttpResponseAsync(httpResponse, false);
                var result = JsonConvert.DeserializeObject<SuccessModel>(response);

                if (result.Success == false)
                {
                    throw new RazorCompileException(result.ErrorMessage)
                    {
                        CompilerErrors = result.CompilerErrors
                    };
                }

                return true;
            }
        }
    }

    async public Task<SuccessCreatedModel> CreateEndPoint(string endPointId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/create/{endPointId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessCreatedModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return result;
            }
        }
    }

    async public Task<SuccessCreatedModel> CreateEndPointQuery(string endPointId, string queryId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/create/{endPointId}/{queryId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessCreatedModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return result;
            }
        }
    }

    async public Task<SuccessCreatedModel> CreateEndPointQueryView(string endPointId, string queryId, string viewId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/create/{endPointId}/{queryId}/{viewId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessCreatedModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return result;
            }
        }
    }

    async public Task<bool> DeleteEndPoint(string endPointId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/delete/{endPointId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> DeleteEndPointQuery(string endPointId, string queryId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/delete/{endPointId}/{queryId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<bool> DeleteEndPointQueryView(string endPointId, string queryId, string viewId)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/delete/{endPointId}/{queryId}/{viewId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                var result = JsonConvert.DeserializeObject<SuccessModel>(await GetAndCheckHttpResponseAsync(httpResponse, false));

                if (result.Success == false)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return true;
            }
        }
    }

    async public Task<IEnumerable<JsLibrary>> GetJsLibraries()
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_targetUrl}/{_apiPath}/capabilities/jslibs"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage))
            {
                return JsonConvert.DeserializeObject<JsLibrary[]>(await GetAndCheckHttpResponseAsync(httpResponse));
            }
        }
    }

    #region Helper

    private void ModifyHttpRequest(HttpRequestMessage requestMessage)
    {
        if (!String.IsNullOrEmpty(_accessToken))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("datalinq-token", _accessToken);
        }
    }

    async private Task<string> GetAndCheckHttpResponseAsync(HttpResponseMessage httpResponse, bool checkForExceptionResponse = true)
    {
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Error connecting with datalinq api service. Status code: {httpResponse.StatusCode}");
        }

        var response = await httpResponse.Content.ReadAsStringAsync();

        if (checkForExceptionResponse && response.Trim().StartsWith("{"))
        {
            SuccessModel successModel = null;

            try
            {
                successModel = JsonConvert.DeserializeObject<SuccessModel>(response);
            }
            catch { }

            if (!String.IsNullOrEmpty(successModel?.ErrorMessage) && successModel.Success == false)
            {
                throw new Exception(successModel.ErrorMessage);
            }
        }

        return response;
    }

    #endregion
}
