using ProjectAgil.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.Views.Pages;

public partial class OptimizePage : INavigableView<OptimizeViewModel>
{
    public OptimizePage(OptimizeViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public OptimizeViewModel ViewModel { get; }
}
