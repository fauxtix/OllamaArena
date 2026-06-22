using Markdig;

namespace OllamaFluentUIChat.Services
{
    public static class CommonService
    {

         public static string FormatMessage(string content)
        {
            if (string.IsNullOrEmpty(content)) return "";

            if (content == "...")
            {
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";
            }

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();

            var html = Markdown.ToHtml(content, pipeline);
            html = html.TrimEnd('\n', '\r', ' ');

            return html.Replace("<p>", "<div>").Replace("</p>", "</div>");
        }
    }
}
