using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using OllamaFluentUIChat.Resources;
using OllamaFluentUIChat.Services;
namespace OllamaFluentUIChat.Components.Pages;

public partial class EditPromptFiles : IDisposable
{
    [Inject] protected ILogger<EditPromptFiles> Logger { get; set; } = default!;
    [Inject] protected IStringLocalizer<SharedResources> L { get; set; } = default!;
    [Inject] protected IToastService ToastService { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;
    [Inject] protected PromptFilesService PromptFilesService { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    private string _editorContent = string.Empty;
    private string _loadedContent = string.Empty;
    private IDisposable? _locationChangingRegistration;

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

    public bool HasUnsavedChanges => _editorContent != _loadedContent;

    protected List<string> _promptFiles = [];
    protected string _selectedPromptFile = string.Empty;
    private bool _isLoading = true;

    protected override void OnInitialized()
    {
        _locationChangingRegistration = NavigationManager.RegisterLocationChangingHandler(OnLocationChanging);
    }

    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (!HasUnsavedChanges)
            return;

        if (!await ConfirmDiscardAsync())
            context.PreventNavigation();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _isLoading = true;

            _promptFiles = PromptFilesService.GetPromptFiles() ?? [];

            if (_promptFiles.Any())
            {
                _selectedPromptFile = _promptFiles.First();
                await LoadPromptFileContent(_selectedPromptFile);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Erro ao inicializar a lista de ficheiros de prompt.");
            ToastService.ShowError(L["EditPrompts.InitError"]);
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

        if (!await ConfirmDiscardAsync())
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

            var promptContent = await PromptFilesService.GetPromptFileContentAsync(filename);
            EditorContent = promptContent ?? string.Empty;
            _loadedContent = EditorContent;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Erro ao obter o prompt: {File}", filename);
            ToastService.ShowError(L["EditPrompts.LoadFileError"]);
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
            ToastService.ShowWarning(L["EditPrompts.NoFileSelected"]);
            return;
        }

        try
        {
            await PromptFilesService.SavePromptFileAsync(_selectedPromptFile, EditorContent, CancellationToken.None);
            _loadedContent = EditorContent;
            ToastService.ShowSuccess(string.Format(L["EditPrompts.SaveSuccess"], _selectedPromptFile), timeout: 3000);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Erro ao gravar o prompt: {File}", _selectedPromptFile);
            ToastService.ShowError(L["EditPrompts.SaveError"]);
        }
    }

    private async Task OnCancel()
    {
        if (string.IsNullOrEmpty(_selectedPromptFile))
            return;

        if (!await ConfirmDiscardAsync())
            return;

        await LoadPromptFileContent(_selectedPromptFile);
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!HasUnsavedChanges)
            return true;

        var dialog = await DialogService.ShowConfirmationAsync(
            L["EditPrompts.DiscardConfirm"],
            L["EditPrompts.Discard"], L["Common.Cancel"], L["EditPrompts.DiscardTitle"]);

        var result = await dialog.Result;
        return !result.Cancelled;
    }

    public void Dispose()
    {
        _locationChangingRegistration?.Dispose();
    }
}
