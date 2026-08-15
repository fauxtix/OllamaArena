namespace OllamaArena.Services;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public int DefaultContextLength { get; set; } = 2048;

    public double HistoryBudgetFraction { get; set; } = 0.60;

    public double ContextReserveFraction { get; set; } = 0.25;

    public int BaseTokensGpu { get; set; } = 1800;

    public int BaseTokensCpu { get; set; } = 1200;

    public double RepeatPenalty { get; set; } = 1.1;

    public int TopKLow { get; set; } = 40;

    public int TopKHigh { get; set; } = 600;

    public double TopPLow { get; set; } = 0.85;

    public double TopPHigh { get; set; } = 0.92;

    public double ModelLoadOverheadFactor { get; set; } = 1.2;

    public long WindowsOverheadMb { get; set; } = 350;

    public double VramReserveFraction { get; set; } = 0.10;

    public int MaxContextCap { get; set; } = 32768;

    public bool EnableReasoning { get; set; } = false;
}
