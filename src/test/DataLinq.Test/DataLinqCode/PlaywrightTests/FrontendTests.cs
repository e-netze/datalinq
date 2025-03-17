using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using E.DataLinq.Test.DataLinqCode.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using System.Text.RegularExpressions;

namespace E.DataLinq.Test.DataLinqCode.PlaywrightTests;

[TestClass]
public class FrontendTests : PageTest
{
    private string DataLinqEndpointUrl;
    private DistributedApplication AppHost;

    [TestInitialize]
    public async Task InitializeAspire()
    {
        //Initialization of Aspire
        var appHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DataLinq_AppHost>();
        AppHost = await appHostBuilder.BuildAsync();
        await AppHost.StartAsync();

        var webResource = appHostBuilder.Resources.Where(r => r.Name == "datalinq").FirstOrDefault();

        DataLinqEndpointUrl = webResource!.Annotations.OfType<EndpointAnnotation>().Where(x => x.Name == "https").FirstOrDefault()!.AllocatedEndpoint!.UriString;

        var resourceNotificationService = AppHost.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService.WaitForResourceAsync(
                "datalinq",
                KnownResourceStates.Running
            )
            .WaitAsync(TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    public async Task Test_IsStatusSuccess_ShouldReturnTrue()
    {
        using var httpClient = new HttpClient();
        bool isSuccess = false;

        for (int i = 0; i < 60; i++)
        {
            if ((await httpClient.GetAsync(DataLinqEndpointUrl)).IsSuccessStatusCode)
            {
                isSuccess = true;
                break;
            }
            await Task.Delay(500);
        }

        Assert.IsTrue(isSuccess, $"Endpoint {DataLinqEndpointUrl} did not return success.");
    }

    [TestMethod]
    public async Task Test_Login_WithValidCredentials_ShouldSucceed()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.Locator(".datalinq-code-modal-close").ClickAsync();

        await Expect(page.Locator("iframe").First.ContentFrame.GetByRole(AriaRole.Heading, new() { Name = "Welcome to DataLinq.Code" })).ToBeVisibleAsync();

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_Login_WithInvalidCredentials_ShouldFail()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.GetByText("Local A local datalinq").ClickAsync();

        await page.GetByPlaceholder("Enter username...").ClickAsync();

        await page.GetByPlaceholder("Enter username...").FillAsync("something_wrong");

        await page.GetByPlaceholder("Enter password...").ClickAsync();

        await page.GetByPlaceholder("Enter password...").FillAsync("something_wrong");

        await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        await Expect(page.GetByText("Invalid user or password")).ToBeVisibleAsync();

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_ModalOpen_WhenSelected_ShouldDisplaySelected()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        var selectedItems = await page.ModalSelectFirst3();

        await page.WaitForSelectorAsync(".tree-node .label");

        foreach (var selectedItem in selectedItems!)
        {
            await Expect(page.GetByRole(AriaRole.List)).ToMatchAriaSnapshotAsync($"- listitem: {selectedItem}");
        }

        await browserContext.CloseAsync();
    }


    [TestMethod]
    public async Task Test_ModalOpen_WhenAllSelected_ShouldDisplayAll()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        var combinedItems = await page.ModalSelectAll();

        await page.WaitForSelectorAsync(".tree-node .label");

        var labelLocator = page.Locator(".tree-node .label");

        var labels = new List<string>();

        for (int i = 0; i < await labelLocator.CountAsync(); i++)
        {
            var label = labelLocator.Nth(i);
            var labelText = await label.TextContentAsync();
            labels.Add(labelText!.Trim());
        }

        Assert.IsTrue(combinedItems!.OrderBy(x => x).SequenceEqual(labels.OrderBy(x => x)), "The lists are not the same.");

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_Verify_SelectedViews_ShouldSucceed()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.ModalSelectFirst3(returnResults: false);

        await page.WaitForJQueryToFinish();

        await page.Locator("iframe").First.ContentFrame.GetByText("Verify All Views Compile all").ClickAsync();

        await Expect(page.Locator("iframe").First.ContentFrame.Locator("#compiler-progress div").First).ToBeVisibleAsync();

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_TryLoad_AllDocuments_ShouldSucceed()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.ModalSelectFirst3(returnResults: false);

        await page.WaitForJQueryToFinish();

        await page.Locator("iframe").First.ContentFrame.Locator("div").Filter(new() { HasText = "Try Load All Documents" }).Nth(1).ClickAsync();

        await Expect(page.Locator("iframe").First.ContentFrame.Locator("#load-progress div").First).ToBeVisibleAsync();

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_ChangeTheme_ShouldApplyNewTheme()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.Locator(".datalinq-code-modal-close").ClickAsync();

        bool hasClassBefore = await page.Locator("div.datalinq-code-ide.colorscheme-light").IsVisibleAsync();

        Assert.IsFalse(hasClassBefore, "The element should not contain the 'colorscheme-light' class.");

        await page.GetByText("Color scheme").ClickAsync();

        bool hasClassAfter = await page.Locator("div.datalinq-code-ide.colorscheme-light").IsVisibleAsync();

        Assert.IsTrue(hasClassAfter, "The element should contain the 'colorscheme-light' class.");

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_Logout_ShouldTerminateSession()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.Locator(".datalinq-code-modal-close").ClickAsync();

        await page.GetByText("client2Logout...").ClickAsync();

        await Expect(page.GetByText("Local A local datalinq")).ToBeVisibleAsync();

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_CreateNewAndDelete_ShouldRemoveSuccessfully()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.Locator(".datalinq-code-modal-close").ClickAsync();

        await Expect(page.GetByPlaceholder("New endpoint...")).ToBeVisibleAsync();

        await page.GetByPlaceholder("New endpoint...").ClickAsync();

        await page.GetByPlaceholder("New endpoint...").FillAsync("frontend-test-endpoint");

        await page.GetByRole(AriaRole.Listitem).ClickAsync();

        await page.GetByPlaceholder("New endpoint...").ClickAsync();

        await page.GetByPlaceholder("New endpoint...").PressAsync("Enter");

        await Expect(page.GetByText("frontend-test-endpoint")).ToBeVisibleAsync();

        var treeContainer = page.Locator(".datalinq-code-tree-holder");

        await treeContainer.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var testtItem = treeContainer.Locator("li.tree-node.endpoint", new() { HasText = "frontend-test-endpoint" });

        await Expect(testtItem).ToBeVisibleAsync();

        await page.EvaluateAsync(@"(element) => {
                                    element.classList.add('collapsed');
                                }", await testtItem.ElementHandleAsync());

        var hasCollapsedClass = await testtItem.EvaluateAsync<bool>(@"element => element.classList.contains('collapsed')");

        await Expect(page.Locator("li").Filter(new() { HasText = "frontend-test-endpointCopy" }).GetByRole(AriaRole.Listitem)).ToBeVisibleAsync();

        await page.GetByPlaceholder("New query/data...").ClickAsync();

        await page.GetByPlaceholder("New query/data...").FillAsync("frontend-test-datasource");

        await page.GetByPlaceholder("New query/data...").PressAsync("Enter");

        await Expect(page.GetByText("frontend-test-datasourceCopy")).ToBeVisibleAsync();

        await treeContainer.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var listItem = treeContainer.Locator("li.tree-node.query", new() { HasText = "frontend-test-datasource" });

        await Expect(listItem).ToBeVisibleAsync();

        await page.EvaluateAsync(@"(element) => {
                                    element.classList.add('collapsed');
                                }", await listItem.ElementHandleAsync());

        await Expect(page.Locator("li").Filter(new() { HasTextRegex = new Regex("^frontend-test-datasourceCopy placeholder$") }).GetByRole(AriaRole.Listitem)).ToBeVisibleAsync();

        await page.GetByPlaceholder("New view...").ClickAsync();

        await page.GetByPlaceholder("New view...").FillAsync("frontend-test-view");

        await page.GetByPlaceholder("New view...").PressAsync("Enter");

        await Expect(page.GetByText("frontend-test-viewCopy")).ToBeVisibleAsync();

        await page.GetByText("frontend-test-endpoint").ClickAsync();

        await page.Locator("iframe").Nth(1).ContentFrame.GetByLabel("Name").ClickAsync();

        await page.Locator("iframe").Nth(1).ContentFrame.GetByLabel("Name").FillAsync("name-frontend-test-endpoint");

        await page.Locator("iframe").Nth(1).ContentFrame.GetByLabel("Description").ClickAsync();

        await page.Locator("iframe").Nth(1).ContentFrame.GetByLabel("Description").FillAsync("description");

        await page.Locator("iframe").Nth(1).ContentFrame.GetByLabel("EndPoint Connection Type").SelectOptionAsync(new[] { "5" });

        await page.GetByText("frontend-test-datasource").ClickAsync();

        await page.Locator("iframe").Nth(2).ContentFrame.Locator(".view-line").ClickAsync();

        await page.Locator("iframe").Nth(2).ContentFrame.GetByLabel("Editor content;Press Alt+F1").FillAsync("one:hello\ntwo:world\n");

        await page.GetByText("frontend-test-view").ClickAsync();

        await page.Locator("iframe").Nth(3).ContentFrame.Locator(".view-lines > div:nth-child(27)").ClickAsync();

        await page.Locator("iframe").Nth(3).ContentFrame.GetByLabel("Editor content;Press Alt+F1").FillAsync("DataLinqHelper (DLH)\n====================\n\nThe DataLinqHelper is a helper class that provides methods for displaying data as well as for forms, etc.\nFor more information, see Help (?).\n*@\n\n\n<h1>frontend-test</h1>\n  \n@DLH.Table(Model.Records, max: 100)\n");

        await page.Locator("div").Filter(new() { HasText = "Check syntax" }).Nth(3).ClickAsync();

        await page.Locator("div").Filter(new() { HasText = "Save all Docs" }).Nth(3).ClickAsync();

        await Task.Delay(3000);

        await page.Locator("div").Filter(new() { HasText = "Simple Preview" }).Nth(3).ClickAsync();

        await Expect(page.Locator(".datalinq-code-blockframe-content > iframe").ContentFrame.GetByRole(AriaRole.Heading, new() { Name = "frontend-test" })).ToBeVisibleAsync();

        await page.Locator(".datalinq-code-blockframe-close").ClickAsync();

        await page.GetByTitle("frontend-test-endpoint", new() { Exact = true }).ClickAsync();

        await Expect(page.Locator("iframe").Nth(1).ContentFrame.GetByRole(AriaRole.Button, new() { Name = "Delete" })).ToBeVisibleAsync();

        await page.Locator("iframe").Nth(1).ContentFrame.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Yes" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Yes" }).ClickAsync();

        await Expect(page.Locator("iframe").First.ContentFrame.GetByRole(AriaRole.Heading, new() { Name = "Welcome to DataLinq.Code" })).ToBeVisibleAsync();

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_ReopenClosedModalAndSelect_DisplayedItemsShouldMatchSelected()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.Locator(".datalinq-code-modal-close").ClickAsync();

        await page.GetByText("DataLinq.Code").ClickAsync();

        var selectedItems = await page.ModalSelectFirst3();

        await page.WaitForSelectorAsync(".tree-node .label");

        foreach (var selectedItem in selectedItems!)
        {
            await Expect(page.GetByRole(AriaRole.List)).ToMatchAriaSnapshotAsync($"- listitem: {selectedItem}");
        }

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_SelectAllAndSearchForEndpoints_ShouldMatchSearch()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        var selectedItems = await page.ModalSelectAll();

        await page.WaitForSelectorAsync(".tree-node .label");

        int numberOfRandomEndpoints = selectedItems!.Count() > 5 ? 5 : selectedItems.Count();

        var itemsSlectedAtRandom = selectedItems!.OrderBy(_ => new Random().Next()).Take(numberOfRandomEndpoints).ToList();

        foreach (var item in itemsSlectedAtRandom)
        {
            await page.GetByPlaceholder("Find Endpoint, Query, View...").ClickAsync();

            await page.GetByPlaceholder("Find Endpoint, Query, View...").FillAsync(item);

            await Expect(page.GetByText(item, new() { Exact = true }).First).ToBeVisibleAsync();

            await page.GetByPlaceholder("Find Endpoint, Query, View...").ClickAsync();

            await page.GetByPlaceholder("Find Endpoint, Query, View...").FillAsync("");
        }

        await browserContext.CloseAsync();
    }

    [TestMethod]
    public async Task Test_DataLinqHelper_ShouldOpenAndDisplayHelp()
    {
        var (page, browserContext) = await GetBrowserInstance();

        await page.GotoAsync(DataLinqEndpointUrl);

        await page.LogIntoDataLinq();

        await page.Locator(".datalinq-code-modal-close").ClickAsync();

        await page.Locator("div").Filter(new() { HasText = "Datalinq Helper" }).Nth(3).ClickAsync();

        await Expect(page.Locator("#help-frame").ContentFrame.GetByRole(AriaRole.Heading, new() { Name = "DataLinq", Exact = true })).ToBeVisibleAsync();

        await page.Locator("#help-frame").ContentFrame.GetByRole(AriaRole.Link, new() { Name = "GetCurrentUsername" }).ClickAsync();

        await Expect(page.Locator("#help-frame").ContentFrame.Locator("pre").Filter(new() { HasText = "@DLH.GetCurrentUsername()Copy" })).ToBeVisibleAsync();

        await page.Locator("#help-frame").ContentFrame.GetByRole(AriaRole.Link, new() { Name = "<< All Methods" }).ClickAsync();

        await Expect(page.Locator("#help-frame").ContentFrame.GetByRole(AriaRole.Heading, new() { Name = "DataLinq", Exact = true })).ToBeVisibleAsync();

        await browserContext.CloseAsync();
    }

    public async Task<(IPage, IBrowserContext)> GetBrowserInstance()
    {
        var browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 0,
        });

        var browserContext = await browser.NewContextAsync();

        var page = await browserContext.NewPageAsync();

        return (page, browserContext);
    }
}
