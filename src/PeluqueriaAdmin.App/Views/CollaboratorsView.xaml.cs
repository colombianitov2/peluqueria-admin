using System.Windows.Controls;
using System.Windows.Input;
using PeluqueriaAdmin.App.ViewModels;

namespace PeluqueriaAdmin.App.Views;

public partial class CollaboratorsView : UserControl
{
    public CollaboratorsView()
    {
        InitializeComponent();
    }

    private void OnCollaboratorDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CollaboratorsViewModel viewModel && viewModel.SelectedCollaboratorRow is not null)
        {
            viewModel.OpenSelectedProfileCommand.Execute(null);
            e.Handled = true;
        }
    }
}
