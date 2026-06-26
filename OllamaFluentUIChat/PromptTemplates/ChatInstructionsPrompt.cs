using System.Text;

namespace OllamaFluentUIChat.PromptTemplates
{
    public static class ChatInstructionsPrompt
    {
        public static string GetSystemInstructionPrompt()
        {
            StringBuilder systemInstruction = new StringBuilder();

            systemInstruction.Append("Do NOT use chain-of-thought. Do NOT reveal internal reasoning. ");
            systemInstruction.Append("Provide ONLY the final answer, concise and direct. ");
            systemInstruction.Append("Be factual and precise. ");
            systemInstruction.Append("If you are not certain about a specific detail, omit it and state only the confirmed information. ");
            systemInstruction.Append("Limit the answer to approximately 100-120 words. ");
            systemInstruction.Append("Keep the output concise and compact. ");

            return systemInstruction.ToString();
        }
    }
}
