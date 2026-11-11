using System.Net.Mail;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Infrastructure.Email;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class PasswordResetEmailTests
{
    private const string Link = "https://stories.example.org/Account/ResetPassword?userId=6f1c2a64-0000-4000-8000-000000000001&code=abc_DEF-123";

    [Fact]
    public void Message_contains_exact_link_in_text_and_html_encoded_in_html()
    {
        using var message = PasswordResetMessage.Create(new MailAddress("no-reply@example.org"), "creator@example.org", Link, 60);
        Assert.Equal("creator@example.org", message.To.Single().Address);
        Assert.Contains(Link, message.Body);
        Assert.Contains("expires in 60 minutes", message.Body);
        Assert.Contains("Your story. Your voice. Our Africa.", message.Body);
        var html = new StreamReader(message.AlternateViews.Single().ContentStream).ReadToEnd();
        Assert.Contains(Link.Replace("&", "&amp;"), html);
        Assert.DoesNotContain("&code", html);
    }

    [Theory]
    [InlineData("/Account/ResetPassword?code=x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org/reset")]
    public void Message_rejects_relative_or_non_http_links(string link) =>
        Assert.Throws<ArgumentException>(() => PasswordResetMessage.Create(new MailAddress("no-reply@example.org"), "creator@example.org", link, 60));

    [Fact]
    public async Task Pickup_mode_writes_one_eml_file_and_sends_nothing_over_the_network()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mamn-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sender = new SmtpAccountEmailSender(Options.Create(new EmailOptions { Mode = "Pickup", PickupDirectory = directory, FromAddress = "no-reply@example.org" }));
            await sender.SendPasswordResetAsync("creator@example.org", Link, CancellationToken.None);
            var file = Assert.Single(Directory.GetFiles(directory, "*.eml"));
            var raw = await File.ReadAllTextAsync(file);
            Assert.Contains("creator@example.org", raw);
            Assert.Contains("Reset your My Africa", raw);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
