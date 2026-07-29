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
    public ObservableCollection<MonthlyPurchaseRow> MonthlyPurchaseRows { get; } = [];
    public ObservableCollection<MonthlyPurchaseRow> PendingMonthlyPurchaseRows { get; } = [];
    public ObservableCollection<string> ProductCategoryOptions { get; } =
        ["Alimento o bebida para venta", "Otro producto para venta", "Cortesía para clientes",
         "Aseo", "Insumo del local", "Otro producto del local"];
    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Todos", "Rango personalizado"];

    [ObservableProperty] private string selectedPeriod = string.Empty;
    [ObservableProperty] private DateTime? customPeriodFrom;
    [ObservableProperty] private DateTime? customPeriodThrough;
    [ObservableProperty] private bool showCustomPeriod;
    [ObservableProperty] private InventoryCurrentRow? selectedCurrentRow;
    [ObservableProperty] private MonthlyPurchaseRow? selectedMonthlyPurchaseRow;
    [ObservableProperty] private MonthlyPurchaseRow? selectedPendingMonthlyPurchaseRow;
    [ObservableProperty] private string monthlyPurchaseSearchText = string.Empty;
    [ObservableProperty] private bool isMonthlyPurchaseDropDownOpen;
    [ObservableProperty] private bool hasNoPendingMonthlyPurchases;
    [ObservableProperty] private string selectedMonthlyPurchaseSummaryText = "Selecciona un producto de la lista de compra.";

    [ObservableProperty] private string monthlyPurchaseName = string.Empty;
    [ObservableProperty] private string selectedMonthlyPurchaseCategory = string.Empty;
    [ObservableProperty] private string monthlyPurchaseQuantity = string.Empty;
    [ObservableProperty] private string monthlyPurchaseUnitCost = string.Empty;
    [ObservableProperty] private string monthlyPurchaseDescription = string.Empty;
    [ObservableProperty] private bool isEditingMonthlyPurchase;
    [ObservableProperty] private bool confirmMonthlyPurchaseDelete;

    [ObservableProperty] private bool isEditingInventorySelection;
    [ObservableProperty] private DateTime? inventoryEditDate;
    [ObservableProperty] private string inventoryEditName = string.Empty;
    [ObservableProperty] private string inventoryEditCategory = string.Empty;
    [ObservableProperty] private string inventoryEditExpectedQuantity = string.Empty;
    [ObservableProperty] private string inventoryEditExpectedUnitCost = string.Empty;
    [ObservableProperty] private string inventoryEditListDescription = string.Empty;
    [ObservableProperty] private string inventoryEditPurchasedQuantity = string.Empty;
    [ObservableProperty] private string inventoryEditSalePrice = string.Empty;
    [ObservableProperty] private string inventoryEditDescription = string.Empty;

    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;

    public string MonthlyPurchaseExpectedTotalText
    {
        get
        {
            if (!TryParseDecimal(MonthlyPurchaseQuantity, out decimal quantity)
                || !TryParseDecimal(MonthlyPurchaseUnitCost, out decimal unitCost)
                || quantity <= 0m
                || unitCost <= 0m)
            {
                return $"{ApplicationCurrency.Code} 0,00";
            }

            return $"{ApplicationCurrency.Code} {quantity * unitCost:N2}";
        }
    }

    public bool InventoryEditRequiresSalePrice =>
        CategoryRequiresSalePrice(InventoryEditCategory);

    public bool ShowInventoryAddForm => !IsEditingInventorySelection;

    public async Task LoadAsync()
    {
        await Editor.SelectModuleAsync(AdministrationViewModel.InventoryModule);
        Editor.SelectedAction = string.Empty;
        Editor.FormDate = null;
        await RefreshAsync();
        await RestoreMonthlyListDraftAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            AdministrationData data = await service.LoadAsync();
            _ = await getSettings.ExecuteAsync();
            Guid? selectedCurrentPlanId = SelectedCurrentRow?.Plan.Id;
            Guid? selectedPendingPlanId = Editor.SelectedMonthlyPlanId;
            bool preservePurchaseFields = Editor.HasRecoveredDraft;

            CurrentInventory.Clear();
            foreach (MonthlyPurchaseItem plan in data.MonthlyPurchaseItems
                         .Where(item => item.ProductId.HasValue && item.PurchaseMovementId.HasValue)
                         .OrderByDescending(item => item.UpdatedUtc)
                         .ThenBy(item => item.Name))
            {
                Product? product = data.Products.SingleOrDefault(item => item.Id == plan.ProductId!.Value);
                InventoryMovement? purchase = data.InventoryMovements
                    .SingleOrDefault(item => item.Id == plan.PurchaseMovementId!.Value);
                if (product is null || purchase is null)
                {
                    continue;
                }

                InventoryMovement[] productMovements = data.InventoryMovements
                    .Where(item => item.ProductId == product.Id)
                    .ToArray();
                decimal currentQuantity = InventoryCalculator.CurrentQuantity(productMovements);
                decimal actualUnitCost = UnitCostOf(purchase);
                DateTime lastUpdatedUtc = productMovements
                    .Select(item => item.UpdatedUtc)
                    .Append(product.UpdatedUtc)
                    .Append(plan.UpdatedUtc)
                    .Max();
                CurrentInventory.Add(new InventoryCurrentRow(
                    product,
                    plan,
                    purchase,
                    plan.Name,
                    SpanishText.For(plan.Category),
                    plan.Quantity.ToString("0.###", CultureInfo.CurrentCulture),
                    $"{ApplicationCurrency.Code} {plan.ExpectedUnitCost.ToDecimal():N2}",
                    plan.Description ?? string.Empty,
                    purchase.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture),
                    $"{ApplicationCurrency.Code} {actualUnitCost:N2}",
                    FormatTotalValue(purchase, ApplicationCurrency.Code),
                    currentQuantity.ToString("0.###", CultureInfo.CurrentCulture),
                    $"{ApplicationCurrency.Code} {currentQuantity * actualUnitCost:N2}",
                    product.DefaultSalePrice.HasValue
                        ? $"{ApplicationCurrency.Code} {product.DefaultSalePrice.Value.ToDecimal():N2}"
                        : string.Empty,
                    product.Description ?? purchase.Description ?? string.Empty,
                    purchase.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    lastUpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
            }
            SelectedCurrentRow = selectedCurrentPlanId.HasValue
                ? CurrentInventory.SingleOrDefault(item => item.Plan.Id == selectedCurrentPlanId.Value)
                : null;

            suppressPendingPurchaseSelection = true;
            SelectedPendingMonthlyPurchaseRow = null;
            MonthlyPurchaseRows.Clear();
            PendingMonthlyPurchaseRows.Clear();
            allPendingMonthlyPurchaseRows.Clear();
            foreach (MonthlyPurchaseItem item in data.MonthlyPurchaseItems
                         .OrderByDescending(item => item.CreatedUtc)
                         .ThenBy(item => item.Name))
            {
                Product? linkedProduct = item.ProductId.HasValue
                    ? data.Products.SingleOrDefault(product => product.Id == item.ProductId.Value)
                    : null;
                InventoryMovement? linkedPurchase = item.PurchaseMovementId.HasValue
                    ? data.InventoryMovements.SingleOrDefault(
                        movement => movement.Id == item.PurchaseMovementId.Value)
                    : null;
                decimal currentQuantity = linkedProduct is null
                    ? 0m
                    : InventoryCalculator.CurrentQuantity(
                        data.InventoryMovements.Where(movement => movement.ProductId == linkedProduct.Id));
                bool requiresSalePrice = linkedProduct?.IsForSale
                    ?? item.Category is ProductCategory.FoodOrDrinkForSale or ProductCategory.OtherProductForSale;
                var row = new MonthlyPurchaseRow(
                    item,
                    item.Name,
                    SpanishText.For(item.Category),
                    item.Quantity.ToString("0.###", CultureInfo.CurrentCulture),
                    $"{ApplicationCurrency.Code} {item.ExpectedUnitCost.ToDecimal():N2}",
                    $"{ApplicationCurrency.Code} {Money.FromMinorUnits(item.ExpectedTotalMinorUnits).ToDecimal():N2}",
                    linkedPurchase?.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty,
                    linkedPurchase is null
                        ? string.Empty
                        : $"{ApplicationCurrency.Code} {UnitCostOf(linkedPurchase):N2}",
                    linkedPurchase is null
                        ? string.Empty
                        : FormatTotalValue(linkedPurchase, ApplicationCurrency.Code),
                    linkedProduct is null
                        ? string.Empty
                        : currentQuantity.ToString("0.###", CultureInfo.CurrentCulture),
                    item.PurchaseMovementId.HasValue ? "Agregado al inventario" : "Pendiente",
                    item.Description ?? string.Empty,
                    requiresSalePrice,
                    linkedProduct?.DefaultSalePrice?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty);
                MonthlyPurchaseRows.Add(row);
                if (!item.PurchaseMovementId.HasValue)
                {
                    allPendingMonthlyPurchaseRows.Add(row);
                }
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
                         .Where(item => SelectedPeriod == "Todos"
                             || range.HasValue && range.Value.Contains(item.Date))
                         .OrderByDescending(item => item.Date)
                         .ThenByDescending(item => item.CreatedUtc))
            {
                Product? product = data.Products.SingleOrDefault(item => item.Id == movement.ProductId);
                MovementHistory.Add(new InventoryMovementRow(
                    movement,
                    movement.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    MovementName(movement.Type),
                    product?.Name ?? "Producto eliminado",
                    movement.QuantityDelta > 0m
                        ? movement.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture)
                        : string.Empty,
                    movement.QuantityDelta < 0m
                        ? Math.Abs(movement.QuantityDelta).ToString("0.###", CultureInfo.CurrentCulture)
                        : string.Empty,
                    FormatUnitCost(movement, ApplicationCurrency.Code),
                    FormatTotalValue(movement, ApplicationCurrency.Code),
                    movement.Description ?? string.Empty));
            }

            StatusMessage = CurrentInventory.Count == 0
                ? "Aún no hay productos agregados al inventario actual."
                : string.Empty;
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
    private async Task RegisterMonthlyPurchaseAsync()
    {
        try
        {
            if (SelectedPendingMonthlyPurchaseRow is null)
            {
                throw new InvalidOperationException("Selecciona un producto pendiente de la lista de compra.");
            }

            if (!Editor.FormDate.HasValue)
            {
                throw new InvalidOperationException("La fecha agregada es obligatoria.");
            }
            DateOnly entryDate = DateOnly.FromDateTime(Editor.FormDate.Value);
            decimal quantity = ParsePositiveDecimal(Editor.QuantityText, "cantidad comprada");
            Money? salePrice = null;
            if (SelectedPendingMonthlyPurchaseRow.RequiresSalePrice)
            {
                salePrice = Money.FromDecimal(ParsePositiveDecimal(Editor.AmountText, "precio de venta"));
            }

            await service.RegisterMonthlyPurchaseAsync(
                SelectedPendingMonthlyPurchaseRow.Item.Id,
                entryDate,
                Quantity.Positive(quantity),
                salePrice,
                Editor.OptionalDescriptionText,
                completedDraftKey: "Inventario:Registrar compra:new");

            Editor.SelectedMonthlyPlanId = null;
            Editor.FormDate = null;
            Editor.QuantityText = string.Empty;
            Editor.AmountText = string.Empty;
            Editor.OptionalDescriptionText = string.Empty;
            suppressPendingPurchaseSelection = true;
            SelectedPendingMonthlyPurchaseRow = null;
            suppressPendingPurchaseSelection = false;
            suppressMonthlyPurchaseSearch = true;
            MonthlyPurchaseSearchText = string.Empty;
            suppressMonthlyPurchaseSearch = false;
            SelectedMonthlyPurchaseSummaryText = "Selecciona un producto de la lista de compra.";
            await RefreshAsync();
            StatusMessage = "El producto se agregó al inventario actual.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible agregar el producto al inventario. {exception.Message}";
            IsError = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditInventorySelection))]
    private void EditInventorySelection()
    {
        if (SelectedCurrentRow is null)
        {
            StatusMessage = "Selecciona un producto del inventario actual.";
            IsError = true;
            return;
        }

        InventoryCurrentRow row = SelectedCurrentRow;
        InventoryEditName = row.Plan.Name;
        InventoryEditCategory = SpanishText.For(row.Plan.Category);
        InventoryEditExpectedQuantity = row.Plan.Quantity.ToString("0.###", CultureInfo.CurrentCulture);
        InventoryEditExpectedUnitCost = row.Plan.ExpectedUnitCost.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        InventoryEditListDescription = row.Plan.Description ?? string.Empty;
        InventoryEditPurchasedQuantity = row.PurchaseMovement.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture);
        InventoryEditSalePrice = row.Product.DefaultSalePrice?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty;
        InventoryEditDescription = row.Product.Description ?? row.PurchaseMovement.Description ?? string.Empty;
        InventoryEditDate = row.PurchaseMovement.Date.ToDateTime(TimeOnly.MinValue);
        IsEditingInventorySelection = true;
        StatusMessage = "Edición activa para el producto seleccionado.";
        IsError = false;
    }

    private bool CanEditInventorySelection() => SelectedCurrentRow is not null;

    [RelayCommand(CanExecute = nameof(CanSaveInventorySelectionEdit))]
    private async Task SaveInventorySelectionEditAsync()
    {
        try
        {
            if (!CanSaveInventorySelectionEdit() || !InventoryEditDate.HasValue)
            {
                throw new InvalidOperationException("Pulsa Editar selección antes de guardar cambios.");
            }

            ProductCategory category = ParseProductCategory(InventoryEditCategory);
            Money? salePrice = null;
            if (CategoryRequiresSalePrice(category))
            {
                salePrice = Money.FromDecimal(ParsePositiveDecimal(InventoryEditSalePrice, "precio de venta"));
            }

            Guid planId = SelectedCurrentRow!.Plan.Id;
            await service.UpdateRegisteredMonthlyPurchaseAsync(
                planId,
                InventoryEditName,
                category,
                ParsePositiveDecimal(InventoryEditExpectedQuantity, "cantidad esperada"),
                Money.FromDecimal(ParsePositiveDecimal(InventoryEditExpectedUnitCost, "precio unitario o por paquete")),
                InventoryEditListDescription,
                DateOnly.FromDateTime(InventoryEditDate.Value),
                Quantity.Positive(ParsePositiveDecimal(InventoryEditPurchasedQuantity, "cantidad comprada")),
                salePrice,
                InventoryEditDescription);

            IsEditingInventorySelection = false;
            ClearInventoryEditForm();
            await RefreshAsync();
            SelectedCurrentRow = CurrentInventory.SingleOrDefault(item => item.Plan.Id == planId);
            StatusMessage = "La selección se editó correctamente.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible editar el producto seleccionado. {exception.Message}";
            IsError = true;
        }
    }

    private bool CanSaveInventorySelectionEdit() =>
        IsEditingInventorySelection && SelectedCurrentRow is not null;

    [RelayCommand]
    private void CancelInventorySelectionEdit()
    {
        IsEditingInventorySelection = false;
        ClearInventoryEditForm();
        StatusMessage = "Edición cancelada.";
        IsError = false;
    }

    [RelayCommand]
    private async Task AddMonthlyPurchaseAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(MonthlyPurchaseName))
            {
                throw new ArgumentException("Escribe el nombre del producto.");
            }

            decimal quantity = ParsePositiveDecimal(MonthlyPurchaseQuantity, "cantidad esperada");
            decimal cost = ParsePositiveDecimal(MonthlyPurchaseUnitCost, "precio unitario o por paquete");
            AdministrationData data = await service.LoadAsync();
            Product? matchingProduct = data.Products.SingleOrDefault(item =>
                item.Name.Equals(MonthlyPurchaseName.Trim(), StringComparison.OrdinalIgnoreCase));
            DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            var item = MonthlyPurchaseItem.Create(
                MonthlyPurchaseName,
                ParseProductCategory(SelectedMonthlyPurchaseCategory),
                YearMonth.From(today),
                quantity,
                Money.FromDecimal(cost),
                true,
                false,
                timeProvider.GetUtcNow().UtcDateTime,
                MonthlyPurchaseDescription,
                matchingProduct?.Id);
            await service.AddMonthlyPurchaseItemAsync(item);
            ClearMonthlyPurchaseForm();
            await DeleteMonthlyListDraftAsync();
            await RefreshAsync();
            StatusMessage = "El producto se agregó a la lista de compra.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible agregar el producto a la lista. {exception.Message}";
            IsError = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditMonthlyPurchase))]
    private void EditMonthlyPurchase()
    {
        if (SelectedMonthlyPurchaseRow is null)
        {
            StatusMessage = "Selecciona un producto de la lista.";
            IsError = true;
            return;
        }

        if (SelectedMonthlyPurchaseRow.Item.PurchaseMovementId.HasValue)
        {
            StatusMessage = "El producto ya está en Inventario actual; usa Editar selección allí.";
            IsError = true;
            return;
        }

        suppressMonthlyListDraft = true;
        MonthlyPurchaseItem item = SelectedMonthlyPurchaseRow.Item;
        MonthlyPurchaseName = item.Name;
        SelectedMonthlyPurchaseCategory = SpanishText.For(item.Category);
        MonthlyPurchaseQuantity = item.Quantity.ToString("0.###", CultureInfo.CurrentCulture);
        MonthlyPurchaseUnitCost = item.ExpectedUnitCost.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        MonthlyPurchaseDescription = item.Description ?? string.Empty;
        IsEditingMonthlyPurchase = true;
        suppressMonthlyListDraft = false;
        StatusMessage = "Edición activa. Pulsa Guardar cambios para confirmar.";
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
            if (!CanSaveMonthlyPurchaseEdit())
            {
                throw new InvalidOperationException("Primero selecciona Editar selección en una fila pendiente.");
            }

            MonthlyPurchaseItem item = SelectedMonthlyPurchaseRow!.Item;
            item.Update(
                MonthlyPurchaseName,
                ParseProductCategory(SelectedMonthlyPurchaseCategory),
                item.Month,
                ParsePositiveDecimal(MonthlyPurchaseQuantity, "cantidad esperada"),
                Money.FromDecimal(ParsePositiveDecimal(MonthlyPurchaseUnitCost, "precio unitario o por paquete")),
                item.IsActive,
                item.ReserveWhenOutOfStock,
                timeProvider.GetUtcNow().UtcDateTime,
                MonthlyPurchaseDescription);
            await service.UpdateMonthlyPurchaseItemAsync(item);
            ClearMonthlyPurchaseForm();
            await DeleteMonthlyListDraftAsync();
            await RefreshAsync();
            StatusMessage = "El producto de la lista se actualizó.";
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
            {
                throw new InvalidOperationException("Selecciona un producto pendiente y confirma la eliminación.");
            }
            if (SelectedMonthlyPurchaseRow.Item.PurchaseMovementId.HasValue)
            {
                throw new InvalidOperationException("Un producto ya agregado al inventario no se elimina desde la lista.");
            }

            await service.DeleteAsync(SelectedMonthlyPurchaseRow.Item);
            ClearMonthlyPurchaseForm();
            await DeleteMonthlyListDraftAsync();
            await RefreshAsync();
            StatusMessage = "El producto se eliminó de la lista conservando su historial.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible eliminar el producto de la lista. {exception.Message}";
            IsError = true;
        }
    }

    public async Task FlushPendingAsync()
    {
        monthlyListDraftCancellation?.Cancel();
        await PersistMonthlyListDraftAsync();
        await Editor.FlushPendingAsync();
    }

    partial void OnSelectedCurrentRowChanged(InventoryCurrentRow? value)
    {
        EditInventorySelectionCommand.NotifyCanExecuteChanged();
        SaveInventorySelectionEditCommand.NotifyCanExecuteChanged();
        if (value is null && IsEditingInventorySelection)
        {
            IsEditingInventorySelection = false;
            ClearInventoryEditForm();
        }
    }

    partial void OnSelectedMonthlyPurchaseRowChanged(MonthlyPurchaseRow? value)
    {
        ConfirmMonthlyPurchaseDelete = false;
        EditMonthlyPurchaseCommand.NotifyCanExecuteChanged();
        SaveMonthlyPurchaseEditCommand.NotifyCanExecuteChanged();
        if (value?.Item.PurchaseMovementId.HasValue == true)
        {
            StatusMessage = "El producto ya está en Inventario actual; allí puedes usar Editar selección.";
            IsError = false;
        }
    }

    partial void OnSelectedPendingMonthlyPurchaseRowChanged(MonthlyPurchaseRow? value)
    {
        if (suppressPendingPurchaseSelection)
        {
            return;
        }

        Editor.SelectedMonthlyPlanId = value?.Item.Id;
        if (value is null)
        {
            SelectedMonthlyPurchaseSummaryText = "Selecciona un producto de la lista de compra.";
            return;
        }

        if (Editor.SelectedAction != "Registrar compra")
        {
            Editor.SelectedAction = "Registrar compra";
        }
        if (!preserveRecoveredPurchaseFields)
        {
            Editor.FormDate = null;
            Editor.QuantityText = string.Empty;
            Editor.AmountText = string.Empty;
            Editor.OptionalDescriptionText = string.Empty;
        }

        string description = string.IsNullOrWhiteSpace(value.Description)
            ? "Sin descripción en la lista."
            : value.Description;
        SelectedMonthlyPurchaseSummaryText =
            $"{value.Product} · {value.Category} · Cantidad esperada: {value.Quantity} · "
            + $"Precio unitario o por paquete: {value.UnitPrice}. {description}";
        suppressMonthlyPurchaseSearch = true;
        MonthlyPurchaseSearchText = value.Product;
        suppressMonthlyPurchaseSearch = false;
        IsMonthlyPurchaseDropDownOpen = false;
    }

    partial void OnMonthlyPurchaseSearchTextChanged(string value)
    {
        if (suppressMonthlyPurchaseSearch)
        {
            return;
        }

        SelectedPendingMonthlyPurchaseRow = null;
        ApplyMonthlyPurchaseFilter();
        IsMonthlyPurchaseDropDownOpen = true;
    }

    partial void OnMonthlyPurchaseNameChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnSelectedMonthlyPurchaseCategoryChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnMonthlyPurchaseQuantityChanged(string value)
    {
        OnPropertyChanged(nameof(MonthlyPurchaseExpectedTotalText));
        ScheduleMonthlyListDraft();
    }
    partial void OnMonthlyPurchaseUnitCostChanged(string value)
    {
        OnPropertyChanged(nameof(MonthlyPurchaseExpectedTotalText));
        ScheduleMonthlyListDraft();
    }
    partial void OnMonthlyPurchaseDescriptionChanged(string value) => ScheduleMonthlyListDraft();
    partial void OnIsEditingMonthlyPurchaseChanged(bool value)
    {
        SaveMonthlyPurchaseEditCommand.NotifyCanExecuteChanged();
        ScheduleMonthlyListDraft();
    }
    partial void OnIsEditingInventorySelectionChanged(bool value)
    {
        SaveInventorySelectionEditCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowInventoryAddForm));
    }
    partial void OnInventoryEditCategoryChanged(string value)
    {
        if (!CategoryRequiresSalePrice(value))
        {
            InventoryEditSalePrice = string.Empty;
        }
        OnPropertyChanged(nameof(InventoryEditRequiresSalePrice));
    }
    partial void OnSelectedPeriodChanged(string value)
    {
        ShowCustomPeriod = value == "Rango personalizado";
        _ = RefreshAsync();
    }
    partial void OnCustomPeriodFromChanged(DateTime? value)
    {
        if (ShowCustomPeriod)
        {
            _ = RefreshAsync();
        }
    }
    partial void OnCustomPeriodThroughChanged(DateTime? value)
    {
        if (ShowCustomPeriod)
        {
            _ = RefreshAsync();
        }
    }

    public static bool MatchesMonthlyPurchaseSearch(MonthlyPurchaseRow row, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        string normalizedSearch = NormalizeSearch(search);
        return NormalizeSearch(row.Product).Contains(normalizedSearch, StringComparison.Ordinal)
            || NormalizeSearch(row.Category).Contains(normalizedSearch, StringComparison.Ordinal)
            || NormalizeSearch(row.Description).Contains(normalizedSearch, StringComparison.Ordinal);
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

    private ActivityDateRange? CurrentRange()
    {
        if (SelectedPeriod == "Todos")
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(SelectedPeriod)) return null;

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        ActivityPeriod period = SelectedPeriod switch
        {
            "Esta semana" => ActivityPeriod.ThisWeek,
            "Este mes" => ActivityPeriod.ThisMonth,
            "Últimos 3 meses" => ActivityPeriod.LastThreeMonths,
            "Últimos 6 meses" => ActivityPeriod.LastSixMonths,
            "Este año" => ActivityPeriod.ThisYear,
            "Rango personalizado" => ActivityPeriod.Custom,
            "Hoy" => ActivityPeriod.Today,
            _ => throw new ArgumentException("Selecciona el periodo que deseas consultar."),
        };
        return ActivityPeriodCalculator.Calculate(
            period,
            today,
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
        if (!movement.EstimatedCost.HasValue || movement.QuantityDelta == 0m)
        {
            return string.Empty;
        }

        decimal unitCost = movement.EstimatedCost.Value.ToDecimal() / Math.Abs(movement.QuantityDelta);
        return $"{currencyCode} {unitCost:N2}";
    }

    private static string FormatTotalValue(InventoryMovement movement, string currencyCode)
    {
        Money? total = movement.CashAmount ?? movement.EstimatedCost;
        return total.HasValue ? $"{currencyCode} {total.Value.ToDecimal():N2}" : string.Empty;
    }

    private static decimal UnitCostOf(InventoryMovement movement)
    {
        Money? total = movement.CashAmount ?? movement.EstimatedCost;
        return total.HasValue && movement.QuantityDelta != 0m
            ? total.Value.ToDecimal() / Math.Abs(movement.QuantityDelta)
            : 0m;
    }

    private static decimal ParsePositiveDecimal(string value, string field)
    {
        bool valid = TryParseDecimal(value, out decimal amount);
        return valid && amount > 0m
            ? amount
            : throw new ArgumentException($"La {field} debe ser mayor que cero.");
    }

    private static bool TryParseDecimal(string value, out decimal amount) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    private static ProductCategory ParseProductCategory(string value) => value switch
    {
        "Alimento o bebida para venta" => ProductCategory.FoodOrDrinkForSale,
        "Otro producto para venta" => ProductCategory.OtherProductForSale,
        "Cortesía para clientes" => ProductCategory.CustomerCourtesy,
        "Aseo" => ProductCategory.Cleaning,
        "Insumo del local" => ProductCategory.LocalSupply,
        "Otro producto del local" => ProductCategory.OtherLocalProduct,
        _ => throw new ArgumentException("Selecciona una categoría de inventario."),
    };

    private static bool CategoryRequiresSalePrice(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && CategoryRequiresSalePrice(ParseProductCategory(value));

    private static bool CategoryRequiresSalePrice(ProductCategory category) =>
        category is ProductCategory.FoodOrDrinkForSale or ProductCategory.OtherProductForSale;

    private static string NormalizeSearch(string value)
    {
        string decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void ScheduleMonthlyListDraft()
    {
        if (suppressMonthlyListDraft || formDraftStore is null)
        {
            return;
        }

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
        if (formDraftStore is null)
        {
            return;
        }

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
                MonthlyPurchaseQuantity,
                MonthlyPurchaseUnitCost,
                MonthlyPurchaseDescription,
                IsEditingMonthlyPurchase,
                IsEditingMonthlyPurchase ? SelectedMonthlyPurchaseRow?.Item.Id : null);
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
            StatusMessage = $"No fue posible conservar la lista escrita: {exception.Message}";
            IsError = true;
        }
        finally
        {
            monthlyListDraftLock.Release();
        }
    }

    private async Task RestoreMonthlyListDraftAsync()
    {
        if (formDraftStore is null)
        {
            return;
        }

        try
        {
            FormDraft? draft = await formDraftStore.FindAsync(MonthlyListDraftKey);
            if (draft is null)
            {
                return;
            }

            MonthlyListDraftPayload? payload =
                JsonSerializer.Deserialize<MonthlyListDraftPayload>(draft.PayloadJson);
            if (payload is null)
            {
                return;
            }

            suppressMonthlyListDraft = true;
            MonthlyPurchaseName = payload.Name;
            SelectedMonthlyPurchaseCategory = payload.Category;
            MonthlyPurchaseQuantity = payload.Quantity;
            MonthlyPurchaseUnitCost = payload.UnitCost;
            MonthlyPurchaseDescription = payload.Description;
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
                    : "Se recuperó el borrador de la lista de compra.";
                IsError = false;
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            StatusMessage = "El borrador de la lista no pudo recuperarse y los datos registrados permanecen intactos.";
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
        {
            await formDraftStore.DeleteAsync(MonthlyListDraftKey);
        }
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
        MonthlyPurchaseName = string.Empty;
        SelectedMonthlyPurchaseCategory = string.Empty;
        MonthlyPurchaseQuantity = string.Empty;
        MonthlyPurchaseUnitCost = string.Empty;
        MonthlyPurchaseDescription = string.Empty;
        SelectedMonthlyPurchaseRow = null;
        ConfirmMonthlyPurchaseDelete = false;
        IsEditingMonthlyPurchase = false;
        suppressMonthlyListDraft = false;
        OnPropertyChanged(nameof(MonthlyPurchaseExpectedTotalText));
    }

    private void ClearInventoryEditForm()
    {
        InventoryEditDate = null;
        InventoryEditName = string.Empty;
        InventoryEditCategory = string.Empty;
        InventoryEditExpectedQuantity = string.Empty;
        InventoryEditExpectedUnitCost = string.Empty;
        InventoryEditListDescription = string.Empty;
        InventoryEditPurchasedQuantity = string.Empty;
        InventoryEditSalePrice = string.Empty;
        InventoryEditDescription = string.Empty;
    }

    private sealed record MonthlyListDraftPayload(
        string Name,
        string Category,
        string Quantity,
        string UnitCost,
        string Description,
        bool IsEditing,
        Guid? EditedItemId);
}

public sealed record InventoryCurrentRow(
    Product Product,
    MonthlyPurchaseItem Plan,
    InventoryMovement PurchaseMovement,
    string Name,
    string Category,
    string ExpectedQuantity,
    string ExpectedUnitCost,
    string ListDescription,
    string PurchasedQuantity,
    string ActualUnitCost,
    string ActualTotal,
    string CurrentQuantity,
    string CurrentValue,
    string SalePrice,
    string InventoryDescription,
    string AddedDate,
    string LastUpdated);

public sealed record InventoryMovementRow(
    InventoryMovement Movement,
    string Date,
    string Type,
    string Product,
    string QuantityIn,
    string QuantityOut,
    string UnitCost,
    string TotalValue,
    string Description);

public sealed record MonthlyPurchaseRow(
    MonthlyPurchaseItem Item,
    string Product,
    string Category,
    string Quantity,
    string UnitPrice,
    string TotalExpected,
    string ActualQuantity,
    string ActualUnitPrice,
    string ActualTotal,
    string CurrentQuantity,
    string State,
    string Description,
    bool RequiresSalePrice,
    string DefaultSalePrice);
