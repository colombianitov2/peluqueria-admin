using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PeluqueriaAdmin.App.Views;

public partial class ManualView : UserControl
{
    public ManualView()
    {
        InitializeComponent();
    }

    private void NavigateToSection(object sender, RequestNavigateEventArgs e)
    {
        string sectionName = e.Uri.OriginalString.TrimStart('#');
        if (ManualDocumentViewer.Document.FindName(sectionName) is FrameworkContentElement section)
        {
            section.BringIntoView();
        }

        e.Handled = true;
    }
}
