using System.Windows.Controls;
using Microsoft.Win32;
using PeluqueriaAdmin.App.ViewModels;

namespace PeluqueriaAdmin.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SelectBackup_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar copia de seguridad",
            Filter = "Copia SQLite (*.db)|*.db|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true && DataContext is SettingsViewModel viewModel)
        {
            viewModel.RestorePath = dialog.FileName;
        }
    }

    private void SelectExportFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta de exportación",
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true && DataContext is SettingsViewModel viewModel)
        {
            viewModel.ExportDirectory = dialog.FolderName;
        }
    }
}
