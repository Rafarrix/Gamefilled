namespace Gamefilled.Application.Games;

public interface IGameDiscoveryService
{
    Task<GameDiscoveryResult> SearchAsync(
        GameDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameFilterOption>> GetPlatformsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameFilterOption>> GetGenresAsync(
        CancellationToken cancellationToken = default);
}
