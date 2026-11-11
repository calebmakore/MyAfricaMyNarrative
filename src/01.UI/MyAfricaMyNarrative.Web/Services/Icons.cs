using Microsoft.AspNetCore.Html;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Web.Services;

/// <summary>Small inline SVG icons (decorative; every use sits beside a visible text label).</summary>
public static class Icons
{
    private const string Open = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">";

    public static IHtmlContent Mic => new HtmlString(Open + "<rect x=\"9\" y=\"2.5\" width=\"6\" height=\"12\" rx=\"3\"/><path d=\"M5 11a7 7 0 0 0 14 0M12 18v3.5M8.5 21.5h7\"/></svg>");
    public static IHtmlContent Shape => new HtmlString(Open + "<path d=\"M3 6h18M3 12h12M3 18h7\"/><path d=\"m15 16 3 3 4-5\"/></svg>");
    public static IHtmlContent Make => new HtmlString(Open + "<rect x=\"2.5\" y=\"6\" width=\"14\" height=\"12\" rx=\"2\"/><path d=\"m16.5 10 5-3v10l-5-3\"/></svg>");
    public static IHtmlContent Export => new HtmlString(Open + "<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\"/><path d=\"M3 8h18M3 16h18M8 3v5M16 3v5M8 16v5M16 16v5\"/></svg>");
    public static IHtmlContent Check => new HtmlString(Open + "<path d=\"m5 12.5 4.5 4.5L19 7.5\"/></svg>");

    public static IHtmlContent ForStage(StudioStage stage) => stage switch
    {
        StudioStage.Record => Mic,
        StudioStage.Shape => Shape,
        StudioStage.Make => Make,
        _ => Export
    };

    /// <summary>Brand emblem: an abstract geometric sun.</summary>
    public static IHtmlContent Emblem => new HtmlString(
        "<svg viewBox=\"0 0 48 48\" aria-hidden=\"true\" focusable=\"false\">" +
        "<path d=\"M24 2 30 12 24 22 18 12z\" fill=\"#e3a33a\"/><path d=\"M24 46 18 36 24 26 30 36z\" fill=\"#e3a33a\"/>" +
        "<path d=\"M2 24 12 18 22 24 12 30z\" fill=\"#b8472a\"/><path d=\"M46 24 36 30 26 24 36 18z\" fill=\"#b8472a\"/>" +
        "<circle cx=\"24\" cy=\"24\" r=\"5\" fill=\"#fffdf8\"/></svg>");
}
