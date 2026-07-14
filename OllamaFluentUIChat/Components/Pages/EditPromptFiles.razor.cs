namespace OllamaFluentUIChat.Components.Pages;

public partial class EditPromptFiles
{

    private string _editorContent;
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
    protected CancellationToken _cancellationToken;

    private bool _isLoading = true;
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _isLoading = true;
            await GetSystemPrompt();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading system prompt");
        }
        finally
        {
            _isLoading = false; // Desliga o carregamento e renderiza o TextArea em segurança
        }
    }
    private async Task GetSystemPrompt()
    {
        try
        {
            var promptContent = await SystemPromptService.GetSystemPrompt(_cancellationToken);
            EditorContent = promptContent;

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Erro ao obter system prompt");
        }
    }
    private async Task OnSave()
    {
        try
        {
            await PromptFilesService.SavePromptFileAsync("system-prompt.txt", EditorContent, CancellationToken.None);
            await GetSystemPrompt();

            ToastService.ShowSuccess("O prompt do sistema foi gravado com sucesso!", timeout: 3000);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in saving prompt");
            ToastService.ShowError("Não foi possível gravar o prompt.");
        }
    }

    private async Task OnCancel()
    {
        EditorContent = await SystemPromptService.GetSystemPrompt(_cancellationToken = default);
        StateHasChanged();
    }

}