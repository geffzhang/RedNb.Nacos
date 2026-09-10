namespace RedNb.Nacos.Core.Maintainer;

/// <summary>
/// Nacos configuration history maintainer interface.
/// </summary>
[Obsolete(
    "Nacos 3.2+ removed v1/v2 HTTP endpoints used by this service. " +
    "Calls will return HTTP 404 at runtime. " +
    "Migrate to Nacos 2.x server or use the nacos-api-legacy-adapter JAR. " +
    "Tracked for removal in the next major version.",
    error: false)]
public interface IConfigHistoryMaintainer
{
    /// <summary>
    /// Lists configuration history.
    /// </summary>
    /// <param name="dataId">Data ID.</param>
    /// <param name="group">Group name.</param>
    /// <param name="namespaceId">Namespace ID.</param>
    /// <param name="pageNo">Page number.</param>
    /// <param name="pageSize">Page size (max 500).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of configuration history.</returns>
    Task<Page<ConfigHistoryBasicInfo>> ListConfigHistoryAsync(
        string dataId,
        string group,
        string namespaceId,
        int pageNo = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configuration history detail.
    /// </summary>
    /// <param name="dataId">Data ID.</param>
    /// <param name="group">Group name.</param>
    /// <param name="namespaceId">Namespace ID.</param>
    /// <param name="nid">History record ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Configuration history detail.</returns>
    Task<ConfigHistoryDetailInfo?> GetConfigHistoryInfoAsync(
        string dataId,
        string group,
        string namespaceId,
        long nid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets previous configuration history.
    /// </summary>
    /// <param name="dataId">Data ID.</param>
    /// <param name="group">Group name.</param>
    /// <param name="namespaceId">Namespace ID.</param>
    /// <param name="id">Current history record ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Previous configuration history detail.</returns>
    Task<ConfigHistoryDetailInfo?> GetPreviousConfigHistoryInfoAsync(
        string dataId,
        string group,
        string namespaceId,
        long id,
        CancellationToken cancellationToken = default);
}
