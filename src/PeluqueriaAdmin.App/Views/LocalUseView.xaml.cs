using System.Windows.Controls;
using System.Windows.Input;
using PeluqueriaAdmin.App.ViewModels;

namespace PeluqueriaAdmin.App.Views;

public partial class LocalUseView : UserControl
{
    public LocalUseView()
    {
        InitializeComponent();
    }

    private void OnWorkerDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LocalUseViewModel viewModel && viewModel.SelectedWorkerRow is not null)
        {
            viewModel.OpenSelectedWorkerProfileCommand.Execute(null);
            e.Handled = true;
        }
    }
}
