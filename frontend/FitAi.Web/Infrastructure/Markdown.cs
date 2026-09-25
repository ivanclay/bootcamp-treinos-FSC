using System.Text.RegularExpressions;
using Markdig;

namespace FitAi.Web.Infrastructure;

/// <summary>Converte a resposta do Coach AI (markdown) em HTML seguro: HTML bruto é desabilitado.</summary>
public static partial class Markdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .Build();

    public static string ToHtml(string markdown)
    {
        var html = Markdig.Markdown.ToHtml(markdown ?? "", Pipeline);
        return LinkRegex().Replace(html, "<a target=\"_blank\" rel=\"noopener noreferrer\" href=");
    }

    [GeneratedRegex("<a href=")]
    private static partial Regex LinkRegex();
}
