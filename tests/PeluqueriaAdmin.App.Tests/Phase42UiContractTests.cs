using System.IO;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase42UiContractTests
{
    [Fact]
    public void LocalUse_UsesDedicatedTwoActionScreenAndProfilesWithVirtualizedHistory()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "LocalUseView.xaml");
        string viewCode = Read("src", "PeluqueriaAdmin.App", "Views", "LocalUseView.xaml.cs");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "LocalUseViewModel.cs");
        string main = Read("src", "PeluqueriaAdmin.App", "ViewModels", "MainViewModel.cs");

        Assert.Contains("Text=\"Acción\"", view, StringComparison.Ordinal);
        Assert.Contains("[\"Añadir silla\", \"Añadir trabajador\"]", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Registrar pago\"]", viewModel.Split("ActionOptions", StringSplitOptions.None)[1].Split(';')[0], StringComparison.Ordinal);
        Assert.Contains("MouseDoubleClick=\"OnWorkerDoubleClick\"", view, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenSelectedWorkerProfileCommand}\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Abrir perfil seleccionado\"", view, StringComparison.Ordinal);
        Assert.Contains("SelectedWorkerRow, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged", view, StringComparison.Ordinal);
        Assert.Contains("OpenSelectedWorkerProfileCommand", viewCode, StringComparison.Ordinal);
        Assert.Contains("CloseWorkerProfileCommand", view, StringComparison.Ordinal);
        Assert.Contains("EnableRowVirtualization=\"True\"", view, StringComparison.Ordinal);
        Assert.Contains("WorkerHistoryRows", view, StringComparison.Ordinal);
        Assert.Contains("CurrentPage = LocalUse", main, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalUse_Phase44SeparatesChairSelectorsAndKeepsOnlyHistoryScrollable()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "LocalUseView.xaml");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "LocalUseViewModel.cs");
        int profileStart = view.IndexOf("x:Name=\"WorkerProfileHeader\"", StringComparison.Ordinal);
        int chairStart = view.IndexOf("IsChairProfileOpen", profileStart + 1, StringComparison.Ordinal);
        string workerProfile = view[profileStart..chairStart];

        Assert.Contains("NewWorkerChairOptions", view, StringComparison.Ordinal);
        Assert.Contains("SelectedNewWorkerChair", view, StringComparison.Ordinal);
        Assert.Contains("WorkerProfileChairOptions", view, StringComparison.Ordinal);
        Assert.Contains("WorkerProfileSelectedChair", view, StringComparison.Ordinal);
        Assert.DoesNotContain("AvailableChairOptions", view, StringComparison.Ordinal);
        Assert.DoesNotContain("AvailableChairOptions", viewModel, StringComparison.Ordinal);
        Assert.Contains("Silla inicial (opcional)", view, StringComparison.Ordinal);
        Assert.Contains("No hay sillas vacías", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Limpiar formulario", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer", workerProfile, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Auto\"", workerProfile, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(item => item.Date)", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalUse_Phase44ExposesAdvanceBalanceProjectionAndProtectedPaymentCommand()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "LocalUseView.xaml");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "LocalUseViewModel.cs");

        Assert.Contains("Deuda acumulada", view, StringComparison.Ordinal);
        Assert.Contains("Saldo a favor", view, StringComparison.Ordinal);
        Assert.Contains("Próxima cuota", view, StringComparison.Ordinal);
        Assert.Contains("Valor de la cuota", view, StringComparison.Ordinal);
        Assert.Contains("Próximo pago requerido", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Importe estimado que faltará", view, StringComparison.Ordinal);
        Assert.Contains("Cobertura estimada hasta", view, StringComparison.Ordinal);
        Assert.Contains("Puedes registrar pagos anticipados.", view, StringComparison.Ordinal);
        Assert.Contains("[RelayCommand]\n    private async Task RegisterWorkerPaymentAsync()", viewModel.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("AllowConcurrentExecutions = true", viewModel, StringComparison.Ordinal);
        Assert.Contains("Stopwatch.GetElapsedTime", viewModel, StringComparison.Ordinal);
        Assert.Contains("Pago registrado correctamente.", viewModel, StringComparison.Ordinal);
        Assert.Contains("Silla asignada correctamente: {chairName}", viewModel, StringComparison.Ordinal);
        Assert.Contains("Todo el historial", viewModel, StringComparison.Ordinal);
        Assert.Contains("HasRecoveredActionDraft", view, StringComparison.Ordinal);
        Assert.Contains("ActionDate, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Collaborators_OpenProfilesAndExposeConfirmedCapitalContributions()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "CollaboratorsView.xaml");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "CollaboratorsViewModel.cs");

        Assert.Contains("MouseDoubleClick=\"OnCollaboratorDoubleClick\"", view, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenSelectedProfileCommand}\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Abrir perfil seleccionado\"", view, StringComparison.Ordinal);
        Assert.Contains("SelectedCollaboratorRow, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged", view, StringComparison.Ordinal);
        Assert.Contains("Perfil del colaborador", view, StringComparison.Ordinal);
        Assert.Contains("Añadir aporte", view, StringComparison.Ordinal);
        Assert.Contains("SaveContributionCommand", view, StringComparison.Ordinal);
        Assert.Contains("ConfirmContributionDelete", view, StringComparison.Ordinal);
        Assert.Contains("Capital / inversión", viewModel, StringComparison.Ordinal);
        Assert.Contains("EnableRowVirtualization=\"True\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryCategoryAndSalesSearch_UseCorrectSelectorsAndImmediateDetails()
    {
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");

        Assert.Contains("UseSecondarySelector = false; SecondaryLabel = \"Categoría\"", viewModel, StringComparison.Ordinal);
        Assert.Contains("Alimento o bebida para venta", viewModel, StringComparison.Ordinal);
        Assert.Contains("Otro producto para venta", viewModel, StringComparison.Ordinal);
        Assert.Contains("Cortesía para clientes", viewModel, StringComparison.Ordinal);
        Assert.Contains("StringComparison.OrdinalIgnoreCase", viewModel, StringComparison.Ordinal);
        Assert.Contains("Existencia disponible", viewModel, StringComparison.Ordinal);
        Assert.Contains("Precio predeterminado", viewModel, StringComparison.Ordinal);
        Assert.Contains("QuantityText = string.Empty", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleProductAndWorkerSurfaces_DoNotContainLegacyTermsOrUnitField()
    {
        string[] files =
        [
            "src/PeluqueriaAdmin.App/Views/LocalUseView.xaml",
            "src/PeluqueriaAdmin.App/Views/AdministrationView.xaml",
            "src/PeluqueriaAdmin.App/ViewModels/LocalUseViewModel.cs",
            "src/PeluqueriaAdmin.App/ViewModels/AdministrationViewModel.cs",
            "src/PeluqueriaAdmin.Application/Administration/AdministrationService.cs",
            "src/PeluqueriaAdmin.Infrastructure/Exporting/ExcelExportService.cs",
            "docs/REQUISITOS_VIGENTES.md",
            "docs/MODELO_DATOS.md",
            "docs/MANUAL_USUARIO.md",
        ];
        string visible = string.Join('\n', files.Select(file => File.ReadAllText(Path.Combine(RepositoryFiles.Root, file))));

        Assert.DoesNotContain("peluquero", visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unidad de medida", visible, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] parts) =>
        RepositoryFiles.Read(parts);
}
