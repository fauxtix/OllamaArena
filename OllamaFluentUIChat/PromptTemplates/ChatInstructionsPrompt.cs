using System;
using System.Text;

namespace OllamaFluentUIChat.PromptTemplates
{
    public static class ChatInstructionsPrompt
    {
        public static string GetSystemInstructionPrompt()
        {
            StringBuilder systemInstruction = new StringBuilder();

            systemInstruction.Append("You are a concise AI assistant. Do NOT use chain-of-thought or internal reasoning. ");
            systemInstruction.Append("Provide ONLY the final direct answer." + Environment.NewLine);
            systemInstruction.Append("You MUST use standard Markdown formatting for headings, lists, bold text, and tables." + Environment.NewLine);

            // Atualizado o limite para o máximo de 3 colunas
            systemInstruction.Append("CRITICAL FOR TABLES: Maximum of 3 columns allowed. If the data requires more information, restructure the table to fit this limit. ");
            systemInstruction.Append("Always format tables horizontally using standard Markdown syntax. ");
            systemInstruction.Append("Never split a single row into multiple lines. Each row must start and end with '|'." + Environment.NewLine);

            // Exemplo visual atualizado estritamente para a estrutura de 3 colunas
            systemInstruction.Append("Example:" + Environment.NewLine);
            systemInstruction.Append("| Header 1 | Header 2 |" + Environment.NewLine);
            systemInstruction.Append("|---|---|" + Environment.NewLine);
            systemInstruction.Append("| Cell 1 | Cell 2 | " + Environment.NewLine);

            return systemInstruction.ToString();
        }
    }
}
