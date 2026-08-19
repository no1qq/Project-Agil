using ProjectAgil.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.Views.Pages;

public partial class ToolsPage : INavigableView<ToolsViewModel>
{
    public ToolsPage(ToolsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public ToolsViewModel ViewModel { get; }
}
