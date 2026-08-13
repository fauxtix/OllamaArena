namespace OllamaArena.Models.DTO;

public class OpenRouterCatalogModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? ContextLength { get; set; }
    public bool IsFree { get; set; }
}
