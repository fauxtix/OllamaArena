using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using OllamaArena.Resources;
using OllamaArena.Services;
using OllamaArena.Services.Helpers;
namespace OllamaArena.Components.Pages;

public partial class TestPlan
{
    private const string PlanFolder = "PlanoDeTestes";
    private const string PlanFileBase = "Prompts_Teste_Juiz";

    [Inject] protected ILogger<TestPlan> Logger { get; set; } = default!;
    [Inject] protected IStringLocalizer<SharedResources> L { get; set; } = default!;
    [Inject] protected IToastService ToastService { get; set; } = default!;
    [Inject] protected PromptFilesService PromptFilesService { get; set; } = default!;
    [Inject] protected MarkdownRenderer MarkdownRenderer { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    public string RenderedHtml { get; set; } = string.Empty;

    // Ficheiro resolvido pelo idioma ativo do seletor PT/EN (null = nenhum encontrado).
    protected string? _planFile;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _isLoading = true;

            var ficheiros = PromptFilesService.GetPromptFiles(PlanFolder) ?? [];
            _planFile = ResolvePlanFileName(ficheiros);

            if (!string.IsNullOrEmpty(_planFile))
            {
                await LoadPlanFileContent(_planFile);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Erro ao inicializar o plano de testes.");
            ToastService.ShowError(L["TestPlan.InitError"]);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Escolhe automaticamente o ficheiro conforme o idioma da UI (cookie PT/EN):
    /// versão localizada com fallback ao ficheiro base quando esta não existe.
    /// </summary>
    private static string? ResolvePlanFileName(IReadOnlyCollection<string> ficheiros)
    {
        var codigo = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

    IEnumerable<string> candidatos = codigo == "pt"
        ? [$"{PlanFileBase}.md"]
        : [$"{PlanFileBase}.{codigo}.md", $"{PlanFileBase}.md"];

        return candidatos.FirstOrDefault(c => ficheiros.Contains(c, StringComparer.OrdinalIgnoreCase));
    }

    private async Task LoadPlanFileContent(string filename)
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            var content = await PromptFilesService.GetPromptFileContentAsync(filename, PlanFolder);
            RenderedHtml = content is null ? string.Empty : MarkdownRenderer.ToHtml(content);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Erro ao obter o ficheiro do plano de testes: {File}", filename);
            ToastService.ShowError(L["TestPlan.LoadError"]);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}
