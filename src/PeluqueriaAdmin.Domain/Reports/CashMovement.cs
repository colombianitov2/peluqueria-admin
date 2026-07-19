namespace PeluqueriaAdmin.Domain.Reports;

public sealed record CashMovement(
    DateOnly Date,
    string Category,
    string Concept,
    long SignedMinorUnits);
