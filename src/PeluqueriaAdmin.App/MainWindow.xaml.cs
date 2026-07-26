using System.ComponentModel;
using System.Windows;
using PeluqueriaAdmin.App.ViewModels;

namespace PeluqueriaAdmin.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private bool closingAfterFlush;

    public MainWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closingAfterFlush) return;
        e.Cancel = true;
        try
        {
            await viewModel.FlushPendingAsync();
        }
        finally
        {
            closingAfterFlush = true;
            Close();
        }
    }
}
