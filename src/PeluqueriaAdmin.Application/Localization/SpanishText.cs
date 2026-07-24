using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Obligations;

namespace PeluqueriaAdmin.Application.Localization;

public static class SpanishText
{
    public static string For(ProductCategory value) => value switch
    {
        ProductCategory.FoodOrDrinkForSale => "Alimento o bebida para venta",
        ProductCategory.OtherProductForSale => "Otro producto para venta",
        ProductCategory.CustomerCourtesy => "Cortesía para clientes",
        ProductCategory.Cleaning => "Aseo",
        ProductCategory.LocalSupply => "Insumo del local",
        ProductCategory.OtherLocalProduct => "Otro producto del local",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string For(ExpenseCategory value) => value switch
    {
        ExpenseCategory.MandatorySupply => "Insumo obligatorio",
        ExpenseCategory.OptionalSupply => "Insumo opcional",
        ExpenseCategory.MerchandisePurchase => "Compra de mercancía",
        ExpenseCategory.Other => "Otro gasto",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string For(FinancialEntryType value) => value switch
    {
        FinancialEntryType.OtherIncome => "Otros ingresos",
        FinancialEntryType.Expense => "Gastos",
        FinancialEntryType.UnexpectedExpense => "Imprevistos",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string For(ObligationType value) => value switch
    {
        ObligationType.Service => "Servicio",
        ObligationType.Tax => "Impuesto",
        ObligationType.OtherRecurring => "Otra obligación",
        ObligationType.Credit => "Crédito",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string For(ObligationStatus value) => value switch
    {
        ObligationStatus.Paid => "Pagado",
        ObligationStatus.Pending => "Pendiente",
        ObligationStatus.Partial => "Parcial",
        ObligationStatus.Overdue => "Vencido",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string For(RecurrenceFrequency value) => value switch
    {
        RecurrenceFrequency.None => "Sin recurrencia",
        RecurrenceFrequency.Monthly => "Mensual",
        RecurrenceFrequency.Annual => "Anual",
        RecurrenceFrequency.Weekly => "Semanal",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string For(LoanFrequency value) => value switch
    {
        LoanFrequency.Weekly => "Semanal",
        LoanFrequency.Biweekly => "Quincenal",
        LoanFrequency.Monthly => "Mensual",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
