using System.Net.Http;
using CommunityToolkit.Diagnostics;
using Serilog;
using ZeroQL.Client;

namespace BG3PakViewer.Services;

/// <summary>
///     Check update service
/// </summary>
internal class CheckUpdateService : ICheckUpdateService
{
    /// <summary>
    ///     Update url
    /// </summary>
    private readonly Uri _updateUrl = new("https://api.nexusmods.com/v2/graphql");

    /// <summary>
    ///     Check update
    /// </summary>
    /// <returns></returns>
    public async Task<bool> CheckUpdate()
    {
        try
        {
            Log.Information("Checking for updates...");
            var lastestVersion = await FetchLatestVersion();
            var currentVersion = GetCurrentVersion();
            var isUpdateAvailable = lastestVersion > currentVersion;
            Log.Information(
                "Update check completed. Current version: {CurrentVersion}, Latest version: {LatestVersion}, Update available: {IsUpdateAvailable}",
                currentVersion, lastestVersion, isUpdateAvailable);
            return isUpdateAvailable;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates.");
            return false;
        }
    }

    /// <summary>
    ///     Get current version
    /// </summary>
    /// <returns></returns>
    private static Version GetCurrentVersion()
    {
        Guard.IsTrue(Version.TryParse(ThisAssembly.AssemblyFileVersion,
            out var currentVersion));
        Guard.IsNotNull(currentVersion);
        return currentVersion;
    }

    /// <summary>
    ///     Fetch latest version
    /// </summary>
    /// <returns></returns>
    private async Task<Version> FetchLatestVersion()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = _updateUrl;
        using var zeroQlClient = new ZeroQLClient(httpClient);
        var filter = new ModsFilter
        {
            Name =
            [
                new BaseFilterValueEqualsWildcard
                {
                    Op = new FilterComparisonOperatorEqualsWildcard(),
                    Value = "Baldur's Gate 3 Pak Viewer"
                }
            ]
        };
        var result =
            (await zeroQlClient.Query(q => q.Mods<string[]>(filter: filter,
                selector: p => p.Nodes(m => m.Version)))).Data;
        Guard.IsNotNull(result);
        Guard.IsNotEmpty(result);
        Guard.IsTrue(Version.TryParse(result[0], out var latestVersion));
        Guard.IsNotNull(latestVersion);
        return latestVersion;
    }
}