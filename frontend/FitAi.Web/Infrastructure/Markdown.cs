using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace FitAi.Web.Infrastructure;

/// <summary>
/// Converte a resposta do Coach AI (markdown) em HTML seguro. O texto vem de um modelo de IA, que pode ser
/// induzido (prompt injection) a gerar conteúdo malicioso, então: HTML bruto é desabilitado, links só são
/// mantidos para http/https/mailto (ex.: <c>javascript:</c> vira texto) e imagens viram o texto alternativo.
/// </summary>
public static partial class Markdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .Build();

    public static string ToHtml(string? markdown)
    {
        var document = Markdig.Markdown.Parse(markdown ?? "", Pipeline);
        Sanitize(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        // Só o renderer produz <a href=...> (HTML bruto está desabilitado); links abrem em nova aba.
        return LinkRegex().Replace(writer.ToString(), "<a target=\"_blank\" rel=\"noopener noreferrer\" href=");
    }

    public static bool IsSafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto";

    private static void Sanitize(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>().ToList())
        {
            if (!link.IsImage && IsSafeUrl(link.Url)) continue;

            // Mantém o texto do link (ou o texto alternativo da imagem) e remove o link.
            var child = link.FirstChild;
            while (child is not null)
            {
                var next = child.NextSibling;
                child.Remove();
                link.InsertBefore(child);
                child = next;
            }
            link.Remove();
        }

        foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
        {
            if (!IsSafeUrl(autolink.Url)) autolink.ReplaceBy(new LiteralInline(autolink.Url));
        }
    }

    [GeneratedRegex("<a href=")]
    private static partial Regex LinkRegex();
}
