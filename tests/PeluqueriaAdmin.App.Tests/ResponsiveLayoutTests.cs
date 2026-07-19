using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PeluqueriaAdmin.App.Views;

namespace PeluqueriaAdmin.App.Tests;

public sealed class ResponsiveLayoutTests
{
    [Fact]
    public void AdministrationAndSettings_FitAt1366x768EquivalentFor100125And150Percent()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new PeluqueriaAdmin.App.App();
                app.InitializeComponent();
                foreach ((double scale, double width, double height) in new[]
                {
                    (1.00, 1_136d, 768d),
                    (1.25, 863d, 614d),
                    (1.50, 681d, 512d),
                })
                {
                    var administration = new AdministrationView
                    {
                        DataContext = new LayoutContext(),
                    };
                    AssertFits(administration, width, height, scale);

                    var settings = new SettingsView
                    {
                        DataContext = new LayoutContext(),
                    };
                    AssertFits(settings, width, height, scale);

                    var localUse = new LocalUseView { DataContext = new LayoutContext() };
                    AssertFits(localUse, width, height, scale);
                    var localUseProfile = new LocalUseView
                    {
                        DataContext = new LayoutContext { IsWorkerProfileOpen = true },
                    };
                    AssertFits(localUseProfile, width, height, scale);

                    var collaborators = new CollaboratorsView { DataContext = new LayoutContext() };
                    AssertFits(collaborators, width, height, scale);
                    var collaboratorProfile = new CollaboratorsView
                    {
                        DataContext = new LayoutContext { IsProfileOpen = true },
                    };
                    AssertFits(collaboratorProfile, width, height, scale);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static void AssertFits(FrameworkElement element, double width, double height, double scale)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        Assert.True(element.ActualWidth <= width + 0.5, $"Ancho excedido a {scale:P0}.");
        Assert.True(element.ActualHeight <= height + 0.5, $"Alto excedido a {scale:P0}.");
        AssertNoInvalidBounds(element, element, scale);
    }

    private static void AssertNoInvalidBounds(Visual root, DependencyObject current, double scale)
    {
        int children = VisualTreeHelper.GetChildrenCount(current);
        for (int index = 0; index < children; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(current, index);
            if (child is FrameworkElement framework && framework.Visibility == Visibility.Visible)
            {
                Rect bounds = framework.TransformToAncestor(root).TransformBounds(
                    new Rect(0, 0, framework.ActualWidth, framework.ActualHeight));
                Assert.False(double.IsNaN(bounds.Width) || double.IsInfinity(bounds.Width), $"Control inválido a {scale:P0}.");
                Assert.True(bounds.Width >= 0 && bounds.Height >= 0, $"Control con tamaño negativo a {scale:P0}.");
            }
            AssertNoInvalidBounds(root, child, scale);
        }
    }

    private sealed class LayoutContext
    {
        public string Title => "Mantenimiento";
        public string Description => "Descripción de validación visual en una escala aumentada.";
        public ObservableCollection<object> Rows { get; } = [];
        public ObservableCollection<string> ActionOptions { get; } = ["Agregar mantenimiento"];
        public ObservableCollection<string> PrimaryOptions { get; } = [];
        public ObservableCollection<string> SecondaryOptions { get; } = [];
        public ObservableCollection<string> ExtraOptions { get; } = [];
        public string SelectedAction { get; set; } = "Agregar mantenimiento";
        public object? SelectedRow { get; set; }
        public string PrimaryLabel => "Equipo o bien";
        public string SecondaryLabel => "Tipo de mantenimiento";
        public string ExtraLabel => "Unidad de medida";
        public string DateLabel => "Fecha programada";
        public string EndDateLabel => "Fecha realizada (opcional)";
        public string AmountLabel => "Costo estimado (opcional)";
        public string SecondaryAmountLabel => "Costo real (opcional)";
        public string QuantityLabel => "Cantidad";
        public bool ShowPrimary => true;
        public bool ShowSecondary => true;
        public bool ShowExtra => true;
        public bool ShowDate => true;
        public bool ShowEndDate => true;
        public bool ShowAmount => true;
        public bool ShowSecondaryAmount => true;
        public bool ShowQuantity => true;
        public bool ShowCommitAction => true;
        public bool ShowActionSelector => true;
        public bool ShowRecordActions => true;
        public bool HasRecoveredDraft => true;
        public bool HasStatusMessage => true;
        public bool IsError => false;
        public bool ConfirmDelete { get; set; }
        public bool UpdateReady => false;
        public string StatusMessage => "Borrador recuperado para validar texto largo.";
        public DateTime? FormDate { get; set; } = DateTime.Today;
        public DateTime? FormEndDate { get; set; }
        public string PrimaryText { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public string ExtraText { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;
        public string SecondaryAmountText { get; set; } = string.Empty;
        public string QuantityText { get; set; } = string.Empty;
        public string WeeklyUsageFee { get; set; } = "12000,00";
        public string CollaboratorProfitPercent { get; set; } = "20,00";
        public string OptionalSuppliesMonthlyBudget { get; set; } = "100000,00";
        public string TotalChairs { get; set; } = "10";
        public string CurrencyCode { get; set; } = "COP";
        public string RestorePath { get; set; } = string.Empty;
        public bool IsWorkerProfileOpen { get; set; }
        public bool IsProfileOpen { get; set; }
    }
}
