using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using OllamaFluentUIChat.Services;
namespace OllamaFluentUIChat.Components.Pages;

public partial class EditPromptFiles
{
    [Inject] protected ILogger<EditPromptFiles> Logger { get; set; } = default!;
    [Inject] protected IToastService ToastService { get; set; } = default!;
    [Inject] protected PromptFilesService PromptFilesService { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    private string _editorContent = string.Empty;
    public string EditorContent
    {
        get => _editorContent;
        set
        {
            if (_editorContent != value)
            {
                _editorContent = value;
            }
        }
    }

    protected string ToastTitle = "Prompt Editor";
    protected string ToastContent = "";
    protected string ToastCssClass = "";
    protected CancellationToken _cancellationToken = CancellationToken.None;
    protected List<string> _promptFiles = [];
    protected string _selectedPromptFile = string.Empty;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _isLoading = true;

            _promptFiles = PromptFilesService.GetPromptFilesAsync() ?? [];

            if (_promptFiles.Any())
            {
                _selectedPromptFile = _promptFiles.First();
                await LoadPromptFileContent(_selectedPromptFile);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading prompt files");
            ToastService.ShowError("Erro ao inicializar a lista de ficheiros.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnPromptFileChanged(string selectedFile)
    {
        if (string.IsNullOrEmpty(selectedFile) || _selectedPromptFile == selectedFile)
            return;

        _selectedPromptFile = selectedFile;
        await LoadPromptFileContent(_selectedPromptFile);
    }

    private async Task LoadPromptFileContent(string filename)
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            var promptContent = await PromptFilesService.GetPromptFileContentAsync(filename, _cancellationToken);
            EditorContent = promptContent ?? string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Erro ao obter o prompt: {filename}");
            ToastService.ShowError("Não foi possível carregar o ficheiro selecionado.");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnSave()
    {
        if (string.IsNullOrEmpty(_selectedPromptFile))
        {
            ToastService.ShowWarning("Nenhum ficheiro selecionado para gravar.");
            return;
        }

        try
        {
            await PromptFilesService.SavePromptFileAsync(_selectedPromptFile, EditorContent, CancellationToken.None);
            ToastService.ShowSuccess($"O ficheiro '{_selectedPromptFile}' foi gravado com sucesso!", timeout: 3000);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error in saving prompt {_selectedPromptFile}");
            ToastService.ShowError("Não foi possível gravar o prompt.");
        }
    }

    private async Task OnCancel()
    {
        if (!string.IsNullOrEmpty(_selectedPromptFile))
        {
            await LoadPromptFileContent(_selectedPromptFile);
        }
    }
}
