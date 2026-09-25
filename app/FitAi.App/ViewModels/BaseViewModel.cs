using CommunityToolkit.Mvvm.ComponentModel;
using FitAi.App.Services;

namespace FitAi.App.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => ErrorMessage is not null;

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>Executa uma ação mostrando carregamento e convertendo erros em mensagem.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try { await action(); }
        catch (Exception e) { ErrorMessage = Format.ErrorMessage(e); }
        finally { IsBusy = false; }
    }
}
