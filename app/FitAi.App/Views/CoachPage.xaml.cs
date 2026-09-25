using FitAi.App.ViewModels;

namespace FitAi.App.Views;

public partial class CoachPage : ContentPage
{
    private readonly CoachViewModel _viewModel;

    public CoachPage(CoachViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppearingCommand.Execute(null);
    }
}
