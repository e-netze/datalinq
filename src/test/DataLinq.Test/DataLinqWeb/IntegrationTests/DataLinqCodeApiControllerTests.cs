using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using E.DataLinq.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace E.DataLinq.Test.DataLinqWeb.IntegrationTests;

[TestClass]
public class DataLinqCodeCodeApiControllerTests
{
    private static readonly string ENDPOINTS_STORAGE_PATH = "c:\\temp\\datalinq-tests\\storage";

    //Setup
    #region Init

    static private HttpClient _clientAuthorized;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var storagePathDirectory = new DirectoryInfo(ENDPOINTS_STORAGE_PATH);
        if (storagePathDirectory.Exists)
        {
            storagePathDirectory.Delete(true);
        }

        // Initialization of Aspire
        var appHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DataLinq_AppHost>();

        var datalinq_api = appHostBuilder.CreateResourceBuilder<ProjectResource>("datalinq-api");
        datalinq_api.WithEnvironment(e =>
        {
            e.EnvironmentVariables.Add("DataLinq.Api__StoragePath", ENDPOINTS_STORAGE_PATH);
        });

        var appHost = await appHostBuilder.BuildAsync();
        await appHost.StartAsync();

        var webResource = appHostBuilder.Resources.Where(r => r.Name == "datalinq-api").FirstOrDefault();

        string dataLinqEndpointUrl = webResource!.Annotations.OfType<EndpointAnnotation>().Where(x => x.Name == "https").FirstOrDefault()!.AllocatedEndpoint!.UriString;

        var resourceNotificationService = appHost.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService.WaitForResourceAsync(
                "datalinq-api",
                KnownResourceStates.Running
            )
            .WaitAsync(TimeSpan.FromSeconds(30));

        _clientAuthorized = appHost.CreateHttpClient("datalinq-api");

        //Handle login and get bearer token
        var loginUrl = $"/DataLinqAuth?redirect=https://{_clientAuthorized.BaseAddress!.Authority}/DataLinqCode/Connect/0";
        var loginGetResponse = await _clientAuthorized.GetAsync(loginUrl);

        var requestVerificationToken = ExtractVerificationToken(await loginGetResponse.Content.ReadAsStringAsync());

