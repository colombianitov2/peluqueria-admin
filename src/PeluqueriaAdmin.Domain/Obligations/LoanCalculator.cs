using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Obligations;

public sealed record LoanPlan(
    Loan Loan,
    IReadOnlyList<LoanInstallment> Installments,
    decimal EquivalentMonthlyRatePercent);

public static class LoanCalculator
{
    public static LoanPlan MonthlyBalanceInterest(
        string name,
        Money principal,
        decimal monthlyInterestPercent,
        int installmentCount,
        DateOnly firstDueDate,
        DateTime utcNow,
        string? description = null)
    {
        Validate(principal, installmentCount, firstDueDate, utcNow);
        if (monthlyInterestPercent < 0m)
            throw new ArgumentOutOfRangeException(nameof(monthlyInterestPercent), "El interés mensual no puede ser negativo.");
        int basisPoints = ToBasisPoints(monthlyInterestPercent);
        decimal rate = basisPoints / 10_000m;
        long regularPayment = rate == 0m
            ? RoundMinorUnits((decimal)principal.MinorUnits / installmentCount)
            : RoundMinorUnits(principal.MinorUnits * rate * Pow(1m + rate, installmentCount)
                / (Pow(1m + rate, installmentCount) - 1m));

        Guid loanId = Guid.NewGuid();
        long balance = principal.MinorUnits;
        var installments = new List<LoanInstallment>(installmentCount);
        long total = 0;
        long totalInterest = 0;
        for (int number = 1; number <= installmentCount; number++)
        {
            long interest = rate == 0m ? 0 : RoundMinorUnits(balance * rate);
            long principalPart = number == installmentCount
                ? balance
                : Math.Min(balance, regularPayment - interest);
            if (principalPart <= 0)
                throw new InvalidOperationException("La tasa y el plazo no producen una amortización válida.");
            long amount = checked(principalPart + interest);
            balance -= principalPart;
            total = checked(total + amount);
            totalInterest = checked(totalInterest + interest);
            installments.Add(LoanInstallment.Create(
                loanId,
                number,
                firstDueDate.AddMonths(number - 1),
                amount,
                principalPart,
                interest,
                balance,
                $"Cuota {number} de {installmentCount}",
                utcNow));
        }

        Loan loan = new(
            loanId,
            name,
            principal,
            Money.FromMinorUnits(total),
            Money.FromMinorUnits(totalInterest),
            installments[0].Amount,
            LoanCalculationMethod.MonthlyBalanceInterest,
            basisPoints,
            basisPoints,
            installmentCount,
            firstDueDate,
            utcNow,
            description);
        return new LoanPlan(loan, installments, basisPoints / 100m);
    }

    public static LoanPlan AgreedFinalAmount(
        string name,
        Money principal,
        Money agreedTotal,
        int installmentCount,
        DateOnly firstDueDate,
        DateTime utcNow,
        string? description = null)
    {
        Validate(principal, installmentCount, firstDueDate, utcNow);
        if (agreedTotal.MinorUnits < principal.MinorUnits)
            throw new ArgumentOutOfRangeException(nameof(agreedTotal), "La cantidad final no puede ser inferior al principal.");

        Guid loanId = Guid.NewGuid();
        long baseAmount = agreedTotal.MinorUnits / installmentCount;
        long basePrincipal = principal.MinorUnits / installmentCount;
        long remainingTotal = agreedTotal.MinorUnits;
        long remainingPrincipal = principal.MinorUnits;
        var installments = new List<LoanInstallment>(installmentCount);
        for (int number = 1; number <= installmentCount; number++)
        {
            long amount = number == installmentCount ? remainingTotal : baseAmount;
            long principalPart = number == installmentCount ? remainingPrincipal : basePrincipal;
            long interest = amount - principalPart;
            if (interest < 0)
                throw new InvalidOperationException("El total acordado no permite distribuir capital e interés.");
            remainingTotal -= amount;
            remainingPrincipal -= principalPart;
            installments.Add(LoanInstallment.Create(
                loanId,
                number,
                firstDueDate.AddMonths(number - 1),
                amount,
                principalPart,
                interest,
                remainingPrincipal,
                $"Cuota {number} de {installmentCount}",
                utcNow));
        }

        decimal ratio = (decimal)agreedTotal.MinorUnits / principal.MinorUnits;
        decimal equivalent = (NthRoot(ratio, installmentCount) - 1m) * 100m;
        int equivalentBasisPoints = ToBasisPoints(equivalent);
        Money totalInterest = Money.FromMinorUnits(agreedTotal.MinorUnits - principal.MinorUnits);
        Loan loan = new(
            loanId,
            name,
            principal,
            agreedTotal,
            totalInterest,
            installments[0].Amount,
            LoanCalculationMethod.AgreedFinalAmount,
            0,
            equivalentBasisPoints,
            installmentCount,
            firstDueDate,
            utcNow,
            description);
        return new LoanPlan(loan, installments, equivalentBasisPoints / 100m);
    }

    private static void Validate(Money principal, int installmentCount, DateOnly firstDueDate, DateTime utcNow)
    {
        if (principal.MinorUnits <= 0) throw new ArgumentOutOfRangeException(nameof(principal));
        if (installmentCount <= 0) throw new ArgumentOutOfRangeException(nameof(installmentCount));
        if (firstDueDate == default) throw new ArgumentException("La fecha de la primera cuota es obligatoria.", nameof(firstDueDate));
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha técnica debe estar en UTC.", nameof(utcNow));
    }

    private static int ToBasisPoints(decimal percent)
    {
        decimal value = decimal.Round(percent * 100m, 0, MidpointRounding.AwayFromZero);
        return checked((int)value);
    }

    private static long RoundMinorUnits(decimal value) =>
        checked((long)decimal.Round(value, 0, MidpointRounding.AwayFromZero));

    private static decimal Pow(decimal value, int exponent)
    {
        decimal result = 1m;
        for (int index = 0; index < exponent; index++) result = checked(result * value);
        return result;
    }

    private static decimal NthRoot(decimal value, int n)
    {
        if (value == 0m) return 0m;
        decimal current = value >= 1m ? value : 1m;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            decimal denominator = Pow(current, n - 1);
            if (denominator == 0m) denominator = 0.0000000000000000000000000001m;
            decimal next = ((n - 1m) * current + value / denominator) / n;
            if (Math.Abs(next - current) < 0.000000000000000000000000001m) return next;
            current = next;
        }
        return current;
    }
}
