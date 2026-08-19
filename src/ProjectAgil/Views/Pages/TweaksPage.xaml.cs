using ProjectAgil.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.Views.Pages;

public partial class TweaksPage : INavigableView<TweaksViewModel>
{
    public TweaksPage(TweaksViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public TweaksViewModel ViewModel { get; }
}
