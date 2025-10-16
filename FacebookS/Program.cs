// Use playwright to open a testing browser for the user to use

using System.Diagnostics;
using System.Text.Json;
using System.Threading.RateLimiting;
using FacebookS;
using Microsoft.Playwright;
using ListingDatabase = System.Collections.Generic.List<FacebookS.Listing>;

const string ListingTextClass = ".x1lliihq.x6ikm8r.x10wlt62.x1n2onr6";
const string Url =
    "https://www.facebook.com/marketplace/113339992009833/search/?maxPrice=0&sortBy=creation_time_descend&query=free&exact=false&radius_in_km=16";
const string DatabasePath = "listings.json";
const int RefreshIntervalSeconds = 60;

DotNetEnv.Env.Load();
var database = new Database(DatabasePath);

var limiter = new TokenBucketRateLimiter(
    new TokenBucketRateLimiterOptions
    {
        TokenLimit = 1,                    // max tokens
        TokensPerPeriod = 1,               // how many refill each period
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = 100,                    // optional waiting queue
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

var handler = new RateLimitedHandler(new HttpClientHandler(), limiter);
var httpClient = new HttpClient(handler);

using var playwright = await Playwright.CreateAsync();
var (page, browser) = await InitializePlaywright(playwright);

while (true)
{
    async Task ResetBrowser()
    {
        await browser.CloseAsync();
        await browser.DisposeAsync();
        (page, browser) = await InitializePlaywright(playwright);
    }
    
    try
    {
        Console.WriteLine("Refreshing Listings at " + DateTime.Now);
    
        var listings = await RefreshListings();
        listings.Reverse(); // Put the oldest listings first so we post them in order
    
        // If the listing count is 0, restart the browser
        if (listings.Count == 0)
        {
            Console.WriteLine("No listings found, restarting browser...");
            await ResetBrowser();
            continue;
        }
    
        foreach (var listing in listings)
        {
            if (!await database.StoreInDatabase(listing)) continue;

            await PostListingToDiscord(listing);
            Console.WriteLine($"Found new listing {listing.Title} - {listing.Link}");
        }

        Console.WriteLine($"Found {listings.Count} listings, waiting {RefreshIntervalSeconds} seconds to refresh...");
        await Task.Delay(RefreshIntervalSeconds * 1000);
    }
    catch (Exception e)
    {
        await PostMessageToDiscord("Error occurred: " + e.Message);
        Console.WriteLine("Error occurred: " + e.Message);
        await ResetBrowser();
    }
}

return;

async Task<(IPage page, IBrowser browser)> InitializePlaywright(IPlaywright pw)
{
    
    var loadedMarketplace = false;
    IBrowser browserResult;
    IPage pageResult;

    // Keep trying to load the marketplace until we succeed
    do
    {
        // Launch the browser with proxy settings from environment variables
        browserResult = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Proxy = new Proxy
            {
                Server = System.Environment.GetEnvironmentVariable("PROXY_HOST")!,
                Username = System.Environment.GetEnvironmentVariable("PROXY_USERNAME")!,
                Password = System.Environment.GetEnvironmentVariable("PROXY_PASSWORD")!
            }
        });

        // Create a new browser context with a custom user agent for consistency across platforms
        var context = await browserResult.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/140.0.7339.16 Safari/537.36"
        });
        pageResult = await context.NewPageAsync();
        
        // Intercept network requests to block images to save on bandwidth
        await pageResult.RouteAsync("**/*", async route =>
        {
            // Hide exceptions here because they override real exceptions
            try
            {
                var request = route.Request;
                if (request.ResourceType == "image")
                    await route.AbortAsync(); // cancel image requests
                else
                    await route.ContinueAsync(); // allow everything else
            }
            catch (Exception e)
            {

            }
        });
        
        // Navigate to Facebook Marketplace
        await pageResult.GotoAsync(Url);
        
        // If the page redirects to a login screen, relaunch a new browser and try again
        if (pageResult.Url.Contains("login"))
        {
            await browserResult.CloseAsync();
            await browserResult.DisposeAsync();
            Console.WriteLine("Login screen detected");
        }
        else
        {
            loadedMarketplace = true;
        }
    }
    while (!loadedMarketplace);
    
    // If the page shows "Decline optional cookies", click it
    var declineButton = await pageResult.QuerySelectorAsync("text=Decline optional cookies");
    if (declineButton != null)
        await declineButton.ClickAsync();
    // Click on the button whose aria-label="Close"
    await pageResult.ClickAsync("div[aria-label='Close']");
    // Click on the radius selection dropdown
    await pageResult.ClickAsync(
        ".x193iq5w.xeuugli.x13faqbe.x1vvkbs.x1xmvt09.x1lliihq.x1s928wv.xhkezso.x1gmr53x.x1cpjm7i.x1fgarty.x1943h6x.xudqn12.x3x7a5m.x6prxxf.xvq8zen.x1s688f.x1fey0fg");
    // Click onthe element with class "xjyslct xjbqb8w x13fuv20 x18b5jzi x1q0q8m5 x1t7ytsu x972fbf x10w94by x1qhh985 x14e42zd x9f619 xzsf02u x78zum5 x1jchvi3 x1fcty0u x132q4wb xdj266r x14z9mp xat24cr x1lziwak x1a2a7pz x1a8lsjc xv54qhq xf7dkkf x9desvi x1n2onr6 x16tdsg8 xh8yej3 x1ja2u2z"
    await pageResult.ClickAsync(
        ".xjyslct.xjbqb8w.x13fuv20.x18b5jzi.x1q0q8m5.x1t7ytsu.x972fbf.x10w94by.x1qhh985.x14e42zd.x9f619.xzsf02u.x78zum5.x1jchvi3.x1fcty0u.x132q4wb.xdj266r.x14z9mp.xat24cr.x1lziwak.x1a2a7pz.x1a8lsjc.xv54qhq.xf7dkkf.x9desvi.x1n2onr6.x16tdsg8.xh8yej3.x1ja2u2z");
    
    // Click on the element whose inner text is "10 miles" or "20 kilometres"
    await pageResult.Locator(":text-matches('^(10 miles|20 kilometres)$')").ClickAsync();

    
    // Delete the element whose class is "x78zum5 xdt5ytf x2lah0s x193iq5w x2bj2ny x1ey2m1c xayqjjm x9f619 xtijo5x x1o0tod x1xy6bms xpdmqnj x1s14bel x1g0dm76 xixxii4 x8hos8a x1u8a7rm"
    await pageResult.EvaluateAsync(@"() => {
        const element = document.querySelector('.x78zum5.xdt5ytf.x2lah0s.x193iq5w.x2bj2ny.x1ey2m1c.xayqjjm.x9f619.xtijo5x.x1o0tod.x1xy6bms.xpdmqnj.x1s14bel.x1g0dm76.xixxii4.x8hos8a.x1u8a7rm');
        if (element) {
            element.remove();
        }
    }");

    await pageResult.ClickAsync("text=Apply");
    
    // Submit the input whose aria-label="Search Marketplace"
    await pageResult.ClickAsync("input[placeholder='Search Marketplace']");
    await pageResult.PressAsync("input[placeholder='Search Marketplace']", "Enter");

    // Wait for the page to load
    await pageResult.WaitForLoadStateAsync(LoadState.NetworkIdle);
    
    return (pageResult, browserResult);
}

