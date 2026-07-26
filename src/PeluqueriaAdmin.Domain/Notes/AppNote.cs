namespace PeluqueriaAdmin.Domain.Notes;

public sealed class AppNote
{
    public const int SingletonId = 1;

    private AppNote()
    {
    }

    private AppNote(string content, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Id = SingletonId;
        Content = content ?? string.Empty;
        UpdatedUtc = utcNow;
    }

    public int Id { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public DateTime UpdatedUtc { get; private set; }

    public static AppNote Create(string content, DateTime utcNow) => new(content, utcNow);

    public void Update(string content, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Content = content ?? string.Empty;
        UpdatedUtc = utcNow;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha técnica debe estar expresada en UTC.", nameof(value));
        }
    }
}
