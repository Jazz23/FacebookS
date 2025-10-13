// Use playwright to open a testing browser for the user to use
using Microsoft.Playwright;

const string listingTextClass = ".x1lliihq.x6ikm8r.x10wlt62.x1n2onr6";
const string url =
    "https://www.facebook.com/marketplace/113339992009833/search/?maxPrice=0&sortBy=creation_time_descend&query=free&exact=false";

DotNetEnv.Env.Load();

using var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
    Proxy = new Proxy
    {
        Server = System.Environment.GetEnvironmentVariable("PROXY_HOST")!,
        Username = System.Environment.GetEnvironmentVariable("PROXY_USERNAME")!,
        Password = System.Environment.GetEnvironmentVariable("PROXY_PASSWORD")!
    }
});

// Set the user agent to Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Mobile Safari/537.36

var context = await browser.NewContextAsync();
var page = await context.NewPageAsync();

await page.RouteAsync("**/*", async route =>
{
    var request = route.Request;
    if (request.ResourceType == "image")
        await route.AbortAsync(); // cancel image requests
    else
        await route.ContinueAsync(); // allow everything else
});

// Navigate to Facebook
await page.GotoAsync(url);
// Wait for elements with the listingTextClass class to load
var candidates = await page.QuerySelectorAllAsync(listingTextClass);

foreach (var element in candidates)
{
    if (await IsListingTitle(element))
    {
        var text = await element.InnerTextAsync();
        // Continously traverse up the DOM tree until we find an <a> element
        // This is the link to the listing
        var link = await element.EvaluateAsync<string?>(@"(element) => {
            while (element) {
                if (element.tagName === 'A') {
                    return element.href;
                }
                element = element.parentElement;
            }
            return null;
        }");
        
        Console.WriteLine();
    }
}




Console.WriteLine("Press any key to close the browser...");
Console.ReadKey();

await browser.CloseAsync();
return;

async Task<bool> IsListingTitle(IElementHandle element)
{
    var classString = await element.GetAttributeAsync("class");
    return classString is "x1lliihq x6ikm8r x10wlt62 x1n2onr6";
}