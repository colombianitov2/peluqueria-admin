using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase410InventoryUiTests
{
    [Fact]
    public void AddInventory_UsesOneSearchableMonthlyPurchaseFlowAndOnlyRequestedFields()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        string viewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "InventoryViewModel.cs");

        Assert.DoesNotContain("Text=\"Operación\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Editor.ActionOptions", view, StringComparison.Ordinal);
        Assert.Contains("Buscar producto en la lista mensual de compra", view, StringComparison.Ordinal);
        Assert.Contains("PendingMonthlyPurchaseRows", view, StringComparison.Ordinal);
        Assert.Contains("MonthlyPurchaseSearchText", view, StringComparison.Ordinal);
        Assert.Contains("IsTextSearchEnabled=\"False\"", view, StringComparison.Ordinal);
        Assert.Contains("TextSearch.TextPath=\"Product\"", view, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Buscar producto en la lista mensual de compra\"",
            view, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", view, StringComparison.Ordinal);
        Assert.Contains("M 7,1 A 6,6", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Fecha de compra\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Cantidad comprada\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Precio de venta por unidad o paquete\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Descripción\"", view, StringComparison.Ordinal);
        Assert.Contains("RegisterMonthlyPurchaseCommand", view, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)",
            viewModel, StringComparison.Ordinal);
        Assert.Contains("if (!item.PurchaseMovementId.HasValue)", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("!item.ProductId.HasValue && !item.PurchaseMovementId.HasValue",
            viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void MonthlyPurchaseList_HasOnlyAddEditSaveConfirmationAndDeleteActions()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        string viewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "InventoryViewModel.cs");

        Assert.Contains("Content=\"Agregar\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Editar\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Guardar\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Confirmo eliminar\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Eliminar\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Activar o desactivar", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content=\"Activa\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Activa\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Reservar cuando", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Header=\"Reserva", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleMonthlyPurchase", viewModel, StringComparison.Ordinal);
        Assert.Contains("Money.FromDecimal(cost), true, false", viewModel, StringComparison.Ordinal);
        Assert.Contains("item.IsActive", viewModel, StringComparison.Ordinal);
        Assert.Contains("item.ReserveWhenOutOfStock", viewModel, StringComparison.Ordinal);
        Assert.Contains("CanEditMonthlyPurchase", viewModel, StringComparison.Ordinal);
        Assert.Contains("conserva su fotografía histórica", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingProductsAndMovements_KeepVisibleEditSaveAndDeleteActions()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");

        Assert.Contains("Text=\"Editar producto seleccionado\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Corregir movimiento seleccionado\"", view, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LoadSelectedCommand}\"", view, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SaveEditCommand}\"", view, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DeleteCommand}\"", view, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding Editor.ConfirmDelete}\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryDrafts_IncludeTheSelectedPlanAndTheMonthlyListForm()
    {
        string editor = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");
        string inventory = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "InventoryViewModel.cs");

        Assert.Contains("SelectedMonthlyPlanId));", editor, StringComparison.Ordinal);
        Assert.Contains("SelectedMonthlyPlanId = payload.SelectedMonthlyPlanId", editor, StringComparison.Ordinal);
        Assert.Contains("MonthlyListDraftKey", inventory, StringComparison.Ordinal);
        Assert.Contains("PersistMonthlyListDraftAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("RestoreMonthlyListDraftAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("EditedItemId", inventory, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Tinte cobrÉ", "TINTE")]
    [InlineData("Tinte cobrÉ", "cobre")]
    [InlineData("Tinte cobrÉ", "COBRÉ")]
    public void MonthlyPurchaseSearch_IgnoresCaseAndAccents(string productName, string search)
    {
        MonthlyPurchaseItem item = MonthlyPurchaseItem.Create(
            productName,
            ProductCategory.OtherProductForSale,
            new YearMonth(2026, 8),
            2,
            Money.FromDecimal(7.25m),
            true,
            false,
            new DateTime(2026, 7, 23, 14, 0, 0, DateTimeKind.Utc));
        var row = new MonthlyPurchaseRow(
            item,
            item.Name,
            "Otro producto para venta",
            item.Month.ToString(),
            "2",
            "USD 7,25",
            "USD 14,50",
            "Pendiente",
            string.Empty,
            true,
            string.Empty);

        Assert.True(InventoryViewModel.MatchesMonthlyPurchaseSearch(row, search));
    }
}
