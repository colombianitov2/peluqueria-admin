using System.Windows;
using PeluqueriaAdmin.App.ViewModels;

namespace PeluqueriaAdmin.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
