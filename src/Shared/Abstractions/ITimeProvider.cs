namespace Shared.Abstractions;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}
