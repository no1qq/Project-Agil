using ProjectAgil.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.Views.Pages;

public partial class AdaptersPage : INavigableView<AdaptersViewModel>
{
    public AdaptersPage(AdaptersViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public AdaptersViewModel ViewModel { get; }
}
