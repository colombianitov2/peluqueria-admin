using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Finance;

public sealed class FinancialEntry : AuditableEntity
{
    private FinancialEntry()
    {
    }

    private FinancialEntry(
        Guid id,
        DateOnly date,
        string concept,
        FinancialEntryType type,
        ExpenseCategory? category,
        Money amount,
        DateTime utcNow) : base(id, utcNow)
    {
        Date = date;
        Concept = NormalizeRequiredText(concept, nameof(concept));
        Type = type;
        Category = category;
        Amount = EnsurePositive(amount);
        ValidateCategory(type, category);
    }

    public DateOnly Date { get; private set; }

    public string Concept { get; private set; } = string.Empty;

    public FinancialEntryType Type { get; private set; }

    public ExpenseCategory? Category { get; private set; }

    public Money Amount { get; private set; }

    public static FinancialEntry CreateIncome(
        DateOnly date,
        string concept,
        Money amount,
        DateTime utcNow) => new(
            Guid.NewGuid(), date, concept, FinancialEntryType.OtherIncome, null, amount, utcNow);

    public static FinancialEntry CreateExpense(
        DateOnly date,
        string concept,
        ExpenseCategory category,
        Money amount,
        DateTime utcNow) => new(
            Guid.NewGuid(), date, concept, FinancialEntryType.Expense, category, amount, utcNow);

    public static FinancialEntry CreateUnexpectedExpense(
        DateOnly date,
        string concept,
        Money amount,
        DateTime utcNow) => new(
            Guid.NewGuid(), date, concept, FinancialEntryType.UnexpectedExpense, null, amount, utcNow);

    public void Update(
        DateOnly date,
        string concept,
        ExpenseCategory? category,
        Money amount,
        DateTime utcNow)
    {
        ValidateCategory(Type, category);
        Date = date;
        Concept = NormalizeRequiredText(concept, nameof(concept));
        Category = category;
        Amount = EnsurePositive(amount);
        MarkUpdated(utcNow);
    }

    private static void ValidateCategory(FinancialEntryType type, ExpenseCategory? category)
    {
        if (type == FinancialEntryType.Expense && !category.HasValue)
        {
            throw new ArgumentException("Un gasto debe tener categoría.", nameof(category));
        }

        if (type != FinancialEntryType.Expense && category.HasValue)
        {
            throw new ArgumentException("La categoría solo corresponde a los gastos.", nameof(category));
        }
    }

    private static Money EnsurePositive(Money amount) => amount.MinorUnits > 0
        ? amount
        : throw new ArgumentOutOfRangeException(nameof(amount), "El monto debe ser mayor que cero.");
}
