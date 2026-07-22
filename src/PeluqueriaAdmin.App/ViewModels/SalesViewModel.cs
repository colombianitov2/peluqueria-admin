using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Activity;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class SalesViewModel(
    AdministrationService service,
    GetSettingsUseCase getSettings,
    IFormDraftStore drafts,
    TimeProvider timeProvider) : ObservableObject
{
    private const string DraftKey = "Ventas:Fase43:registro";
    private readonly List<SalesProductOption> allProducts = [];
    private CancellationTokenSource? draftCancellation;
    private bool suppressSearch;

    public ObservableCollection<SalesProductOption> FilteredProducts { get; } = [];
    public ObservableCollection<SaleHistoryRow> Sales { get; } = [];
    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Rango personalizado"];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private SalesProductOption? selectedProduct;
    [ObservableProperty] private bool isProductDropDownOpen;
    [ObservableProperty] private bool hasNoProducts;
    [ObservableProperty] private DateTime? saleDate = DateTime.Today;
    [ObservableProperty] private string quantityText = string.Empty;
    [ObservableProperty] private string unitPriceText = string.Empty;
    [ObservableProperty] private string descriptionText = string.Empty;
    [ObservableProperty] private string selectedAvailability = "Selecciona un producto";
    [ObservableProperty] private string calculatedTotal = string.Empty;
    [ObservableProperty] private string selectedPeriod = "Este mes";
    [ObservableProperty] private DateTime? customPeriodFrom = DateTime.Today;
    [ObservableProperty] private DateTime? customPeriodThrough = DateTime.Today;
    [ObservableProperty] private bool showCustomPeriod;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;
    [ObservableProperty] private bool isBusy;

    public async Task LoadAsync()
    {
        suppressSearch = true;
        SearchText = string.Empty;
        SelectedProduct = null;
        suppressSearch = false;
        await RefreshAsync();
        await RestoreDraftAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            AdministrationData data = await service.LoadAsync();
            SettingsDto settings = await getSettings.ExecuteAsync();
            allProducts.Clear();
            foreach (Product product in data.Products.Where(item => item.IsForSale).OrderBy(item => item.Name))
            {
                InventoryMovement[] movements = data.InventoryMovements.Where(item => item.ProductId == product.Id).ToArray();
                allProducts.Add(new SalesProductOption(
                    product,
                    product.Name,
                    InventoryCalculator.CurrentQuantity(movements),
                    product.DefaultSalePrice?.ToDecimal()));
            }
            ApplyProductFilter();

            ActivityDateRange range = CurrentRange();
            Sales.Clear();
            foreach (InventoryMovement sale in data.InventoryMovements
                .Where(item => item.Type == InventoryMovementType.Sale && range.Contains(item.Date))
                .OrderByDescending(item => item.Date).ThenByDescending(item => item.CreatedUtc))
            {
                Product? product = data.Products.SingleOrDefault(item => item.Id == sale.ProductId);
                decimal quantity = Math.Abs(sale.QuantityDelta);
                decimal total = sale.CashAmount?.ToDecimal() ?? 0m;
                Sales.Add(new SaleHistoryRow(
                    sale,
                    sale.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    product?.Name ?? "Producto eliminado",
                    quantity.ToString("0.###", CultureInfo.CurrentCulture),
                    quantity == 0 ? string.Empty : $"{ApplicationCurrency.Code} {total / quantity:N2}",
                    $"{ApplicationCurrency.Code} {total:N2}",
                    sale.Description ?? string.Empty));
            }
            StatusMessage = Sales.Count == 0 ? "Sin ventas registradas en el periodo seleccionado." : string.Empty;
            IsError = false;
            UpdateSelectedProductDetails();
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible cargar Ventas. {exception.Message}";
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterSaleAsync()
    {
        if (SelectedProduct is null)
        {
            StatusMessage = "Selecciona un producto para registrar la venta.";
            IsError = true;
            return;
        }
        IsBusy = true;
        try
        {
            await service.RegisterSaleAsync(
                SelectedProduct.Product.Id,
                RequiredDate(SaleDate),
                Quantity.Positive(ParsePositiveDecimal(QuantityText, "cantidad")),
                Money.FromDecimal(ParsePositiveDecimal(UnitPriceText, "precio")),
                DescriptionText,
                completedDraftKey: DraftKey);
            QuantityText = string.Empty;
            DescriptionText = string.Empty;
            StatusMessage = "La venta se registró correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearFormAsync()
    {
        suppressSearch = true;
        SearchText = string.Empty;
        SelectedProduct = null;
        suppressSearch = false;
        SaleDate = DateTime.Today;
        QuantityText = string.Empty;
        UnitPriceText = string.Empty;
        DescriptionText = string.Empty;
        ApplyProductFilter();
        await drafts.DeleteAsync(DraftKey);
    }

    public async Task FlushPendingAsync()
    {
        draftCancellation?.Cancel();
        await PersistDraftAsync();
    }

    public static bool MatchesProductSearch(string productName, string search) =>
        string.IsNullOrWhiteSpace(search) || Normalize(productName).Contains(Normalize(search), StringComparison.Ordinal);

    private void ApplyProductFilter()
    {
        FilteredProducts.Clear();
        foreach (SalesProductOption option in allProducts.Where(item => MatchesProductSearch(item.Name, SearchText)))
        {
            FilteredProducts.Add(option);
        }
        HasNoProducts = FilteredProducts.Count == 0;
    }

    private void UpdateSelectedProductDetails()
    {
        if (SelectedProduct is null)
        {
            SelectedAvailability = "Selecciona un producto";
            UnitPriceText = string.Empty;
            CalculatedTotal = string.Empty;
            return;
        }
        SelectedAvailability = $"Existencia disponible: {SelectedProduct.AvailableQuantity:0.###}";
        UnitPriceText = SelectedProduct.DefaultPrice?.ToString("0.00", CultureInfo.CurrentCulture) ?? "Sin precio configurado";
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        bool validQuantity = TryParseDecimal(QuantityText, out decimal quantity)
            && quantity > 0m
            && SelectedProduct is not null
            && quantity <= SelectedProduct.AvailableQuantity;
        bool validPrice = TryParseDecimal(UnitPriceText, out decimal price) && price > 0m;
        if (!validQuantity || !validPrice)
        {
            CalculatedTotal = string.Empty;
            return;
        }

        Money unitPrice = Money.FromDecimal(price);
        Money total = CalculateSaleTotal(unitPrice, Quantity.Positive(quantity));
        CalculatedTotal = $"Total: {ApplicationCurrency.Code} {total.ToDecimal():N2}";
    }

    private ActivityDateRange CurrentRange()
    {
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
        return ActivityPeriodCalculator.Calculate(
            period, today,
            CustomPeriodFrom.HasValue ? DateOnly.FromDateTime(CustomPeriodFrom.Value) : null,
            CustomPeriodThrough.HasValue ? DateOnly.FromDateTime(CustomPeriodThrough.Value) : null);
    }

    private void ScheduleDraft()
    {
        draftCancellation?.Cancel();
        draftCancellation = new CancellationTokenSource();
        _ = PersistDraftAfterDelayAsync(draftCancellation.Token);
    }

    private async Task PersistDraftAfterDelayAsync(CancellationToken cancellationToken)
    {
        try { await Task.Delay(650, cancellationToken); await PersistDraftAsync(cancellationToken); }
        catch (OperationCanceledException) { }
    }

    private async Task PersistDraftAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProduct is null && string.IsNullOrWhiteSpace(QuantityText) && string.IsNullOrWhiteSpace(DescriptionText))
        {
            await drafts.DeleteAsync(DraftKey, cancellationToken);
            return;
        }
        string json = JsonSerializer.Serialize(new SaleDraft(
            SelectedProduct?.Product.Id, SaleDate, QuantityText, UnitPriceText, DescriptionText));
        FormDraft? existing = await drafts.FindAsync(DraftKey, cancellationToken);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        if (existing is null)
        {
            await drafts.UpsertAsync(FormDraft.Create(DraftKey, "Ventas", "Registrar venta", json, null, false, utcNow), cancellationToken);
        }
        else
        {
            existing.UpdatePayload(json, utcNow);
            await drafts.UpsertAsync(existing, cancellationToken);
        }
    }

    private async Task RestoreDraftAsync()
    {
        FormDraft? draft = await drafts.FindAsync(DraftKey);
        if (draft is null) return;
        SaleDraft? payload = JsonSerializer.Deserialize<SaleDraft>(draft.PayloadJson);
        if (payload is null) return;
        suppressSearch = true;
        SelectedProduct = payload.ProductId.HasValue ? allProducts.SingleOrDefault(item => item.Product.Id == payload.ProductId.Value) : null;
        SearchText = SelectedProduct?.Name ?? string.Empty;
        SaleDate = payload.Date;
        QuantityText = payload.Quantity;
        DescriptionText = payload.Description;
        suppressSearch = false;
        ApplyProductFilter();
        UpdateSelectedProductDetails();
        if (!string.IsNullOrWhiteSpace(payload.UnitPrice))
        {
            UnitPriceText = payload.UnitPrice;
            UpdateTotal();
        }
        StatusMessage = "Se recuperó un borrador de venta sin registrar.";
    }

    partial void OnSearchTextChanged(string value)
    {
        if (suppressSearch) return;
        SelectedProduct = null;
        ApplyProductFilter();
        IsProductDropDownOpen = true;
    }
    partial void OnSelectedProductChanged(SalesProductOption? value)
    {
        if (value is not null)
        {
            suppressSearch = true;
            SearchText = value.Name;
            suppressSearch = false;
        }
        UpdateSelectedProductDetails();
        ScheduleDraft();
    }
    partial void OnQuantityTextChanged(string value) { UpdateTotal(); ScheduleDraft(); }
    partial void OnUnitPriceTextChanged(string value) { UpdateTotal(); ScheduleDraft(); }
    partial void OnSaleDateChanged(DateTime? value) => ScheduleDraft();
    partial void OnDescriptionTextChanged(string value) => ScheduleDraft();
    partial void OnSelectedPeriodChanged(string value) { ShowCustomPeriod = value == "Rango personalizado"; _ = RefreshAsync(); }
    partial void OnCustomPeriodFromChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }
    partial void OnCustomPeriodThroughChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }

    private static DateOnly RequiredDate(DateTime? value) => value.HasValue
        ? DateOnly.FromDateTime(value.Value)
        : throw new ArgumentException("La fecha es obligatoria.");

    private static decimal ParsePositiveDecimal(string value, string field)
    {
        bool valid = TryParseSaleDecimal(value, out decimal number);
        return valid && number > 0 ? number : throw new ArgumentException($"La {field} debe ser mayor que cero.");
    }

    private static bool TryParseDecimal(string value, out decimal number) => TryParseSaleDecimal(value, out number);

    public static bool TryParseSaleDecimal(string value, out decimal number)
    {
        string normalized = value.Trim();
        int lastComma = normalized.LastIndexOf(',');
        int lastDot = normalized.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            char decimalSeparator = lastComma > lastDot ? ',' : '.';
            char groupSeparator = decimalSeparator == ',' ? '.' : ',';
            normalized = normalized.Replace(groupSeparator.ToString(), string.Empty, StringComparison.Ordinal)
                .Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0 || lastDot >= 0)
        {
            char decimalSeparator = lastComma >= 0 ? ',' : '.';
            string[] parts = normalized.Split(decimalSeparator);
            normalized = parts.Length == 2
                ? $"{parts[0]}.{parts[1]}"
                : string.Empty;
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);
    }

    public static Money CalculateSaleTotal(Money unitPrice, Quantity quantity) =>
        Money.FromMinorUnits(checked((long)decimal.Round(
            unitPrice.MinorUnits * quantity.Value,
            0,
            MidpointRounding.AwayFromZero)));

    private static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (char character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(char.ToUpperInvariant(character));
            }
        }
        return result.ToString();
    }

    private sealed record SaleDraft(Guid? ProductId, DateTime? Date, string Quantity, string UnitPrice, string Description);
}

public sealed record SalesProductOption(Product Product, string Name, decimal AvailableQuantity, decimal? DefaultPrice)
{
    public string Display => Name;
}

public sealed record SaleHistoryRow(
    InventoryMovement Sale,
    string Date,
    string Product,
    string Quantity,
    string UnitPrice,
    string Total,
    string Description);
