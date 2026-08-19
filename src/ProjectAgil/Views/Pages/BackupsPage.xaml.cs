using ProjectAgil.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.Views.Pages;

public partial class BackupsPage : INavigableView<BackupsViewModel>
{
    public BackupsPage(BackupsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public BackupsViewModel ViewModel { get; }
}
