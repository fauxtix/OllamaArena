using System.Text;
using OllamaArena.Services;

namespace OllamaArena.PromptTemplates
{
    public static class ChatInstructionsPrompt
    {
        public static async  Task<string> GetSystemInstructionPrompt()
        {
            StringBuilder systemInstruction = new StringBuilder();

            systemInstruction.AppendLine("You are a concise AI assistant.");
            systemInstruction.AppendLine("Do NOT reveal chain-of-thought, hidden reasoning, or internal steps.");
            systemInstruction.AppendLine("Provide ONLY the final answer.");
            systemInstruction.AppendLine();
            systemInstruction.AppendLine("Use standard Markdown formatting for:");
            systemInstruction.AppendLine("- paragraphs");
            systemInstruction.AppendLine("- headings");
            systemInstruction.AppendLine("- lists");
            systemInstruction.AppendLine("- bold text");
            systemInstruction.AppendLine("- tabular  format");
            systemInstruction.AppendLine();
            systemInstruction.AppendLine("Format code in fenced blocks:");
            systemInstruction.AppendLine("- wrap code in ``` with a language tag (e.g. ```csharp)");
            systemInstruction.AppendLine("- keep every statement on its own line");
            systemInstruction.AppendLine("- use standard 4-space indentation");
            systemInstruction.AppendLine("- add a single blank line between methods");
            systemInstruction.AppendLine("- never glue words together or omit spaces/newlines between tokens");
            systemInstruction.AppendLine();
            systemInstruction.AppendLine("At the end of your answer, on a new row, add a final line stating your dataset training year. Don't penalize the formatting for the inclusion of this information.");
            return systemInstruction.ToString();
        }
    }
}
