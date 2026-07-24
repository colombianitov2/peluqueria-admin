using System.Windows;
using System.Windows.Controls;
using PeluqueriaAdmin.App.ViewModels;

namespace PeluqueriaAdmin.App.Views;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
    }

    private async void OnLostKeyboardFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesViewModel viewModel)
        {
            await viewModel.FlushPendingAsync();
        }
    }
}
