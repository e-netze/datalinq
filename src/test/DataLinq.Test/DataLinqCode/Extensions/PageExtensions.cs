using Microsoft.Playwright;

namespace E.DataLinq.Test.DataLinqCode.Extensions;

public static class PageExtensions
{
    public static async Task LogIntoDataLinq(this IPage page)
    {
        await page.GetByText("Local A local datalinq").ClickAsync();

        var headingLocator = page.GetByRole(AriaRole.Heading);

        var headingText = await headingLocator.TextContentAsync();

        Assert.IsTrue(headingText.Contains("DataLinq.Code Login"), "Expected login heading not found.");

        await page.Locator("html").ClickAsync();

        await page.GetByPlaceholder("Enter username...").FillAsync("client2");

        await page.GetByPlaceholder("Enter password...").FillAsync("password");

        await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
    }

    public static async Task<List<string>?> ModalSelectFirst3(this IPage page, bool returnResults = true)
    {
        await page.Locator("#datalinq-code-modal-content").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var listItemsLocator = page.Locator("#datalinq-code-modal-content ul.datalinq-code-app-prefixes li.datalinq-code-app-prefix");

        List<string> selectedItems = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            var listItem = listItemsLocator.Nth(i);
            var text = await listItem.Locator(".text").TextContentAsync();
            var subtext = await listItem.Locator(".subtext").TextContentAsync();

            if (!string.IsNullOrEmpty(subtext))
            {
                selectedItems.AddRange(subtext.Split(',').Select(sub => $"{text}-{sub.Trim()}"));
            }
            else
            {
                selectedItems.Add(text);
            }

            await listItem.ClickAsync();
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Open selected" }).ClickAsync();
        return returnResults ? selectedItems : null;
    }

    public static async Task<List<string>?> ModalSelectAll(this IPage page, bool returnResults = true)
    {
        await page.Locator("#datalinq-code-modal-content").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var listItemsLocator = page.Locator("#datalinq-code-modal-content ul.datalinq-code-app-prefixes li.datalinq-code-app-prefix");

        var combinedItems = new List<string>();

        for (int i = 0; i < await listItemsLocator.CountAsync(); i++)
        {
            var listItem = listItemsLocator.Nth(i);
            var text = await listItem.Locator(".text").TextContentAsync();
            var subtext = await listItem.Locator(".subtext").TextContentAsync();

            if (!string.IsNullOrEmpty(subtext))
            {
                combinedItems.AddRange(subtext.Split(',').Select(sub => $"{text}-{sub.Trim()}"));
            }
            else
            {
                combinedItems.Add(text);
            }
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Open all" }).ClickAsync();
        return returnResults ? combinedItems : null;
    }

    public static async Task WaitForJQueryToFinish(this IPage page)
    {
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        await page.EvaluateAsync(@"(function() {
                                        return new Promise((resolve) => {
                                            if (window.jQuery) {
                                                var checkJQueryComplete = setInterval(function() {
                                                    if (window.jQuery.active === 0) {
                                                        clearInterval(checkJQueryComplete);
                                                        resolve(true);
                                                    }
                                                }, 100); // Check every 100ms if there are any active jQuery requests
                                            } else {
                                                resolve(true); // No jQuery loaded
                                            }
                                        });
                                    })()");

        await page.EvaluateAsync(@"(function() {
                                        return new Promise((resolve) => {
                                            var checkAnimationsComplete = setInterval(function() {
                                                if (!document.querySelector('body').classList.contains('animating')) {
                                                    clearInterval(checkAnimationsComplete);
                                                    resolve(true);
                                                }
                                            }, 100); // Check every 100ms for ongoing animations
                                        });
                                    })()");

    }
}
