namespace MyAfricaMyNarrative.BrowserTests;

public sealed class PublicPageTests
{
    [Fact]
    public async Task Public_pages_and_static_assets_are_reachable_and_accessible_by_structure()
    {
        var configured = Environment.GetEnvironmentVariable("MAMN_BASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(configured));
        using var client = new HttpClient { BaseAddress = new Uri(configured!) };

        var login = await client.GetStringAsync("/Account/Login");
        Assert.Contains("Your story. Your voice. Our Africa.", login);
        Assert.Contains("<main", login, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<label", login, StringComparison.OrdinalIgnoreCase);
        var help = await client.GetStringAsync("/Home/Help");
        Assert.Contains("Record", help);
        Assert.Contains("Shape", help);
        Assert.Contains("Make", help);
        Assert.Contains("Export", help);
        Assert.Contains("aria-label", help, StringComparison.OrdinalIgnoreCase);
        var css = await client.GetStringAsync("/css/site.css");
        Assert.Contains("@media", css);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("prefers-reduced-motion", css);
    }
}
