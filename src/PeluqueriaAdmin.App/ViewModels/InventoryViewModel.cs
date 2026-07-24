using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Activity;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class InventoryViewModel(
    AdministrationViewModel editor,
    AdministrationService service,
    GetSettingsUseCase getSettings,
    TimeProvider timeProvider,
    IFormDraftStore? formDraftStore = null) : ObservableObject
{
    private const string MonthlyPurchaseDraftKey = "Inventario:Registrar compra:new";
    private const string MonthlyListDraftKey = "Inventario:Lista mensual de compra:new";
    private readonly List<MonthlyPurchaseRow> allPendingMonthlyPurchaseRows = [];
    private readonly SemaphoreSlim monthlyListDraftLock = new(1, 1);
    private bool suppressMonthlyPurchaseSearch;
    private bool suppressPendingPurchaseSelection;
    private bool preserveRecoveredPurchaseFields;
    private bool suppressMonthlyListDraft;
    private CancellationTokenSource? monthlyListDraftCancellation;

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
    [ObservableProperty] private string monthlyPurchaseSearchText = string.Empty;
    [ObservableProperty] private bool isMonthlyPurchaseDropDownOpen;
    [ObservableProperty] private bool hasNoPendingMonthlyPurchases;
    [ObservableProperty] private string selectedMonthlyPurchaseCostText = "Selecciona un producto de la lista mensual.";
    [ObservableProperty] private string monthlyPurchaseName = string.Empty;
    [ObservableProperty] private string selectedMonthlyPurchaseCategory = "Otro producto del local";
    [ObservableProperty] private DateTime? monthlyPurchaseMonth = timeProvider.GetLocalNow().DateTime.Date;
    [ObservableProperty] private string monthlyPurchaseQuantity = string.Empty;
    [ObservableProperty] private string monthlyPurchaseUnitCost = string.Empty;
    [ObservableProperty] private string monthlyPurchaseDescription = string.Empty;
    [ObservableProperty] private bool isEditingMonthlyPurchase;
    [ObservableProperty] private bool confirmMonthlyPurchaseDelete;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;

    public async Task LoadAsync()
    {
        await Editor.SelectModuleAsync(AdministrationViewModel.InventoryModule);
        Editor.SelectedAction = "Registrar compra";
        await RefreshAsync();
        await RestoreMonthlyListDraftAsync();
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

            Guid? selectedPendingPlanId = Editor.SelectedMonthlyPlanId;
            bool preservePurchaseFields = Editor.HasRecoveredDraft;
            suppressPendingPurchaseSelection = true;
            SelectedPendingMonthlyPurchaseRow = null;
            MonthlyPurchaseRows.Clear();
            PendingMonthlyPurchaseRows.Clear();
            allPendingMonthlyPurchaseRows.Clear();
            foreach (MonthlyPurchaseItem item in data.MonthlyPurchaseItems.OrderByDescending(item => item.Month.Year).ThenByDescending(item => item.Month.Month))
            {
                Product? linkedProduct = item.ProductId.HasValue
                    ? data.Products.SingleOrDefault(product => product.Id == item.ProductId.Value)
                    : null;
                bool requiresSalePrice = linkedProduct?.IsForSale
                    ?? item.Category is ProductCategory.FoodOrDrinkForSale or ProductCategory.OtherProductForSale;
                var row = new MonthlyPurchaseRow(item, item.Name,
                    SpanishText.For(item.Category), item.Month.ToString(),
                    item.Quantity.ToString("0.###", CultureInfo.CurrentCulture),
                    $"{ApplicationCurrency.Code} {item.ExpectedUnitCost.ToDecimal():N2}",
                    $"{ApplicationCurrency.Code} {Money.FromMinorUnits(item.ExpectedTotalMinorUnits).ToDecimal():N2}",
                    item.PurchaseMovementId.HasValue ? "Compra registrada" : "Pendiente",
                    item.Description ?? string.Empty,
                    requiresSalePrice,
                    linkedProduct?.DefaultSalePrice?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty);
                MonthlyPurchaseRows.Add(row);
                if (!item.PurchaseMovementId.HasValue)
                    allPendingMonthlyPurchaseRows.Add(row);
            }
            ApplyMonthlyPurchaseFilter();
            suppressPendingPurchaseSelection = false;
            MonthlyPurchaseRow? selectedPendingPlan = selectedPendingPlanId.HasValue
                ? allPendingMonthlyPurchaseRows.SingleOrDefault(
                    item => item.Item.Id == selectedPendingPlanId.Value)
                : null;
            if (selectedPendingPlan is not null)
            {
                preserveRecoveredPurchaseFields = preservePurchaseFields;
                SelectedPendingMonthlyPurchaseRow = selectedPendingPlan;
                preserveRecoveredPurchaseFields = false;
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
            suppressPendingPurchaseSelection = false;
            preserveRecoveredPurchaseFields = false;
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
        try
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
                Money.FromDecimal(cost), true, false,
                timeProvider.GetUtcNow().UtcDateTime, MonthlyPurchaseDescription, matchingProduct?.Id);
            await service.AddMonthlyPurchaseItemAsync(item);
            ClearMonthlyPurchaseForm();
            await DeleteMonthlyListDraftAsync();
            await RefreshAsync();
            StatusMessage = "El producto se agregó a la lista mensual de compra.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible agregar el producto a la lista mensual. {exception.Message}";
            IsError = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditMonthlyPurchase))]
    private void EditMonthlyPurchase()
    {
        if (SelectedMonthlyPurchaseRow is null)
        {
            StatusMessage = "Selecciona un producto planificado.";
            IsError = true;
            return;
        }

        if (SelectedMonthlyPurchaseRow.Item.PurchaseMovementId.HasValue)
        {
            StatusMessage = "La compra ya registrada conserva su fotografía histórica y no se puede editar.";
            IsError = true;
            return;
        }

        suppressMonthlyListDraft = true;
        MonthlyPurchaseItem item = SelectedMonthlyPurchaseRow.Item;
        MonthlyPurchaseName = item.Name;
        SelectedMonthlyPurchaseCategory = SpanishText.For(item.Category);
        MonthlyPurchaseMonth = item.Month.FirstDay.ToDateTime(TimeOnly.MinValue);
        MonthlyPurchaseQuantity = item.Quantity.ToString("0.###", CultureInfo.CurrentCulture);
        MonthlyPurchaseUnitCost = item.ExpectedUnitCost.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        MonthlyPurchaseDescription = item.Description ?? string.Empty;
        IsEditingMonthlyPurchase = true;
        suppressMonthlyListDraft = false;
        StatusMessage = "Edición activa. Pulsa Guardar para confirmar los cambios.";
        IsError = false;
        ScheduleMonthlyListDraft();
    }

    private bool CanEditMonthlyPurchase() =>
        SelectedMonthlyPurchaseRow is not null
        && !SelectedMonthlyPurchaseRow.Item.PurchaseMovementId.HasValue;

    [RelayCommand(CanExecute = nameof(CanSaveMonthlyPurchaseEdit))]
    private async Task SaveMonthlyPurchaseEditAsync()
    {
        try
        {
            if (!CanSaveMonthlyPurchaseEdit() || !MonthlyPurchaseMonth.HasValue)
                throw new InvalidOperationException("Primero selecciona Editar en una compra mensual pendiente.");
            MonthlyPurchaseItem item = SelectedMonthlyPurchaseRow!.Item;
            item.Update(
                MonthlyPurchaseName,
                ParseProductCategory(SelectedMonthlyPurchaseCategory),
                YearMonth.From(DateOnly.FromDateTime(MonthlyPurchaseMonth.Value)),
                ParsePositiveDecimal(MonthlyPurchaseQuantity, "cantidad mensual"),
                Money.FromDecimal(ParsePositiveDecimal(MonthlyPurchaseUnitCost, "costo esperado")),
                item.IsActive,
                item.ReserveWhenOutOfStock,
                timeProvider.GetUtcNow().UtcDateTime,
                MonthlyPurchaseDescription);
            await service.UpdateMonthlyPurchaseItemAsync(item);
            ClearMonthlyPurchaseForm();
            await DeleteMonthlyListDraftAsync();
            await RefreshAsync();
            StatusMessage = "El producto planificado se actualizó y conserva su historial.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible guardar la edición. {exception.Message}";
            IsError = true;
        }
    }

    private bool CanSaveMonthlyPurchaseEdit() =>
        IsEditingMonthlyPurchase
        && SelectedMonthlyPurchaseRow is not null
        && !SelectedMonthlyPurchaseRow.Item.PurchaseMovementId.HasValue;

    [RelayCommand]
    private async Task DeleteMonthlyPurchaseAsync()
    {
        try
        {
            if (SelectedMonthlyPurchaseRow is null || !ConfirmMonthlyPurchaseDelete)
                throw new InvalidOperationException("Selecciona un producto planificado y confirma la eliminación.");
            await service.DeleteAsync(SelectedMonthlyPurchaseRow.Item);
            ClearMonthlyPurchaseForm();
            await DeleteMonthlyListDraftAsync();
            await RefreshAsync();
            StatusMessage = "El producto planificado se eliminó lógicamente y conserva su historial.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible eliminar el producto planificado. {exception.Message}";
            IsError = true;
        }
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
        StatusMessage = Editor.StatusMessage;
        IsError = Editor.IsError;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        await Editor.SaveEditCommand.ExecuteAsync(null);
        if (Editor.IsError)
        {
            StatusMessage = Editor.StatusMessage;
            IsError = true;
            return;
        }

        await RefreshAsync();
        StatusMessage = "El registro se editó correctamente.";
        IsError = false;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await Editor.DeleteCommand.ExecuteAsync(null);
        if (Editor.IsError)
        {
            StatusMessage = Editor.StatusMessage;
            IsError = true;
            return;
        }

        await RefreshAsync();
        StatusMessage = "El registro se eliminó conservando su historial.";
        IsError = false;
    }

    public async Task FlushPendingAsync()
    {
        monthlyListDraftCancellation?.Cancel();
        await PersistMonthlyListDraftAsync();
        await Editor.FlushPendingAsync();
    }

    partial void OnSelectedCurrentRowChanged(InventoryCurrentRow? value)
    {
        SelectedMovementRow = null;
        SelectEditorEntity(value?.Product);
        if (value is not null)
        {
            Editor.LoadSelectedCommand.Execute(null);
            StatusMessage = Editor.StatusMessage;
            IsError = Editor.IsError;
        }
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
        MonthlyPurchaseUnitCost = value.Product.DefaultUnitCost?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty;
    }
    partial void OnSelectedMonthlyPurchaseRowChanged(MonthlyPurchaseRow? value)
    {
        ConfirmMonthlyPurchaseDelete = false;
        EditMonthlyPurchaseCommand.NotifyCanExecuteChanged();
        SaveMonthlyPurchaseEditCommand.NotifyCanExecuteChanged();
        if (value?.Item.PurchaseMovementId.HasValue == true)
        {
            StatusMessage = "La compra registrada está congelada; puedes consultarla o eliminarla conservando el historial.";
            IsError = false;
        }
    }
    partial void OnSelectedPendingMonthlyPurchaseRowChanged(MonthlyPurchaseRow? value)
    {
        if (suppressPendingPurchaseSelection) return;
        Editor.SelectedMonthlyPlanId = value?.Item.Id;
        if (value is null)
        {
            SelectedMonthlyPurchaseCostText = "Selecciona un producto de la lista mensual.";
            return;
        }
        if (Editor.SelectedAction != "Registrar compra")
            Editor.SelectedAction = "Registrar compra";
        if (!preserveRecoveredPurchaseFields)
        {
            Editor.QuantityText = value.Item.Quantity.ToString("0.###", CultureInfo.CurrentCulture);
            Editor.AmountText = value.DefaultSalePrice;
            Editor.OptionalDescriptionText = value.Item.Description ?? string.Empty;
        }
        SelectedMonthlyPurchaseCostText =
            $"Costo de compra definido en la lista: {ApplicationCurrency.Code} {value.Item.ExpectedUnitCost.ToDecimal():N2} por unidad o paquete.";
        suppressMonthlyPurchaseSearch = true;
        MonthlyPurchaseSearchText = value.Product;
        suppressMonthlyPurchaseSearch = false;
        IsMonthlyPurchaseDropDownOpen = false;
    }
    partial void OnMonthlyPurchaseSearchTextChanged(string value)
    {
        if (suppressMonthlyPurchaseSearch) return;
        SelectedPendingMonthlyPurchaseRow = null;
        ApplyMonthlyPurchaseFilter();
        IsMonthlyPurchaseDropDownOpen = true;
    }
    partial void OnMonthlyPurchaseNameChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnSelectedMonthlyPurchaseCategoryChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnMonthlyPurchaseMonthChanged(DateTime? value) => ScheduleMonthlyListDraft();
    partial void OnMonthlyPurchaseQuantityChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnMonthlyPurchaseUnitCostChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnMonthlyPurchaseDescriptionChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnIsEditingMonthlyPurchaseChanged(bool value)
    {
        SaveMonthlyPurchaseEditCommand.NotifyCanExecuteChanged();
        ScheduleMonthlyListDraft();
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

    [RelayCommand]
    private async Task RegisterMonthlyPurchaseAsync()
    {
        try
        {
            if (SelectedPendingMonthlyPurchaseRow is null)
                throw new InvalidOperationException("Selecciona un producto pendiente de la lista mensual.");
            if (!Editor.FormDate.HasValue)
                throw new ArgumentException("Selecciona la fecha de compra.");

            decimal quantity = ParsePositiveDecimal(Editor.QuantityText, "cantidad comprada");
            Money? salePrice = null;
            if (SelectedPendingMonthlyPurchaseRow.RequiresSalePrice)
            {
                salePrice = Money.FromDecimal(ParsePositiveDecimal(Editor.AmountText, "precio de venta"));
            }

            await service.RegisterMonthlyPurchaseAsync(
                SelectedPendingMonthlyPurchaseRow.Item.Id,
                DateOnly.FromDateTime(Editor.FormDate.Value),
                Quantity.Positive(quantity),
                salePrice,
                Editor.OptionalDescriptionText,
                completedDraftKey: MonthlyPurchaseDraftKey);

            Editor.SelectedMonthlyPlanId = null;
            Editor.QuantityText = string.Empty;
            Editor.AmountText = string.Empty;
            Editor.OptionalDescriptionText = string.Empty;
            suppressPendingPurchaseSelection = true;
            SelectedPendingMonthlyPurchaseRow = null;
            suppressPendingPurchaseSelection = false;
            suppressMonthlyPurchaseSearch = true;
            MonthlyPurchaseSearchText = string.Empty;
            suppressMonthlyPurchaseSearch = false;
            SelectedMonthlyPurchaseCostText = "Selecciona un producto de la lista mensual.";
            await RefreshAsync();
            StatusMessage = "La compra se registró y el producto quedó incorporado al inventario.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible registrar la compra. {exception.Message}";
            IsError = true;
        }
    }

    public static bool MatchesMonthlyPurchaseSearch(MonthlyPurchaseRow row, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        string normalizedSearch = NormalizeSearch(search);
        return NormalizeSearch(row.Product).Contains(normalizedSearch, StringComparison.Ordinal)
            || NormalizeSearch(row.Category).Contains(normalizedSearch, StringComparison.Ordinal)
            || NormalizeSearch(row.Month).Contains(normalizedSearch, StringComparison.Ordinal);
    }

    private void ApplyMonthlyPurchaseFilter()
    {
        PendingMonthlyPurchaseRows.Clear();
        foreach (MonthlyPurchaseRow row in allPendingMonthlyPurchaseRows
                     .Where(item => MatchesMonthlyPurchaseSearch(item, MonthlyPurchaseSearchText)))
        {
            PendingMonthlyPurchaseRows.Add(row);
        }
        HasNoPendingMonthlyPurchases = PendingMonthlyPurchaseRows.Count == 0;
    }

    private static string NormalizeSearch(string value)
    {
        string decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToUpperInvariant(character));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
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

    private void ScheduleMonthlyListDraft()
    {
        if (suppressMonthlyListDraft || formDraftStore is null) return;
        monthlyListDraftCancellation?.Cancel();
        monthlyListDraftCancellation = new CancellationTokenSource();
        _ = PersistMonthlyListDraftAfterDelayAsync(monthlyListDraftCancellation.Token);
    }

    private async Task PersistMonthlyListDraftAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            await PersistMonthlyListDraftAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PersistMonthlyListDraftAsync(CancellationToken cancellationToken = default)
    {
        if (formDraftStore is null) return;
        await monthlyListDraftLock.WaitAsync(cancellationToken);
        try
        {
            if (!HasMonthlyListFormContent())
            {
                await formDraftStore.DeleteAsync(MonthlyListDraftKey, cancellationToken);
                return;
            }

            var payload = new MonthlyListDraftPayload(
                MonthlyPurchaseName,
                SelectedMonthlyPurchaseCategory,
                MonthlyPurchaseMonth,
                MonthlyPurchaseQuantity,
                MonthlyPurchaseUnitCost,
                MonthlyPurchaseDescription,
                IsEditingMonthlyPurchase,
                IsEditingMonthlyPurchase ? SelectedMonthlyPurchaseRow?.Item.Id : null,
                SelectedMonthlyPurchaseProduct?.Product.Id);
            await formDraftStore.UpsertAsync(FormDraft.Create(
                MonthlyListDraftKey,
                AdministrationViewModel.InventoryModule,
                "Lista mensual de compra",
                JsonSerializer.Serialize(payload),
                payload.EditedItemId,
                payload.IsEditing,
                timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = $"No fue posible conservar la lista mensual escrita: {exception.Message}";
            IsError = true;
        }
        finally
        {
            monthlyListDraftLock.Release();
        }
    }

    private async Task RestoreMonthlyListDraftAsync()
    {
        if (formDraftStore is null) return;
        try
        {
            FormDraft? draft = await formDraftStore.FindAsync(MonthlyListDraftKey);
            if (draft is null) return;
            MonthlyListDraftPayload? payload =
                JsonSerializer.Deserialize<MonthlyListDraftPayload>(draft.PayloadJson);
            if (payload is null) return;

            suppressMonthlyListDraft = true;
            MonthlyPurchaseName = payload.Name;
            SelectedMonthlyPurchaseCategory = payload.Category;
            MonthlyPurchaseMonth = payload.Month;
            MonthlyPurchaseQuantity = payload.Quantity;
            MonthlyPurchaseUnitCost = payload.UnitCost;
            MonthlyPurchaseDescription = payload.Description;
            SelectedMonthlyPurchaseProduct = payload.SelectedProductId.HasValue
                ? CurrentInventory.SingleOrDefault(
                    item => item.Product.Id == payload.SelectedProductId.Value)
                : null;
            SelectedMonthlyPurchaseRow = payload.EditedItemId.HasValue
                ? MonthlyPurchaseRows.SingleOrDefault(
                    item => item.Item.Id == payload.EditedItemId.Value)
                : null;
            IsEditingMonthlyPurchase = payload.IsEditing
                && SelectedMonthlyPurchaseRow is not null
                && !SelectedMonthlyPurchaseRow.Item.PurchaseMovementId.HasValue;
            if (HasMonthlyListFormContent())
            {
                StatusMessage = payload.IsEditing && !IsEditingMonthlyPurchase
                    ? "Se recuperaron los campos; la fila original ya no admite edición y quedaron como borrador."
                    : "Se recuperó el borrador de la lista mensual de compra.";
                IsError = false;
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            StatusMessage = "El borrador de la lista mensual no pudo recuperarse y los datos registrados permanecen intactos.";
            IsError = true;
        }
        finally
        {
            suppressMonthlyListDraft = false;
        }
    }

    private async Task DeleteMonthlyListDraftAsync()
    {
        monthlyListDraftCancellation?.Cancel();
        if (formDraftStore is not null)
            await formDraftStore.DeleteAsync(MonthlyListDraftKey);
    }

    private bool HasMonthlyListFormContent() =>
        IsEditingMonthlyPurchase
        || !string.IsNullOrWhiteSpace(MonthlyPurchaseName)
        || !string.IsNullOrWhiteSpace(MonthlyPurchaseQuantity)
        || !string.IsNullOrWhiteSpace(MonthlyPurchaseUnitCost)
        || !string.IsNullOrWhiteSpace(MonthlyPurchaseDescription);

    private void ClearMonthlyPurchaseForm()
    {
        suppressMonthlyListDraft = true;
        MonthlyPurchaseName = MonthlyPurchaseQuantity = MonthlyPurchaseUnitCost =
            MonthlyPurchaseDescription = string.Empty;
        SelectedMonthlyPurchaseCategory = "Otro producto del local";
        MonthlyPurchaseMonth = timeProvider.GetLocalNow().DateTime.Date;
        SelectedMonthlyPurchaseProduct = null;
        SelectedMonthlyPurchaseRow = null;
        ConfirmMonthlyPurchaseDelete = false;
        IsEditingMonthlyPurchase = false;
        suppressMonthlyListDraft = false;
    }

    private sealed record MonthlyListDraftPayload(
        string Name,
        string Category,
        DateTime? Month,
        string Quantity,
        string UnitCost,
        string Description,
        bool IsEditing,
        Guid? EditedItemId,
        Guid? SelectedProductId);
}

public sealed record InventoryCurrentRow(Product Product, string Name, string Category, string CurrentQuantity,
    string AverageUnitCost, string DefaultSalePrice, string Description, string LastUpdate);
public sealed record InventoryMovementRow(InventoryMovement Movement, string Date, string Type, string Product,
    string QuantityIn, string QuantityOut, string UnitCost, string TotalValue, string Description);
public sealed record MonthlyPurchaseRow(MonthlyPurchaseItem Item, string Product, string Category, string Month,
    string Quantity, string UnitCost, string Total, string State, string Description,
    bool RequiresSalePrice, string DefaultSalePrice);
