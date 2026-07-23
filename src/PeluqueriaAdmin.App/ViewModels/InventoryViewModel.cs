using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Activity;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class InventoryViewModel(
    AdministrationViewModel editor,
    AdministrationService service,
    GetSettingsUseCase getSettings,
    TimeProvider timeProvider) : ObservableObject
{
    public AdministrationViewModel Editor { get; } = editor;
    public ObservableCollection<InventoryCurrentRow> CurrentInventory { get; } = [];
    public ObservableCollection<InventoryMovementRow> MovementHistory { get; } = [];
    public ObservableCollection<InventoryCurrentRow> RecentProducts { get; } = [];
    public ObservableCollection<MonthlyPurchaseRow> MonthlyPurchaseRows { get; } = [];
    public ObservableCollection<MonthlyPurchaseRow> PendingMonthlyPurchaseRows { get; } = [];
    public ObservableCollection<string> ProductCategoryOptions { get; } =
        ["Alimento o bebida para venta", "Otro producto para venta", "Cortesía para clientes",
         "Aseo", "Insumo del local", "Otro producto del local"];
    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Todos", "Rango personalizado"];

    [ObservableProperty] private string selectedPeriod = "Todos";
    [ObservableProperty] private DateTime? customPeriodFrom = DateTime.Today;
    [ObservableProperty] private DateTime? customPeriodThrough = DateTime.Today;
    [ObservableProperty] private bool showCustomPeriod;
    [ObservableProperty] private InventoryCurrentRow? selectedCurrentRow;
    [ObservableProperty] private InventoryMovementRow? selectedMovementRow;
    [ObservableProperty] private InventoryCurrentRow? selectedMonthlyPurchaseProduct;
    [ObservableProperty] private MonthlyPurchaseRow? selectedMonthlyPurchaseRow;
    [ObservableProperty] private MonthlyPurchaseRow? selectedPendingMonthlyPurchaseRow;
    [ObservableProperty] private string monthlyPurchaseName = string.Empty;
    [ObservableProperty] private string selectedMonthlyPurchaseCategory = "Otro producto del local";
    [ObservableProperty] private DateTime? monthlyPurchaseMonth = timeProvider.GetLocalNow().DateTime.Date;
    [ObservableProperty] private string monthlyPurchaseQuantity = string.Empty;
    [ObservableProperty] private string monthlyPurchaseUnitCost = string.Empty;
    [ObservableProperty] private bool monthlyPurchaseActive = true;
    [ObservableProperty] private bool monthlyPurchaseReserveAtZero;
    [ObservableProperty] private string monthlyPurchaseDescription = string.Empty;
    [ObservableProperty] private bool isEditingMonthlyPurchase;
    [ObservableProperty] private bool confirmMonthlyPurchaseDelete;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;

    public async Task LoadAsync()
    {
        await Editor.SelectModuleAsync(AdministrationViewModel.InventoryModule);
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            AdministrationData data = await service.LoadAsync();
            SettingsDto settings = await getSettings.ExecuteAsync();
            CurrentInventory.Clear();
            RecentProducts.Clear();
            var productRows = new List<InventoryCurrentRow>();
            foreach (Product product in data.Products.OrderBy(item => item.Name))
            {
                InventoryMovement[] movements = data.InventoryMovements.Where(item => item.ProductId == product.Id).ToArray();
                var row = new InventoryCurrentRow(
                    product,
                    product.Name,
                    SpanishText.For(product.Category),
                    InventoryCalculator.CurrentQuantity(movements).ToString("0.###", CultureInfo.CurrentCulture),
                    product.DefaultUnitCost.HasValue
                        ? $"{ApplicationCurrency.Code} {product.DefaultUnitCost.Value.ToDecimal():N2}"
                        : string.Empty,
                    product.DefaultSalePrice.HasValue ? $"{ApplicationCurrency.Code} {product.DefaultSalePrice.Value.ToDecimal():N2}" : string.Empty,
                    product.Description ?? string.Empty,
                    product.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                CurrentInventory.Add(row);
                productRows.Add(row);
            }
            foreach (InventoryCurrentRow row in productRows.OrderByDescending(item => item.Product.CreatedUtc).Take(20))
            {
                RecentProducts.Add(row);
            }

            MonthlyPurchaseRows.Clear();
            PendingMonthlyPurchaseRows.Clear();
            foreach (MonthlyPurchaseItem item in data.MonthlyPurchaseItems.OrderByDescending(item => item.Month.Year).ThenByDescending(item => item.Month.Month))
            {
                var row = new MonthlyPurchaseRow(item, item.Name,
                    SpanishText.For(item.Category), item.Month.ToString(),
                    item.Quantity.ToString("0.###", CultureInfo.CurrentCulture),
                    $"{ApplicationCurrency.Code} {item.ExpectedUnitCost.ToDecimal():N2}",
                    $"{ApplicationCurrency.Code} {Money.FromMinorUnits(item.ExpectedTotalMinorUnits).ToDecimal():N2}",
                    item.IsActive ? "Activa" : "Inactiva", item.ReserveWhenOutOfStock ? "Sí" : "No",
                    item.PurchaseMovementId.HasValue ? "Incorporada al inventario" : "Pendiente", item.Description ?? string.Empty);
                MonthlyPurchaseRows.Add(row);
                if (item.IsActive && !item.ProductId.HasValue && !item.PurchaseMovementId.HasValue)
                    PendingMonthlyPurchaseRows.Add(row);
            }

            ActivityDateRange? range = CurrentRange();
            MovementHistory.Clear();
            foreach (InventoryMovement movement in data.InventoryMovements
                .Where(item => !range.HasValue || range.Value.Contains(item.Date))
                .OrderByDescending(item => item.Date).ThenByDescending(item => item.CreatedUtc))
            {
                Product? product = data.Products.SingleOrDefault(item => item.Id == movement.ProductId);
                MovementHistory.Add(new InventoryMovementRow(
                    movement,
                    movement.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    MovementName(movement.Type),
                    product?.Name ?? "Producto eliminado",
                    movement.QuantityDelta > 0m ? movement.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture) : string.Empty,
                    movement.QuantityDelta < 0m ? Math.Abs(movement.QuantityDelta).ToString("0.###", CultureInfo.CurrentCulture) : string.Empty,
                    FormatUnitCost(movement, ApplicationCurrency.Code),
                    FormatTotalValue(movement, ApplicationCurrency.Code),
                    movement.Description ?? string.Empty));
            }

            StatusMessage = CurrentInventory.Count == 0 ? "Sin productos registrados." : string.Empty;
            IsError = false;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible cargar Inventario. {exception.Message}";
            IsError = true;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await Editor.SaveCommand.ExecuteAsync(null);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddMonthlyPurchaseAsync()
    {
        if (string.IsNullOrWhiteSpace(MonthlyPurchaseName) || !MonthlyPurchaseMonth.HasValue)
            throw new ArgumentException("Escribe el nombre y selecciona el mes de la lista.");
        if (!decimal.TryParse(MonthlyPurchaseQuantity, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal quantity)
            && !decimal.TryParse(MonthlyPurchaseQuantity, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity))
            throw new ArgumentException("La cantidad mensual no es válida.");
        if (!decimal.TryParse(MonthlyPurchaseUnitCost, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal cost)
            && !decimal.TryParse(MonthlyPurchaseUnitCost, NumberStyles.Number, CultureInfo.InvariantCulture, out cost))
            throw new ArgumentException("El costo esperado no es válido.");
        AdministrationData data = await service.LoadAsync();
        YearMonth month = YearMonth.From(DateOnly.FromDateTime(MonthlyPurchaseMonth.Value));
        Product? matchingProduct = data.Products.SingleOrDefault(item =>
            item.Name.Equals(MonthlyPurchaseName.Trim(), StringComparison.OrdinalIgnoreCase));
        var item = MonthlyPurchaseItem.Create(
            MonthlyPurchaseName,
            ParseProductCategory(SelectedMonthlyPurchaseCategory),
            month,
            quantity,
            Money.FromDecimal(cost), MonthlyPurchaseActive, MonthlyPurchaseReserveAtZero,
            timeProvider.GetUtcNow().UtcDateTime, MonthlyPurchaseDescription, matchingProduct?.Id);
        await service.AddMonthlyPurchaseItemAsync(item);
        ClearMonthlyPurchaseForm();
        StatusMessage = "El producto se agregó a la lista mensual de compra.";
        await RefreshAsync();
    }

    [RelayCommand]
    private void EditMonthlyPurchase()
    {
        if (SelectedMonthlyPurchaseRow is null)
            throw new InvalidOperationException("Selecciona un producto planificado.");
        MonthlyPurchaseItem item = SelectedMonthlyPurchaseRow.Item;
        MonthlyPurchaseName = item.Name;
        SelectedMonthlyPurchaseCategory = SpanishText.For(item.Category);
        MonthlyPurchaseMonth = item.Month.FirstDay.ToDateTime(TimeOnly.MinValue);
        MonthlyPurchaseQuantity = item.Quantity.ToString("0.###", CultureInfo.CurrentCulture);
        MonthlyPurchaseUnitCost = item.ExpectedUnitCost.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        MonthlyPurchaseActive = item.IsActive;
        MonthlyPurchaseReserveAtZero = item.ReserveWhenOutOfStock;
        MonthlyPurchaseDescription = item.Description ?? string.Empty;
        IsEditingMonthlyPurchase = true;
    }

    [RelayCommand]
    private async Task SaveMonthlyPurchaseEditAsync()
    {
        if (!IsEditingMonthlyPurchase || SelectedMonthlyPurchaseRow is null || !MonthlyPurchaseMonth.HasValue)
            throw new InvalidOperationException("Primero selecciona Editar producto planificado.");
        MonthlyPurchaseItem item = SelectedMonthlyPurchaseRow.Item;
        item.Update(
            MonthlyPurchaseName,
            ParseProductCategory(SelectedMonthlyPurchaseCategory),
            YearMonth.From(DateOnly.FromDateTime(MonthlyPurchaseMonth.Value)),
            ParsePositiveDecimal(MonthlyPurchaseQuantity, "cantidad mensual"),
            Money.FromDecimal(ParsePositiveDecimal(MonthlyPurchaseUnitCost, "costo esperado")),
            MonthlyPurchaseActive,
            MonthlyPurchaseReserveAtZero,
            timeProvider.GetUtcNow().UtcDateTime,
            MonthlyPurchaseDescription);
        await service.UpdateMonthlyPurchaseItemAsync(item);
        ClearMonthlyPurchaseForm();
        StatusMessage = "El producto planificado se actualizó y conserva su historial.";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteMonthlyPurchaseAsync()
    {
        if (SelectedMonthlyPurchaseRow is null || !ConfirmMonthlyPurchaseDelete)
            throw new InvalidOperationException("Selecciona un producto planificado y confirma la eliminación.");
        await service.DeleteAsync(SelectedMonthlyPurchaseRow.Item);
        ClearMonthlyPurchaseForm();
        StatusMessage = "El producto planificado se eliminó lógicamente y conserva su historial.";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ToggleMonthlyPurchaseAsync()
    {
        if (SelectedMonthlyPurchaseRow is null) throw new InvalidOperationException("Selecciona un artículo mensual.");
        MonthlyPurchaseItem item = SelectedMonthlyPurchaseRow.Item;
        item.Update(item.Quantity, item.ExpectedUnitCost, !item.IsActive, item.ReserveWhenOutOfStock,
            timeProvider.GetUtcNow().UtcDateTime, item.Description);
        await service.UpdateMonthlyPurchaseItemAsync(item);
        StatusMessage = item.IsActive ? "El artículo mensual quedó activo." : "El artículo mensual quedó inactivo; su historial se conserva.";
        await RefreshAsync();
    }

    [RelayCommand]
    private void LoadSelected()
    {
        if (Editor.SelectedRow is null)
        {
            StatusMessage = "Selecciona un producto o movimiento para editar.";
            IsError = true;
            return;
        }
        Editor.LoadSelectedCommand.Execute(null);
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        await Editor.SaveEditCommand.ExecuteAsync(null);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await Editor.DeleteCommand.ExecuteAsync(null);
        await RefreshAsync();
    }

    public Task FlushPendingAsync() => Editor.FlushPendingAsync();

    partial void OnSelectedCurrentRowChanged(InventoryCurrentRow? value)
    {
        SelectedMovementRow = null;
        SelectEditorEntity(value?.Product);
        if (value is not null) Editor.LoadSelectedCommand.Execute(null);
    }
    partial void OnSelectedMovementRowChanged(InventoryMovementRow? value)
    {
        if (value is null) return;
        SelectedCurrentRow = null;
        SelectEditorEntity(value.Movement);
    }
    partial void OnSelectedMonthlyPurchaseProductChanged(InventoryCurrentRow? value)
    {
        if (value is null) return;
        MonthlyPurchaseName = value.Product.Name;
        SelectedMonthlyPurchaseCategory = SpanishText.For(value.Product.Category);
        MonthlyPurchaseReserveAtZero = value.Product.Category is ProductCategory.Cleaning or ProductCategory.LocalSupply;
        MonthlyPurchaseUnitCost = value.Product.DefaultUnitCost?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty;
    }
    partial void OnSelectedMonthlyPurchaseRowChanged(MonthlyPurchaseRow? value)
    {
        if (value is not null) ConfirmMonthlyPurchaseDelete = false;
    }
    partial void OnSelectedPendingMonthlyPurchaseRowChanged(MonthlyPurchaseRow? value)
    {
        Editor.SelectedMonthlyPlanId = value?.Item.Id;
        if (value is null) return;
        Editor.SelectedAction = "Agregar producto";
        Editor.PrimaryText = value.Item.Name;
        Editor.SecondaryText = SpanishText.For(value.Item.Category);
        Editor.AmountText = value.Item.ExpectedUnitCost.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        Editor.QuantityText = value.Item.Quantity.ToString("0.###", CultureInfo.CurrentCulture);
        Editor.OptionalDescriptionText = value.Item.Description ?? string.Empty;
    }
    partial void OnSelectedPeriodChanged(string value) { ShowCustomPeriod = value == "Rango personalizado"; _ = RefreshAsync(); }
    partial void OnCustomPeriodFromChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }
    partial void OnCustomPeriodThroughChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }

    private void SelectEditorEntity(AuditableEntity? entity) => Editor.SelectedRow = entity is null
        ? null
        : new OperationRow(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, entity);

    private ActivityDateRange? CurrentRange()
    {
        if (SelectedPeriod == "Todos") return null;
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        ActivityPeriod period = SelectedPeriod switch
        {
            "Esta semana" => ActivityPeriod.ThisWeek,
            "Este mes" => ActivityPeriod.ThisMonth,
            "Últimos 3 meses" => ActivityPeriod.LastThreeMonths,
            "Últimos 6 meses" => ActivityPeriod.LastSixMonths,
            "Este año" => ActivityPeriod.ThisYear,
            "Rango personalizado" => ActivityPeriod.Custom,
            _ => ActivityPeriod.Today,
        };
        return ActivityPeriodCalculator.Calculate(period, today,
            CustomPeriodFrom.HasValue ? DateOnly.FromDateTime(CustomPeriodFrom.Value) : null,
            CustomPeriodThrough.HasValue ? DateOnly.FromDateTime(CustomPeriodThrough.Value) : null);
    }

    private static string MovementName(InventoryMovementType type) => type switch
    {
        InventoryMovementType.InitialStock => "Existencia inicial",
        InventoryMovementType.Purchase => "Compra",
        InventoryMovementType.Sale => "Venta",
        InventoryMovementType.InternalConsumption => "Consumo interno",
        InventoryMovementType.PhysicalCountAdjustment => "Ajuste por conteo físico",
        _ => type.ToString(),
    };

    private static string FormatUnitCost(InventoryMovement movement, string currencyCode)
    {
        if (!movement.EstimatedCost.HasValue || movement.QuantityDelta == 0m) return string.Empty;
        decimal unitCost = movement.EstimatedCost.Value.ToDecimal() / Math.Abs(movement.QuantityDelta);
        return $"{currencyCode} {unitCost:N2}";
    }

    private static string FormatTotalValue(InventoryMovement movement, string currencyCode)
    {
        Money? total = movement.CashAmount ?? movement.EstimatedCost;
        return total.HasValue ? $"{currencyCode} {total.Value.ToDecimal():N2}" : string.Empty;
    }

    private static decimal ParsePositiveDecimal(string value, string field)
    {
        bool valid = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        return valid && amount > 0 ? amount : throw new ArgumentException($"La {field} debe ser mayor que cero.");
    }

    private static ProductCategory ParseProductCategory(string value) => value switch
    {
        "Alimento o bebida para venta" => ProductCategory.FoodOrDrinkForSale,
        "Otro producto para venta" => ProductCategory.OtherProductForSale,
        "Cortesía para clientes" => ProductCategory.CustomerCourtesy,
        "Aseo" => ProductCategory.Cleaning,
        "Insumo del local" => ProductCategory.LocalSupply,
        _ => ProductCategory.OtherLocalProduct,
    };

    private void ClearMonthlyPurchaseForm()
    {
        MonthlyPurchaseName = MonthlyPurchaseQuantity = MonthlyPurchaseUnitCost =
            MonthlyPurchaseDescription = string.Empty;
        SelectedMonthlyPurchaseCategory = "Otro producto del local";
        MonthlyPurchaseMonth = timeProvider.GetLocalNow().DateTime.Date;
        MonthlyPurchaseActive = true;
        MonthlyPurchaseReserveAtZero = false;
        SelectedMonthlyPurchaseRow = null;
        ConfirmMonthlyPurchaseDelete = false;
        IsEditingMonthlyPurchase = false;
    }
}

public sealed record InventoryCurrentRow(Product Product, string Name, string Category, string CurrentQuantity,
    string AverageUnitCost, string DefaultSalePrice, string Description, string LastUpdate);
public sealed record InventoryMovementRow(InventoryMovement Movement, string Date, string Type, string Product,
    string QuantityIn, string QuantityOut, string UnitCost, string TotalValue, string Description);
public sealed record MonthlyPurchaseRow(MonthlyPurchaseItem Item, string Product, string Category, string Month,
    string Quantity, string UnitCost, string Total, string Active, string ReserveAtZero, string State, string Description);
