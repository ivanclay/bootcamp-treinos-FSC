using FitAi.App.ViewModels;

namespace FitAi.App.Views;

public partial class PlanPage : ContentPage
{
    private readonly PlanViewModel _viewModel;

    public PlanPage(PlanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
