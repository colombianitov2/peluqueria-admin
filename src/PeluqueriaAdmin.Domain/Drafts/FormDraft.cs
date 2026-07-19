namespace PeluqueriaAdmin.Domain.Drafts;

public sealed class FormDraft
{
    private FormDraft()
    {
    }

    private FormDraft(
        string key,
        string module,
        string formType,
        string payloadJson,
        Guid? entityId,
        bool isEdit,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Key = Normalize(key, nameof(key), 300);
        Module = Normalize(module, nameof(module), 100);
        FormType = Normalize(formType, nameof(formType), 100);
        PayloadJson = Normalize(payloadJson, nameof(payloadJson), 20_000);
        EntityId = entityId;
        IsEdit = isEdit;
        CreatedUtc = utcNow;
        UpdatedUtc = utcNow;
    }

    public string Key { get; private set; } = string.Empty;

    public string Module { get; private set; } = string.Empty;

    public string FormType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public bool IsEdit { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public static FormDraft Create(
        string key,
        string module,
        string formType,
        string payloadJson,
        Guid? entityId,
        bool isEdit,
        DateTime utcNow) => new(key, module, formType, payloadJson, entityId, isEdit, utcNow);

    public void UpdatePayload(string payloadJson, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        PayloadJson = Normalize(payloadJson, nameof(payloadJson), 20_000);
        UpdatedUtc = utcNow;
    }

    private static string Normalize(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{parameterName} supera la longitud permitida.", parameterName);
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha técnica debe estar expresada en UTC.", nameof(value));
        }
    }
}
