namespace OllamaArena.Services.Helpers
{
    // Reconstrução inteligente de tabelas a partir de linhas com pipes.
    public static partial class MessageFormatter
    {
        private static string ReconstructTables(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<string>();
            var tableBuffer = new List<string>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.Length >= 3 && trimmed.All(c => c == '-'))
                {
                    continue; // ignora separadores de markdown antigos
                }

                if (trimmed.StartsWith("|") || trimmed.Contains("|"))
                {
                    tableBuffer.Add(line); // guarda a linha original
                    continue;
                }

                // Fechar tabela anterior
                if (tableBuffer.Count > 0)
                {
                    result.AddRange(RebuildTableProperly(tableBuffer));
                    tableBuffer.Clear();
                }

                result.Add(line);
            }

            if (tableBuffer.Count > 0)
                result.AddRange(RebuildTableProperly(tableBuffer));

            return string.Join("\n", result);
        }

        private static List<string> RebuildTableProperly(List<string> rawLines)
        {
            if (rawLines.Count == 0) return new List<string>();

            // Detectar número real de colunas pela linha de header ou pela mais completa
            int columnCount = EstimateBestColumnCount(rawLines);

            var output = new List<string>();

            foreach (var line in rawLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cells = line.Split('|')
                                .Select(x => x.Trim())
                                .Where(x => !string.IsNullOrWhiteSpace(x) || output.Count == 0) // permite células vazias no header
                                .ToList();

                // Ajustar quantidade de colunas
                while (cells.Count < columnCount)
                    cells.Add("");
                while (cells.Count > columnCount)
                    cells.RemoveAt(cells.Count - 1); // remove excedentes

                string formattedLine = "| " + string.Join(" | ", cells) + " |";
                output.Add(formattedLine);
            }

            // Adicionar separador se não existir
            if (output.Count > 0 && !output.Any(l => l.Contains("---")))
            {
                var separator = "| " + string.Join(" | ", Enumerable.Repeat("---", columnCount)) + " |";
                output.Insert(1, separator); // insere após o header
            }

            return output;
        }

        private static int EstimateBestColumnCount(List<string> lines)
        {
            int best = 0;

            foreach (var line in lines)
            {
                int pipes = line.Count(c => c == '|');
                if (pipes > best)
                    best = pipes;
            }

            return Math.Max(best - 1, 2); // pelo menos 2 colunas
        }
    }
}
