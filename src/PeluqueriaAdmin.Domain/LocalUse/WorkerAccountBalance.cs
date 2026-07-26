using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed record WorkerAccountBalance(
    Money Debt,
    Money Credit,
    Money TotalCharged,
    Money TotalPaid,
    DateOnly? NextChargeDate,
    Money? NextChargeAmount,
    DateOnly? NextRequiredPaymentDate,
    Money? NextRequiredPaymentAmount,
    DateOnly? CoveredThroughDate);
