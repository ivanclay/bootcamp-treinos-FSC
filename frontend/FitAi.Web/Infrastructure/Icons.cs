using Microsoft.AspNetCore.Html;

namespace FitAi.Web.Infrastructure;

/// <summary>Ícones SVG inline (traçados no estilo Lucide, 24×24).</summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["home"] = "<path d=\"M3 10.5 12 3l9 7.5\"/><path d=\"M5 9.5V21h5v-6h4v6h5V9.5\"/>",
        ["calendar"] = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"18\" rx=\"2\"/><path d=\"M16 2v4M8 2v4M3 10h18\"/>",
        ["sparkles"] = "<path d=\"M12 3l1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9z\"/><path d=\"M19 15l.9 2.1L22 18l-2.1.9L19 21l-.9-2.1L16 18l2.1-.9z\"/><path d=\"M5 2l.6 1.4L7 4l-1.4.6L5 6l-.6-1.4L3 4l1.4-.6z\"/>",
        ["chart"] = "<path d=\"M18 20V10M12 20V4M6 20v-6\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 21c0-4 4-6 8-6s8 2 8 6\"/>",
        ["users"] = "<circle cx=\"9\" cy=\"8\" r=\"4\"/><path d=\"M1 21c0-4 4-6 8-6s8 2 8 6\"/><path d=\"M16 3.1a4 4 0 0 1 0 7.8M23 21c0-3-2-5-5-5.7\"/>",
        ["flame"] = "<path d=\"M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.4-.5-2-1-3-1.1-2.1-.2-4 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.2.4-2.3 1-3.2.3 1.5 1.3 2.7 2.5 2.7z\"/>",
        ["timer"] = "<circle cx=\"12\" cy=\"13\" r=\"8\"/><path d=\"M12 9v4l2 2M10 2h4\"/>",
        ["dumbbell"] = "<path d=\"M6.5 6.5 17.5 17.5M21 21l-1-1M3 3l1 1M18 22l4-4M2 6l4-4M3 10l7-7M14 21l7-7\"/>",
        ["zap"] = "<path d=\"M13 2 3 14h9l-1 8 10-12h-9z\"/>",
        ["arrow-left"] = "<path d=\"M19 12H5M12 19l-7-7 7-7\"/>",
        ["arrow-up"] = "<path d=\"M12 19V5M5 12l7-7 7 7\"/>",
        ["x"] = "<path d=\"M18 6 6 18M6 6l12 12\"/>",
        ["help"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3M12 17h.01\"/>",
        ["log-out"] = "<path d=\"M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9\"/>",
        ["weight"] = "<circle cx=\"12\" cy=\"5\" r=\"3\"/><path d=\"M6.5 8h11l3 13h-17z\"/>",
        ["ruler"] = "<path d=\"M21.3 15.3 8.7 2.7a1 1 0 0 0-1.4 0L2.7 7.3a1 1 0 0 0 0 1.4l12.6 12.6a1 1 0 0 0 1.4 0l4.6-4.6a1 1 0 0 0 0-1.4zM7.5 10.5l2-2M10.5 13.5l2-2M13.5 16.5l2-2\"/>",
        ["percent"] = "<path d=\"M19 5 5 19\"/><circle cx=\"6.5\" cy=\"6.5\" r=\"2.5\"/><circle cx=\"17.5\" cy=\"17.5\" r=\"2.5\"/>",
        ["cake"] = "<path d=\"M20 21v-8a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8M4 16s1.5-1 4-1 4 2 6 2 4-1 4-1M2 21h20M7 8v3M12 8v3M17 8v3M7 4h.01M12 4h.01M17 4h.01\"/>",
        ["play"] = "<path d=\"m6 3 14 9-14 9z\"/>",
        ["check"] = "<path d=\"M20 6 9 17l-5-5\"/>",
        ["check-circle"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"m9 12 2 2 4-4\"/>",
        ["chevron-right"] = "<path d=\"m9 18 6-6-6-6\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["trash"] = "<path d=\"M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6\"/>",
        ["edit"] = "<path d=\"M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z\"/>",
        ["ticket"] = "<path d=\"M2 9a3 3 0 0 0 0 6v3a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-3a3 3 0 0 0 0-6V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2z\"/><path d=\"M13 5v2M13 17v2M13 11v2\"/>",
        ["mail"] = "<rect x=\"2\" y=\"4\" width=\"20\" height=\"16\" rx=\"2\"/><path d=\"m22 7-10 6L2 7\"/>",
        ["settings"] = "<circle cx=\"12\" cy=\"12\" r=\"3\"/><path d=\"M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z\"/>",
        ["dashboard"] = "<rect x=\"3\" y=\"3\" width=\"7\" height=\"9\" rx=\"1\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"5\" rx=\"1\"/><rect x=\"14\" y=\"12\" width=\"7\" height=\"9\" rx=\"1\"/><rect x=\"3\" y=\"16\" width=\"7\" height=\"5\" rx=\"1\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"7\"/><path d=\"m21 21-4.3-4.3\"/>",
        ["target"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><circle cx=\"12\" cy=\"12\" r=\"6\"/><circle cx=\"12\" cy=\"12\" r=\"2\"/>",
        ["hourglass"] = "<path d=\"M5 22h14M5 2h14M17 22v-4.2a2 2 0 0 0-.6-1.4L12 12l-4.4 4.4a2 2 0 0 0-.6 1.4V22M7 2v4.2a2 2 0 0 0 .6 1.4L12 12l4.4-4.4a2 2 0 0 0 .6-1.4V2\"/>",
        ["circle-check"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"m9 12 2 2 4-4\"/>",
        ["google"] = "",
    };

    public static IHtmlContent Get(string name, int size = 20, string? cssClass = null)
    {
        if (name == "google") return new HtmlString(Google(size));
        var path = Paths.GetValueOrDefault(name, "");
        var cls = cssClass is null ? "icon" : "icon " + cssClass;
        return new HtmlString(
            $"<svg class=\"{cls}\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" " +
            $"stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">{path}</svg>");
    }

    private static string Google(int size) =>
        $"<svg width=\"{size}\" height=\"{size}\" viewBox=\"0 0 48 48\" aria-hidden=\"true\">" +
        "<path fill=\"#FFC107\" d=\"M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z\"/>" +
        "<path fill=\"#FF3D00\" d=\"m6.3 14.7 6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z\"/>" +
        "<path fill=\"#4CAF50\" d=\"M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z\"/>" +
        "<path fill=\"#1976D2\" d=\"M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z\"/></svg>";
}
