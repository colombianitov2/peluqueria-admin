using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Reports;

public sealed record FinancialCommitmentCandidate(
    FinancialCommitmentSource SourceType,
    Guid SourceId,
    string Origin,
    string Name,
    DateOnly DueDate,
    long ExpectedMinorUnits,
    long ActualMinorUnits,
    string Status,
    bool IsExcluded,
    string? ExclusionReason);

public sealed record FinancialMonthSnapshot(
    YearMonth Month,
    long CollectedOperatingIncomeMinorUnits,
    long AccountsReceivableMinorUnits,
    long PaidOutflowsMinorUnits,
    long AccountsPayableMinorUnits,
    long NewReservesMinorUnits,
    long CarriedReservesMinorUnits,
    long ReserveAdjustmentsMinorUnits,
    long LoanPaymentsMinorUnits,
    long FinancingReceivedMinorUnits,
    long PriorUncoveredCommitmentsMinorUnits,
    long DistributableResultMinorUnits,
    long BreakEvenMinorUnits,
    long ShortfallMinorUnits,
    long CollaboratorFundMinorUnits,
    long RetainedLocalMinorUnits,
    int GlobalPercentageBasisPoints,
    IReadOnlyList<FinancialCommitmentCandidate> Candidates)
{
    public long SurplusMinorUnits => Math.Max(0, DistributableResultMinorUnits);
}
