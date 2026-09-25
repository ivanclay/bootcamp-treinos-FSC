using FitAi.App.ViewModels;

namespace FitAi.App.Views;

public partial class WorkoutDayPage : ContentPage
{
    private readonly WorkoutDayViewModel _viewModel;

    public WorkoutDayPage(WorkoutDayViewModel viewModel)
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
