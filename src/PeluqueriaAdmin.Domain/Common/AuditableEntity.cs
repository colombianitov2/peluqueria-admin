namespace PeluqueriaAdmin.Domain.Common;

public abstract class AuditableEntity
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid id, DateTime utcNow)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador no puede estar vacío.", nameof(id));
        }

        EnsureUtc(utcNow);
        Id = id;
        CreatedUtc = utcNow;
        UpdatedUtc = utcNow;
    }

    public Guid Id { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public DateTime? DeletedUtc { get; private set; }

    public bool IsDeleted => DeletedUtc.HasValue;

    public void MarkDeleted(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            return;
        }

        DeletedUtc = utcNow;
        UpdatedUtc = utcNow;
    }

    protected void MarkUpdated(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            throw new InvalidOperationException("No se puede modificar un registro eliminado.");
        }

        UpdatedUtc = utcNow;
    }

    protected static string NormalizeRequiredText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    protected static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha técnica debe estar expresada en UTC.", nameof(value));
        }
    }
}
