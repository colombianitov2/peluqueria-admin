using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase410InventoryUiTests
{
    [Fact]
    public void InventoryCurrent_RegistersOnlyProductsFromThePurchaseListAndEditsOneSelection()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        string viewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "InventoryViewModel.cs");

        Assert.Contains("Producto previamente añadido a la lista de compra", view, StringComparison.Ordinal);
        Assert.Contains("PendingMonthlyPurchaseRows", view, StringComparison.Ordinal);
        Assert.Contains("MonthlyPurchaseSearchText", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Cantidad comprada\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Precio de venta\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Descripción para inventario\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Fecha agregada\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Costo unitario real\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Total comprado\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Valor inventario actual\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Última actualización\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Editar selección\"", view, StringComparison.Ordinal);
        Assert.Contains("SaveInventorySelectionEditCommand", view, StringComparison.Ordinal);
        Assert.Contains("UpdateRegisteredMonthlyPurchaseAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("InventoryCurrentRow", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Agregar al inventario\">", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer", view, StringComparison.Ordinal);
    }

    [Fact]
    public void MovementTab_IsOnlyAReadOnlyHistory()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");

        Assert.Contains("Header=\"Movimientos\"", view, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding MovementHistory}\"", view, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MovementHistoryGrid\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Corregir movimiento seleccionado", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Variación de cantidad", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Movimiento de caja", view, StringComparison.Ordinal);
    }

    [Fact]
    public void PurchaseList_HasNoVisibleMonthAndCalculatesExpectedTotal()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        string viewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "InventoryViewModel.cs");

        Assert.Contains("Text=\"Producto\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Categoría\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Cantidad esperada\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Precio unitario o por paquete\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Precio total esperado\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Cantidad comprada\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Costo unitario real\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Total real\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Existencia actual\"", view, StringComparison.Ordinal);
        Assert.Contains("MonthlyPurchaseExpectedTotalText", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Mes\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Mes\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("MonthlyPurchaseMonth", viewModel, StringComparison.Ordinal);
        Assert.Contains("Content=\"Agregar\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Editar selección\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Guardar cambios\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Confirmo eliminar\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Eliminar\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryDrafts_KeepTheSelectedPlanAndThePurchaseListForm()
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
    public void PurchaseListSearch_IgnoresCaseAndAccents(string productName, string search)
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
            "2",
            "USD 7,25",
            "USD 14,50",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "Pendiente",
            string.Empty,
            true,
            string.Empty);

        Assert.True(InventoryViewModel.MatchesMonthlyPurchaseSearch(row, search));
    }
}
