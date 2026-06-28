using System.Text;

namespace OllamaFluentUIChat.PromptTemplates
{
    public static class ChatInstructionsPrompt
    {
        public static string GetSystemInstructionPrompt()
        {
            StringBuilder systemInstruction = new StringBuilder();

            systemInstruction.Append("You are a concise AI assistant. Do NOT use chain-of-thought or internal reasoning. ");
            systemInstruction.Append("Provide ONLY the final direct answer.\n");
            systemInstruction.Append("You MUST use standard Markdown formatting for headings, lists, bold text, and tables.\n");
            systemInstruction.Append("CRITICAL FOR TABLES: Always format tables horizontally using standard Markdown syntax. ");
            systemInstruction.Append("Never split a single row into multiple lines. Each row must start and end with '|'.\n");
            systemInstruction.Append("Example:\n| Header 1 | Header 2 |\n|---|---|\n| Cell 1 | Cell 2 |");

            return systemInstruction.ToString();
        }
    }
}