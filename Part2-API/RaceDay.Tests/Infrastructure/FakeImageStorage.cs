using RaceDay.Api.Services;

namespace RaceDay.Tests.Infrastructure;

/// <summary>Stands in for Azure Blob Storage in the tests. It keeps the URLs it handed out.</summary>
public class FakeImageStorage : IImageStorage
{
    public List<string> Saved { get; } = new();

    public Task<string> SaveAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default)
    {
        var url = $"https://fake-storage.raceday.test/{folder}/{Guid.NewGuid():N}{extension}";
        Saved.Add(url);
        return Task.FromResult(url);
    }
}
