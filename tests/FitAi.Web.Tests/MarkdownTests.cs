using FitAi.Web.Infrastructure;

namespace FitAi.Web.Tests;

public class MarkdownTests
{
    [Theory]
    [InlineData("[clique](javascript:alert(1))")]
    [InlineData("[clique](JavaScript:alert(1))")]
    [InlineData("[clique](javascript&#58;alert(1))")]
    [InlineData("[clique](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)")]
    [InlineData("[clique](vbscript:msgbox(1))")]
    [InlineData("[clique](//evil.com/x)")]
    public void UnsafeLinks_BecomePlainText(string markdown)
    {
        var html = Markdown.ToHtml(markdown);
        Assert.DoesNotContain("<a", html);
        Assert.Contains("clique", html);
    }

    [Fact]
    public void RawHtml_IsEscaped()
    {
        var html = Markdown.ToHtml("<script>alert(1)</script><img src=x onerror=alert(1)>");
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void Images_BecomeAltText()
    {
        var html = Markdown.ToHtml("![rastreador](https://evil.com/pixel.png)");
        Assert.DoesNotContain("<img", html);
        Assert.Contains("rastreador", html);
    }

    [Fact]
    public void SafeLinks_AreKept_AndOpenInNewTab()
    {
        var html = Markdown.ToHtml("Veja [o vídeo](https://www.youtube.com/watch?v=abc) e **capriche**.");
        Assert.Contains("<a target=\"_blank\" rel=\"noopener noreferrer\" href=\"https://www.youtube.com/watch?v=abc\">o vídeo</a>", html);
        Assert.Contains("<strong>capriche</strong>", html);
    }

    [Fact]
    public void Autolinks_OnlyHttp()
    {
        Assert.Contains("href=\"https://exemplo.com\"", Markdown.ToHtml("<https://exemplo.com>"));
    }
}
