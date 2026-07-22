using PeluqueriaAdmin.Domain.Collaborators;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed record CollaboratorRow(
    Collaborator Collaborator,
    string Name,
    string StartDate,
    string ExitDate,
    string State,
    string Description,
    string ProfitShare,
    string AssignedAmount);

public sealed record ContributionRow(
    CollaboratorContribution Contribution,
    string Date,
    string Amount,
    string Description,
    string State);
