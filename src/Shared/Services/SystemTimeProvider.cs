using Shared.Abstractions;

namespace Shared.Services;

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
