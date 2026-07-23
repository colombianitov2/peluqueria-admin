using PeluqueriaAdmin.Domain.LocalUse;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed record WorkerRow(
    LocalUsePerson Worker,
    string Name,
    string EntryDate,
    string Chair,
    string Debt,
    string Credit,
    string NextCharge,
    string NextRequiredPayment,
    string State);

public sealed record ChairRow(
    Chair Chair,
    string Name,
    string CreationDate,
    string Worker,
    string Description,
    string State);