        var loginFormData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken),
            new KeyValuePair<string, string>("Name", "datalinq"),
            new KeyValuePair<string, string>("Password", "datalinq"),
            new KeyValuePair<string, string>("Redirect", $"http://{_clientAuthorized.BaseAddress.Authority}/DataLinqCode/Connect/0")
        });

        var loginPostResponse = await _clientAuthorized.PostAsync(loginUrl, loginFormData);
    }

    #endregion


    //Test cases

    #region Get

    #region GetEndpointPrefix

    [TestMethod]
    public async Task GetEndPointPrefixes_ShouldReturnValidDictionary_WhenAuthorized()
    {
        await CreateEndpoint("integration-test-endpoint");

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/endpointprefixes";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var deserializedModel = JsonConvert.DeserializeObject<IDictionary<string, IEnumerable<string>>>(content);
        Assert.IsNotNull(deserializedModel);
        Assert.IsTrue(deserializedModel.Count > 0);

        await DeleteEndpoint("integration-test-endpoint");
    }

    #endregion

    #region GetEndpoint

    [TestMethod]
    public async Task GetEndPoint_ShouldReturnValidJson_WhenEndPointExists()
    {
        string endPointId = "integration-test-endpoint";

        await CreateEndpoint(endPointId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/get/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);
        Assert.AreEqual(response.Content.Headers.ContentType.MediaType, "application/json");

        var deserializedModel = JsonConvert.DeserializeObject<DataLinqEndPoint>(content);

        Assert.IsNotNull(deserializedModel, "Model should not be null");
        Assert.AreEqual(endPointId, deserializedModel.Id, "Property value mismatch");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task GetEndPoint_ShouldReturnNull_WhenEndPointDoesNotExist()
    {
        string endPointId = "non-existing-endpoint-id";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/get/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual("null", content);
    }

    [TestMethod]
    public async Task GetEndPoint_ShouldReturnError_WhenEndPointIsEmpty()
    {
        string endPointId = "";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/get/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode, "Expected 404 Not Found");
    }

    #endregion

    #region GetEndpointQuery

    [TestMethod]
    public async Task GetEndPointQuery_ShouldReturnValidJson_WhenQueryExists()
    {
        string endPointId = "integration-test-endpoint";
        string queryId = "integration-test-query";

        await CreateQuery(endPointId,queryId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/get/{endPointId}/{queryId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);
        Assert.AreEqual(response.Content.Headers.ContentType.MediaType, "application/json");

        var deserializedModel = JsonConvert.DeserializeObject<DataLinqEndPointQuery>(content);
        Assert.IsNotNull(deserializedModel, "Model should not be null");
        Assert.AreEqual(queryId, deserializedModel.QueryId, "Property value mismatch");

        await DeleteQuery(endPointId,queryId);
    }

    [TestMethod]
    public async Task GetEndPointQuery_ShouldReturnNull_WhenQueryDoesNotExist()
    {
        string endPointId = "allgemein-read";
        string queryId = "non-existing-query-id";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/get/{endPointId}/{queryId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("null", content);
    }

    #endregion

    #region GetEndpointQueryView

    [TestMethod]
    public async Task GetEndPointQueryView_ShouldReturnValidJson_WhenViewExists()
    {
        string endPointId = "integration-test-endpoint";
        string queryId = "integration-test-query";
        string viewId = "integration-test-view";

        await CreateView(endPointId, queryId, viewId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/get/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);
        Assert.AreEqual(response.Content.Headers.ContentType.MediaType, "application/json");

        var deserializedModel = JsonConvert.DeserializeObject<DataLinqEndPointQueryView>(content);
        Assert.IsNotNull(deserializedModel, "Model should not be null");
        Assert.AreEqual(viewId, deserializedModel.ViewId, "Property value mismatch");

        await DeleteView(endPointId, queryId, viewId);
    }

    [TestMethod]
    public async Task GetEndPointQueryView_ShouldReturnNull_WhenViewDoesNotExist()
    {
        string endPointId = "allgemein-read";
        string queryId = "infomobil";
        string viewId = "non-existing-view-id";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/get/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("null", content);
    }

    #endregion

    #region GetEndpointCss

    [TestMethod]
    public async Task GetEndPointCss_ShouldReturnCss_WhenEndPointExists()
    {
        string endPointId = "integration-test-endpoint";

        await CreateCssEndpoint(endPointId, "body {background-color: lightblue;}");

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/css/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);
        Assert.IsTrue(content.Contains("body"), "CSS content should contain body tag or style");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task GetEndPointCss_ShouldReturnEmptyString_WhenEndPointDoesNotExist()
    {
        string endPointId = "non-existing-endpoint-id";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/css/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("", content);
    }


    #endregion

    #region GetEndpointJs

    [TestMethod]
    public async Task GetEndPointJavascript_ShouldReturnJavascript_WhenEndPointExists()
    {
        string endPointId = "integration-test-endpoint";

        await CreateJsEndpoint(endPointId, "function myFunction(p1, p2) {return p1 * p2;}");

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/js/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);
        Assert.IsTrue(content.Contains("function"), "Javascript content should contain function definition");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task GetEndPointJavascript_ShouldReturnEmptyString_WhenEndPointDoesNotExist()
    {
        string endPointId = "non-existing-endpoint-id";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/js/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("", content);
    }


    #endregion

    #region GetEndpointTypes

    [TestMethod]
    public async Task GetEndPointTypes_ShouldReturnValidDictionary_WhenAuthorized()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/types/endpoint";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var deserializedModel = JsonConvert.DeserializeObject<IDictionary<int, string>>(content);
        Assert.IsNotNull(deserializedModel);
        Assert.IsTrue(deserializedModel.Count > 0);
    }

    [TestMethod]
    public async Task GetEndPointTypes_ShouldReturnError_WhenNotAuthorized()
    {
        var _clientUnauthorized = new HttpClient
        {
            BaseAddress = _clientAuthorized.BaseAddress
        };

        string requestUrl = $"{_clientUnauthorized.BaseAddress}datalinqcodeapi/types/endpoint";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientUnauthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }

    #endregion

    #region GetEndpoints

    [TestMethod]
    public async Task GetEndPoints_ShouldReturnValidList_WhenEndPointsExist()
    {
        for (int i = 0; i < 3; i++)
        {
            await CreateEndpoint("integration-test-endpoint-" + i.ToString());
        }

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/endpoints";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var endPoints = JsonConvert.DeserializeObject<List<string>>(content);

        Assert.IsNotNull(endPoints, "EndPoints should not be null");
        Assert.IsTrue(endPoints.Any(), "EndPoints list should not be empty");

        for (int i = 0; i < 3; i++)
        {
            await DeleteEndpoint("integration-test-endpoint-" + i.ToString());
        }
    }

    [TestMethod]
    public async Task GetEndPoints_ShouldReturnEmptyList_WhenNoEndPointsExist()
    {
        string filters = "non-existing-filter";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/endpoints?filters={filters}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();

        var endPoints = JsonConvert.DeserializeObject<List<string>>(content);

        Assert.IsNotNull(endPoints, "EndPoints should not be null");
        Assert.IsFalse(endPoints.Any(), "EndPoints list should be empty");
    }


    #endregion

    #region GetEndpointQueries

    [TestMethod]
    public async Task GetEndPointQueries_ShouldReturnValidList_WhenQueriesExist()
    {
        string endPointId = "integration-test-endpoint";

        await CreateEndpoint(endPointId);
        for (int i = 0; i < 3; i++)
        {
            await CreateQuery(endPointId, "integration-test-query-" + i.ToString());
        }

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/{endPointId}/queries";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var queries = JsonConvert.DeserializeObject<List<string>>(content);

        Assert.IsNotNull(queries, "Queries should not be null");
        Assert.IsTrue(queries.Any(), "Queries list should not be empty");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task GetEndPointQueries_ShouldReturnEmptyList_WhenNoQueriesExist()
    {
        string endPointId = "non-existing-endpoint";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/{endPointId}/queries";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();

        var queries = JsonConvert.DeserializeObject<List<string>>(content);

        Assert.IsNotNull(queries, "Queries should not be null");
        Assert.IsFalse(queries.Any(), "Queries list should be empty");
    }

    #endregion

    #region GetEndpointQueryViews

    [TestMethod]
    public async Task GetEndPointQueryViews_ShouldReturnValidList_WhenQueryViewsExist()
    {
        string endPointId = "integration-test-endpoint";
        string queryId = "integration-test-query";

        await CreateQuery(endPointId, queryId);
        for (int i = 0; i < 3; i++)
        {
            await CreateView(endPointId, queryId, "integration-test-view-" + i.ToString());
        }

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/{endPointId}/{queryId}/views";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var queryViews = JsonConvert.DeserializeObject<List<string>>(content);

        Assert.IsNotNull(queryViews, "QueryViews should not be null");
        Assert.IsTrue(queryViews.Any(), "QueryViews list should not be empty");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task GetEndPointQueryViews_ShouldReturnEmptyList_WhenNoQueryViewsExist()
    {
        string endPointId = "allgemein-read";
        string queryId = "non-existing-query";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/{endPointId}/{queryId}/views";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        string content = await response.Content.ReadAsStringAsync();

        var queryViews = JsonConvert.DeserializeObject<List<string>>(content);

        Assert.IsNotNull(queryViews, "QueryViews should not be null");
        Assert.IsFalse(queryViews.Any(), "QueryViews list should be empty");
    }

    #endregion

    #endregion

    #region Edit

    #region StoreEndpoint

    [TestMethod]
    public async Task StoreEndPoint_ShouldReturnSuccess_WhenValidEndPointIsProvided()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpoint";

        var endPoint = new DataLinqEndPoint { Id = "integration-test-endpoint", Name = "IntegrationTest", Description = "IntegrationTest for storing an Endpoint", Created = DateTime.Now, Access = new string[] { "client2" }, SubscriberId = "2", Subscriber = "client2", TypeValue = 0 };
        var requestContent = new StringContent(JsonConvert.SerializeObject(endPoint), Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = requestContent };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPoint.Id);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string endpointFilePath = Path.Combine(endpointFolderPath, $"{endPoint.Id}.blb");
        Assert.IsTrue(File.Exists(endpointFilePath), $"Endpoint file '{endpointFilePath}' does not exist");

        var savedEndpoint = JsonConvert.DeserializeObject<DataLinqEndPoint>(await File.ReadAllTextAsync(endpointFilePath));
        Assert.IsNotNull(savedEndpoint, "Saved endpoint should not be null");
        Assert.AreEqual(JsonConvert.SerializeObject(endPoint), JsonConvert.SerializeObject(savedEndpoint), "Saved endpoint data does not match the sent data");

        await DeleteEndpoint("integration-test-endpoint");
    }

    [TestMethod]
    public async Task StoreEndPoint_ShouldReturnFailure_WhenInvalidEndPointIsProvided()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpoint";

        var endPoint = new DataLinqEndPoint { Id = "", Name = "" };
        var requestContent = new StringContent(JsonConvert.SerializeObject(endPoint), Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = requestContent };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsFalse(successModel.Success, "Operation should fail");
    }


    #endregion

    #region StoreEndpointCss

    [TestMethod]
    public async Task StoreEndPointCss_ShouldReturnSuccess_WhenValidDataIsProvided()
    {
        await CreateEndpoint("integration-test-endpoint");

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointcss";

        var formData = new MultipartFormDataContent
        {
            { new StringContent("integration-test-endpoint"), "endPointId" },
            { new StringContent("body { background-color: #fff; }"), "css" }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = formData };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, "integration-test-endpoint");
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string cssFilePath = Path.Combine(endpointFolderPath, "_css.blb");
        Assert.IsTrue(File.Exists(cssFilePath), $"CSS file '{cssFilePath}' does not exist");

        string fileContent = await File.ReadAllTextAsync(cssFilePath);
        Assert.AreEqual("body { background-color: #fff; }", fileContent, "Saved CSS content does not match the sent data");

        await DeleteEndpoint("integration-test-endpoint");
    }

    [TestMethod]
    public async Task StoreEndPointCss_ShouldReturnFailure_WhenEndPointIdIsMissing()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointcss";

        var formData = new MultipartFormDataContent
        {
            { new StringContent("body { background-color: #fff; }"), "css" }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = formData };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsFalse(successModel.Success, "Operation should fail");
    }

    [TestMethod]
    public async Task StoreEndPointCss_ShouldReturnFailure_WhenEndPointDoesNotExist()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointcss";

        var formData = new MultipartFormDataContent
        {
            { new StringContent("non-existing-endpoint"), "endPointId" },
            { new StringContent("body { background-color: #fff; }"), "css" }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = formData };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsFalse(successModel.Success, "Operation should fail");
    }


    #endregion

    #region StoreEndpointJs

    [TestMethod]
    public async Task StoreEndPointJavascript_ShouldReturnSuccess_WhenValidDataIsProvided()
    {
        await CreateEndpoint("integration-test-endpoint");

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointjs";

        var formData = new MultipartFormDataContent
        {
            { new StringContent("integration-test-endpoint"), "endPointId" },
            { new StringContent("console.log('Hello World');"), "js" }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = formData };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, "integration-test-endpoint");
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string jsFilePath = Path.Combine(endpointFolderPath, "_js.blb");
        Assert.IsTrue(File.Exists(jsFilePath), $"JS file '{jsFilePath}' does not exist");

        string fileContent = await File.ReadAllTextAsync(jsFilePath);
        Assert.AreEqual("console.log('Hello World');", fileContent, "Saved JS content does not match the sent data");

        await DeleteEndpoint("integration-test-endpoint");
    }

    [TestMethod]
    public async Task StoreEndPointJavascript_ShouldReturnFailure_WhenEndPointIdIsMissing()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointjs";

        var formData = new MultipartFormDataContent
        {
            { new StringContent("console.log('Hello World');"), "js" }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = formData };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsFalse(successModel.Success, "Operation should fail");
    }

    [TestMethod]
    public async Task StoreEndPointJavascript_ShouldReturnFailure_WhenEndPointDoesNotExist()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointjs";

        var formData = new MultipartFormDataContent
        {
            { new StringContent("non-existing-endpoint"), "endPointId" },
            { new StringContent("console.log('Hello World');"), "js" }
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = formData };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsFalse(successModel.Success, "Operation should fail");
    }


    #endregion

    #region StoreEndpointQuery

    [TestMethod]
    public async Task StoreEndPointQuery_ShouldReturnSuccess_WhenValidDataIsProvided()
    {
        await CreateEndpoint("integration-test-endpoint");

        string endPointId = "integration-test-endpoint";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/{endPointId}/query";

        var query = new DataLinqEndPointQuery { QueryId = "integration-query", Name = "IntegrationQuery", Access = new string[] { "subscriber::author" }, Created = DateTime.Now, Statement = "" };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json")
        };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string queriesFolderPath = Path.Combine(endpointFolderPath, "queries");
        Assert.IsTrue(Directory.Exists(queriesFolderPath), $"Queries folder '{queriesFolderPath}' does not exist");

        string queryFilePath = Path.Combine(queriesFolderPath, $"{query.QueryId}.blb");
        Assert.IsTrue(File.Exists(queryFilePath), $"Query file '{queryFilePath}' does not exist");

        var savedQuery = JsonConvert.DeserializeObject<DataLinqEndPointQuery>(await File.ReadAllTextAsync(queryFilePath));
        savedQuery.Statement = "";
        Assert.IsNotNull(savedQuery, "Saved query should not be null");
        Assert.AreEqual(JsonConvert.SerializeObject(query), JsonConvert.SerializeObject(savedQuery), "Saved query content does not match the sent data");

        await DeleteEndpoint("integration-test-endpoint");
    }

    [TestMethod]
    public async Task StoreEndPointQuery_ShouldReturnFailure_WhenEndPointIdIsMissing()
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/{""}/query";

        var query = new DataLinqEndPointQuery { QueryId = "integration-query", Name = "IntegrationQuery", Access = new string[] { "subscriber::author" }, Created = DateTime.Now, Statement = "" };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json")
        };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsFalse(response.IsSuccessStatusCode, $"Request unexpectedly succeeded with status code {response.StatusCode}");
    }

    [TestMethod]
    public async Task StoreEndPointQuery_ShouldReturnFailure_WhenRequestBodyIsMissing()
    {
        string endPointId = "existing-endpoint";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/{endPointId}/query";

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsFalse(successModel.Success, "Operation should fail");
    }


    #endregion

    #region StoreEndpointQueryView

    [TestMethod]
    public async Task StoreEndPointQueryView_ShouldReturnSuccess_WhenValidDataIsProvided()
    {
        string endPointId = "integration-test-endpoint";
        string queryId = "integration-test-query";

        await CreateQuery(endPointId, queryId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/{endPointId}/{queryId}/view";

        var view = new DataLinqEndPointQueryView { ViewId = "integration-view1", Name = "IntegrationView1", Description = "View for integration test.", Code = "", Created = DateTime.Now, IncludedJsLibraries = "legacy_chartjs", TestParameters = null };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(view), Encoding.UTF8, "application/json")
        };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string viewFolderPath = Path.Combine(endpointFolderPath, "queries", $"{queryId}-views");
        Assert.IsTrue(Directory.Exists(viewFolderPath), $"Query folder '{viewFolderPath}' does not exist");

        string viewFilePath = Path.Combine(viewFolderPath, $"{view.ViewId}.blb");
        Assert.IsTrue(File.Exists(viewFilePath), $"View file '{viewFilePath}' does not exist");

        var savedView = JsonConvert.DeserializeObject<DataLinqEndPointQueryView>(await File.ReadAllTextAsync(viewFilePath));
        savedView.Changed = DateTime.Parse("01.01.0001 00:00:00");
        Assert.IsNotNull(savedView, "Saved view should not be null");
        Assert.AreEqual(JsonConvert.SerializeObject(view), JsonConvert.SerializeObject(savedView), "Saved view content does not match the sent data");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task StoreEndPointQueryView_ShouldReturnSuccess_WhenVerifyOnlyIsTrue()
    {
        string endPointId = "integration-test";
        string queryId = "integration-query";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/{endPointId}/{queryId}/view?verifyOnly=true";

        var view = new DataLinqEndPointQueryView { ViewId = "integration-view1", Name = "IntegrationView1", Description = "View for integration test.", Code = "", Created = DateTime.Now, IncludedJsLibraries = "legacy_chartjs", TestParameters = null };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(view), Encoding.UTF8, "application/json")
        };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");
    }

    [TestMethod]
    public async Task StoreEndPointQueryView_ShouldReturnFailure_WhenEndPointIdIsMissing()
    {
        string queryId = "integration-query";

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post//{queryId}/view";

        var view = new DataLinqEndPointQueryView { ViewId = "integration-view", Name = "IntegrationView", Description = "View for integration test.", Code = "", Created = DateTime.Now, IncludedJsLibraries = "legacy_chartjs", TestParameters = null };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(view), Encoding.UTF8, "application/json")
        };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsFalse(response.IsSuccessStatusCode, $"Request unexpectedly succeeded with status code {response.StatusCode}");
    }

    [TestMethod]
    public async Task StoreEndPointQueryView_ShouldReturnFailure_WhenQueryIdIsMissing()
    {
        string endPointId = "existing-endpoint";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/{endPointId}//view";

        var view = new DataLinqEndPointQueryView { ViewId = "integration-view", Name = "IntegrationView", Description = "View for integration test.", Code = "", Created = DateTime.Now, IncludedJsLibraries = "legacy_chartjs", TestParameters = null };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonConvert.SerializeObject(view), Encoding.UTF8, "application/json")
        };

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsFalse(response.IsSuccessStatusCode, $"Request unexpectedly succeeded with status code {response.StatusCode}");
    }

    #endregion

    #endregion

    #region Create

    #region CreateEndpoint

    [TestMethod]
    public async Task CreateEndPoint_ShouldReturnSuccess_WhenEndPointDoesNotExist()
    {
        string endPointId = "integration-test-endpoint";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessCreatedModel>(content);

        Assert.IsNotNull(successModel, "SuccessCreatedModel should not be null");
        Assert.AreEqual(endPointId, successModel.EndPointId, "EndPointId mismatch");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string endpointFilePath = Path.Combine(endpointFolderPath, $"{endPointId}.blb");
        Assert.IsTrue(File.Exists(endpointFilePath), $"Endpoint file '{endpointFilePath}' does not exist");
        Assert.IsInstanceOfType(JsonConvert.DeserializeObject<DataLinqEndPoint>(await File.ReadAllTextAsync(endpointFilePath)), typeof(DataLinqEndPoint), "JSON string is not of the expected object type");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task CreateEndPoint_ShouldReturnFailure_WhenEndPointAlreadyExists()
    {
        string endPointId = "integration-test-endpoint";

        await CreateEndpoint(endPointId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Endpoint {endPointId} alread exists"), "Expected error message not found");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task CreateEndPoint_ShouldReturnFailure_WhenEndPointIdIsInvalid()
    {
        string endPointId = "a_!";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains("Invalid endpoint id"), "Expected error message not found");
    }

    #endregion

    #region CreateEndpointQuery

    [TestMethod]
    public async Task CreateEndPointQuery_ShouldReturnSuccess_WhenQueryDoNotExist()
    {
        string endPointId = "integration-test-endpoint";
        string queryId = "integration-test-query";

        await CreateEndpoint(endPointId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}/{queryId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessCreatedModel>(content);

        Assert.IsNotNull(successModel, "SuccessCreatedModel should not be null");
        Assert.AreEqual(endPointId, successModel.EndPointId, "EndPointId mismatch");
        Assert.AreEqual(queryId, successModel.QueryId, "QueryId mismatch");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string queriesFolderPath = Path.Combine(endpointFolderPath, "queries");
        Assert.IsTrue(Directory.Exists(queriesFolderPath), $"Queries folder '{queriesFolderPath}' does not exist");

        string queryFilePath = Path.Combine(queriesFolderPath, $"{queryId}.blb");
        Assert.IsTrue(File.Exists(queryFilePath), $"Query file '{queryFilePath}' does not exist");

        Assert.IsTrue(File.Exists(queryFilePath), $"Endpoint file '{queryFilePath}' does not exist");
        Assert.IsInstanceOfType(JsonConvert.DeserializeObject<DataLinqEndPointQuery>(await File.ReadAllTextAsync(queryFilePath)), typeof(DataLinqEndPointQuery), "JSON string is not of the expected object type");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task CreateEndPointQuery_ShouldReturnFailure_WhenEndPointDoesNotExist()
    {
        string endPointId = "non-existing-endpoint";
        string queryId = "new-query";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}/{queryId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Endpoint {endPointId} not exists"), "Expected error message not found");
    }

    [TestMethod]
    public async Task CreateEndPointQuery_ShouldReturnFailure_WhenQueryAlreadyExists()
    {
        string endPointId = "integration-test-endpoint";
        string queryId = "integration-test-query";

        await CreateQuery(endPointId, queryId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}/{queryId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Query {endPointId}@{queryId} allready exists"), "Expected error message not found");

        await DeleteEndpoint(endPointId);
    }


    #endregion

    #region CreateEndpointQueryView

    [TestMethod]
    public async Task CreateEndPointQueryView_ShouldReturnSuccess_WhenViewDoNotExist()
    {
        string endPointId = "create-integration-test";
        string queryId = "new-query-integration";
        string viewId = "new-view-integration";

        await CreateQuery(endPointId, queryId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessCreatedModel>(content);

        Assert.IsNotNull(successModel, "SuccessCreatedModel should not be null");
        Assert.AreEqual(endPointId, successModel.EndPointId, "EndPointId mismatch");
        Assert.AreEqual(queryId, successModel.QueryId, "QueryId mismatch");
        Assert.AreEqual(viewId, successModel.ViewId, "ViewId mismatch");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string viewFolderPath = Path.Combine(endpointFolderPath, "queries", $"{queryId}-views");
        Assert.IsTrue(Directory.Exists(viewFolderPath), $"Query folder '{viewFolderPath}' does not exist");

        string viewFilePath = Path.Combine(viewFolderPath, $"{viewId}.blb");
        Assert.IsTrue(File.Exists(viewFilePath), $"View file '{viewFilePath}' does not exist");

        Assert.IsTrue(File.Exists(viewFilePath), $"Endpoint file '{viewFilePath}' does not exist");
        Assert.IsInstanceOfType(JsonConvert.DeserializeObject<DataLinqEndPointQueryView>(await File.ReadAllTextAsync(viewFilePath)), typeof(DataLinqEndPointQueryView), "JSON string is not of the expected object type");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task CreateEndPointQueryView_ShouldReturnFailure_WhenEndPointDoesNotExist()
    {
        string endPointId = "non-existing-endpoint";
        string queryId = "new-query-integration";
        string viewId = "new-view-integration";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Endpoint {endPointId} not exists"), "Expected error message not found");
    }

    [TestMethod]
    public async Task CreateEndPointQueryView_ShouldReturnFailure_WhenQueryDoesNotExist()
    {
        string endPointId = "create-integration-test";
        string queryId = "non-existing-query";
        string viewId = "new-view-integration";

        await CreateEndpoint(endPointId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Query {endPointId}@{queryId} not exists"), "Expected error message not found");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task CreateEndPointQueryView_ShouldReturnFailure_WhenViewAlreadyExists()
    {
        string endPointId = "create-integration-test";
        string queryId = "new-query-integration";
        string viewId = "new-view-integration";

        await CreateView(endPointId, queryId, viewId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"View {endPointId}@{queryId}@{viewId} allready exists"), "Expected error message not found");

        await DeleteEndpoint(endPointId);
    }


    #endregion

    #endregion

    #region Delete

    #region DeleteEndpoint

    [TestMethod]
    public async Task DeleteEndPoint_ShouldReturnSuccess_WhenEndPointExists()
    {
        string endPointId = "integration-test";

        await CreateEndpoint(endPointId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsFalse(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string endpointFilePath = Path.Combine(endpointFolderPath, $"{endPointId}.blb");
        Assert.IsFalse(File.Exists(endpointFilePath), $"Endpoint file '{endpointFilePath}' does not exist");
    }

    [TestMethod]
    public async Task DeleteEndPoint_ShouldReturnFailure_WhenEndPointDoesNotExist()
    {
        string endPointId = "non-existing-endpoint";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endPointId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Endpoint {endPointId} not exists"), "Expected error message not found");
    }


    #endregion

    #region DeleteEndpointQuery

    [TestMethod]
    public async Task DeleteEndPointQuery_ShouldReturnSuccess_WhenEndPointQueryExists()
    {
        string endPointId = "integration-test";
        string queryId = "integration-query";

        await CreateQuery(endPointId, queryId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endPointId}/{queryId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string queriesFolderPath = Path.Combine(endpointFolderPath, "queries");
        Assert.IsTrue(Directory.Exists(queriesFolderPath), $"Queries folder '{queriesFolderPath}' does not exist");

        string queryFilePath = Path.Combine(queriesFolderPath, $"{queryId}.blb");
        Assert.IsFalse(File.Exists(queryFilePath), $"Query file '{queryFilePath}' does not exist");
    }

    [TestMethod]
    public async Task DeleteEndPointQuery_ShouldReturnFailure_WhenEndPointQueryDoesNotExist()
    {
        string endPointId = "integration-test";
        string queryId = "non-existing-query";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endPointId}/{queryId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Query {endPointId}@{queryId} not exists"), "Expected error message not found");
    }


    #endregion

    #region DeleteEndpointQueryView

    [TestMethod]
    public async Task DeleteEndPointQueryView_ShouldReturnSuccess_WhenEndPointQueryViewExists()
    {
        string endPointId = "integration-test";
        string queryId = "integration-query";
        string viewId = "integration-view1";

        await CreateView(endPointId, queryId, viewId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        string endpointFolderPath = Path.Combine(ENDPOINTS_STORAGE_PATH, endPointId);
        Assert.IsTrue(Directory.Exists(endpointFolderPath), $"Endpoint folder '{endpointFolderPath}' does not exist");

        string viewFolderPath = Path.Combine(endpointFolderPath, "queries", $"{queryId}-views");
        Assert.IsTrue(Directory.Exists(viewFolderPath), $"Query folder '{viewFolderPath}' does not exist");

        string viewFilePath = Path.Combine(viewFolderPath, $"{viewId}.blb");
        Assert.IsFalse(File.Exists(viewFilePath), $"View file '{viewFilePath}' does not exist");
    }

    [TestMethod]
    public async Task DeleteEndPointQueryView_ShouldReturnFailure_WhenEndPointQueryViewDoesNotExist()
    {
        string endPointId = "integration-test";
        string queryId = "integration-query";
        string viewId = "non-existing-view";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"View {endPointId}@{queryId}@{viewId} not exists"), "Expected error message not found");
    }


    #endregion

    #endregion

    #region Verify

    #region VerifyEndPointQueryView

    [TestMethod]
    public async Task VerifyEndPointQueryView_ShouldReturnSuccess_WhenViewExistsAndIsValid()
    {
        string endPointId = "integration-test";
        string queryId = "integration-query";
        string viewId = "integration-view1";

        await CreateView(endPointId, queryId, viewId);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/verify/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        var successModel = JsonConvert.DeserializeObject<SuccessModel>(content);

        Assert.IsNotNull(successModel, "SuccessModel should not be null");
        Assert.IsTrue(successModel.Success, "Operation should be successful");

        await DeleteEndpoint(endPointId);
    }

    [TestMethod]
    public async Task VerifyEndPointQueryView_ShouldReturnError_WhenViewDoesNotExist()
    {
        string endPointId = "integration-test";
        string queryId = "integration-query";
        string viewId = "non-existing-view";
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/verify/{endPointId}/{queryId}/{viewId}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Request failed with status code {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        Assert.IsNotNull(content);

        Assert.IsTrue(content.Contains($"Unknown view {endPointId}@{queryId}@{viewId}"), "Expected error message not found");
    }


    #endregion

    #endregion

    #region Helper
    static string ExtractVerificationToken(string html)
    {
        var match = Regex.Match(html, @"<input\s+[^>]*name=""__RequestVerificationToken""\s+[^>]*value=""([^""]+)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    public async Task CreateEndpoint(string name)
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{name}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);
    }

    public async Task DeleteEndpoint(string name)
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{name}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);
    }

    public async Task CreateQuery(string endpoint, string query)
    {
        await CreateEndpoint(endpoint);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endpoint}/{query}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);
    }

    public async Task DeleteQuery(string endpoint, string query)
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endpoint}/{query}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        await DeleteEndpoint(endpoint);
    }

    public async Task CreateView(string endpoint, string query, string view)
    {
        await CreateQuery(endpoint, query);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/create/{endpoint}/{query}/{view}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);
    }

    public async Task DeleteView(string endpoint, string query, string view)
    {
        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/delete/{endpoint}/{query}/{view}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var response = await _clientAuthorized.SendAsync(requestMessage);

        await DeleteQuery(endpoint,query);
    }

    public async Task CreateCssEndpoint(string endpoint, string css)
    {
        await CreateEndpoint(endpoint);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointcss";

        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("endPointId", endpoint),
            new KeyValuePair<string, string>("css", css)
        });

        var response = await _clientAuthorized.PostAsync(requestUrl, formData);
    }

    public async Task CreateJsEndpoint(string endpoint, string js)
    {
        await CreateEndpoint(endpoint);

        string requestUrl = $"{_clientAuthorized.BaseAddress}datalinqcodeapi/post/endpointjs";

        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("endPointId", endpoint),
            new KeyValuePair<string, string>("js", js)
        });

        var response = await _clientAuthorized.PostAsync(requestUrl, formData);
    }

    #endregion
}
