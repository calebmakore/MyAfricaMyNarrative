using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace MyAfricaMyNarrative.Infrastructure.Email;

/// <summary>Sends account messages such as password reset links.</summary>
public interface IAccountEmailSender
{
    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken);
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// "Pickup" writes .eml files to <see cref="PickupDirectory"/> for local development; nothing leaves the machine.
    /// "Smtp" delivers through the configured server. Credentials belong in User Secrets or environment variables.
    /// </summary>
    public string Mode { get; set; } = "Pickup";
    public string PickupDirectory { get; set; } = "App_Data/mail-outbox";
    /// <summary>Public site address used in emailed links, e.g. https://stories.example.org. Required outside Development.</summary>
    public string? PublicBaseUrl { get; set; }
    public string FromAddress { get; set; } = "no-reply@localhost";
    public string FromName { get; set; } = "My Africa, My Narrative";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpEnableSsl { get; set; } = true;
    public string? SmtpUserName { get; set; }
    public string? SmtpPassword { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    /// <summary>How long a reset link stays valid.</summary>
    public int ResetLinkLifetimeMinutes { get; set; } = 60;
}

public static class PasswordResetMessage
{
    public const string Subject = "Reset your My Africa, My Narrative password";

    public static MailMessage Create(MailAddress from, string toEmail, string resetLink, int lifetimeMinutes)
    {
        if (!Uri.TryCreate(resetLink, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("Reset link must be an absolute http(s) URL.", nameof(resetLink));

        var text = new StringBuilder()
            .AppendLine("Someone asked to reset the password for your My Africa, My Narrative account.")
            .AppendLine()
            .AppendLine("Open this link to choose a new password:")
            .AppendLine(resetLink)
            .AppendLine()
            .AppendLine($"The link works once and expires in {lifetimeMinutes} minutes. If you did not ask for this, you can ignore this email; your password has not changed.")
            .AppendLine()
            .AppendLine("Your story. Your voice. Our Africa.")
            .ToString();
        var encoded = WebUtility.HtmlEncode(resetLink);
        var html = $"""
            <p>Someone asked to reset the password for your My Africa, My Narrative account.</p>
            <p><a href="{encoded}">Choose a new password</a></p>
            <p>The link works once and expires in {lifetimeMinutes} minutes. If you did not ask for this, you can ignore this email; your password has not changed.</p>
            <p>Your story. Your voice. Our Africa.</p>
            """;

        var message = new MailMessage { From = from, Subject = Subject, SubjectEncoding = Encoding.UTF8, Body = text, BodyEncoding = Encoding.UTF8, IsBodyHtml = false };
        message.To.Add(new MailAddress(toEmail));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, "text/html"));
        return message;
    }
}

public sealed class SmtpAccountEmailSender(IOptions<EmailOptions> options) : IAccountEmailSender
{
    public async Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken)
    {
        var config = options.Value;
        using var message = PasswordResetMessage.Create(new MailAddress(config.FromAddress, config.FromName), toEmail, resetLink, config.ResetLinkLifetimeMinutes);
        using var client = new SmtpClient { Timeout = Math.Clamp(config.TimeoutSeconds, 5, 120) * 1000 };
        if (config.Mode == "Smtp")
        {
            client.Host = config.SmtpHost!;
            client.Port = config.SmtpPort;
            client.EnableSsl = config.SmtpEnableSsl;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            if (!string.IsNullOrWhiteSpace(config.SmtpUserName))
                client.Credentials = new NetworkCredential(config.SmtpUserName, config.SmtpPassword);
        }
        else
        {
            var directory = Path.GetFullPath(config.PickupDirectory);
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
        }
        await client.SendMailAsync(message, cancellationToken);
    }
}