async Task PostListingToDiscord(Listing listing)
{
    var payload = new
    {
        content = $"{listing.Title}\n{listing.Link}",
        embeds = new[]
        {
            new
            {
                image = new
                {
                    url = listing.ImageLink
                }
            }
        }
    };

    var jsonPayload = JsonSerializer.Serialize(payload);
    var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
    var response = await httpClient.PostAsync(System.Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL")!, content);
    response.EnsureSuccessStatusCode();
}

async Task PostMessageToDiscord(string message)
{
    var payload = new
    {
        content = message
    };

    var jsonPayload = JsonSerializer.Serialize(payload);
    var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
    var response = await httpClient.PostAsync(System.Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL")!, content);
    response.EnsureSuccessStatusCode();
}

async Task<ListingDatabase> RefreshListings()
{
    var results = new ListingDatabase();
    // Navigate to Facebook
    await page.ReloadAsync();
    
    // take a screenshot
    await page.ScreenshotAsync(new PageScreenshotOptions { Path = "screenshot.png" });
    // Wait for elements with the listingTextClass class to load
    var candidates = await page.QuerySelectorAllAsync(ListingTextClass);
    var retries = 5;
    while (candidates.Count == 0 && retries > 0)
    {
        await Task.Delay(1000);
        candidates = await page.QuerySelectorAllAsync(ListingTextClass);
        retries--;
    }
    
    foreach (var element in candidates)
    {
        if (!await IsListingTitle(element)) continue;
        
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
        
        // Go up 7 parents then search it's children and grandchildren etc for an <img> element
        var imageLink = await element.EvaluateAsync<string?>(@"(element) => {
            for (let i = 0; i < 7; i++) {
                if (!element.parentElement) return null;
                element = element.parentElement;
            }
            const img = element.querySelector('img');
            return img ? img.src : null;
        }");
        
        if (link is null || imageLink is null) continue;
        results.Add(new Listing(text, link, imageLink));
    }
    
    return results;
}

async Task<bool> IsListingTitle(IElementHandle element)
{
    var classString = await element.GetAttributeAsync("class");
    return classString is "x1lliihq x6ikm8r x10wlt62 x1n2onr6";
}