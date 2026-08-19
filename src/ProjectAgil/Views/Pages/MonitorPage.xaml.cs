using ProjectAgil.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace ProjectAgil.Views.Pages;

public partial class MonitorPage : INavigableView<MonitorViewModel>
{
    public MonitorPage(MonitorViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public MonitorViewModel ViewModel { get; }
}
