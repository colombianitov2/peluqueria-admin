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
    public ObservableCollection<RestockPlanRow> RestockPlans { get; } = [];
    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Todos", "Rango personalizado"];

    [ObservableProperty] private string selectedPeriod = "Todos";
    [ObservableProperty] private DateTime? customPeriodFrom = DateTime.Today;
    [ObservableProperty] private DateTime? customPeriodThrough = DateTime.Today;
    [ObservableProperty] private bool showCustomPeriod;
    [ObservableProperty] private InventoryCurrentRow? selectedCurrentRow;
    [ObservableProperty] private InventoryMovementRow? selectedMovementRow;
    [ObservableProperty] private RestockPlanRow? selectedPlanRow;
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
            foreach (Product product in data.Products.OrderBy(item => item.Name))
            {
                InventoryMovement[] movements = data.InventoryMovements.Where(item => item.ProductId == product.Id).ToArray();
                CurrentInventory.Add(new InventoryCurrentRow(
                    product,
                    product.Name,
                    SpanishText.For(product.Category),
                    InventoryCalculator.CurrentQuantity(movements).ToString("0.###", CultureInfo.CurrentCulture),
                    product.DefaultUnitCost.HasValue
                        ? $"{ApplicationCurrency.Code} {product.DefaultUnitCost.Value.ToDecimal():N2}"
                        : string.Empty,
                    product.DefaultSalePrice.HasValue ? $"{ApplicationCurrency.Code} {product.DefaultSalePrice.Value.ToDecimal():N2}" : string.Empty,
                    product.Description ?? string.Empty,
                    product.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
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

            RestockPlans.Clear();
            foreach (MonthlyRestockPlan plan in data.RestockPlans.OrderByDescending(item => item.Month.Year).ThenByDescending(item => item.Month.Month))
            {
                Product? product = data.Products.SingleOrDefault(item => item.Id == plan.ProductId);
                decimal available = InventoryCalculator.CurrentQuantity(data.InventoryMovements.Where(item => item.ProductId == plan.ProductId));
                RestockPlans.Add(new RestockPlanRow(
                    plan,
                    plan.Month.ToString(),
                    product?.Name ?? "Producto eliminado",
                    plan.NeededQuantity.Value.ToString("0.###", CultureInfo.CurrentCulture),
                    plan.SuggestedPurchase(available).ToString("0.###", CultureInfo.CurrentCulture)));
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
    private void LoadSelected()
    {
        if (Editor.SelectedRow is null)
        {
            StatusMessage = "Selecciona un producto, movimiento o plan para editar.";
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
        SelectedPlanRow = null;
        SelectEditorEntity(value?.Product);
        if (value is not null) Editor.LoadSelectedCommand.Execute(null);
    }
    partial void OnSelectedMovementRowChanged(InventoryMovementRow? value)
    {
        if (value is null) return;
        SelectedCurrentRow = null;
        SelectedPlanRow = null;
        SelectEditorEntity(value.Movement);
    }
    partial void OnSelectedPlanRowChanged(RestockPlanRow? value)
    {
        if (value is null) return;
        SelectedCurrentRow = null;
        SelectedMovementRow = null;
        SelectEditorEntity(value.Plan);
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
}

public sealed record InventoryCurrentRow(Product Product, string Name, string Category, string CurrentQuantity,
    string AverageUnitCost, string DefaultSalePrice, string Description, string LastUpdate);
public sealed record InventoryMovementRow(InventoryMovement Movement, string Date, string Type, string Product,
    string QuantityIn, string QuantityOut, string UnitCost, string TotalValue, string Description);
public sealed record RestockPlanRow(MonthlyRestockPlan Plan, string Month, string Product, string NeededQuantity, string SuggestedPurchase);
