using System.Globalization;

namespace BG3PakViewer.Locales;

/// <summary>
///     CultureMatcher
/// </summary>
public class CultureMatcher : ICultureMatcher
{
    /// <summary>
    ///     Matches a target culture to available cultures.
    /// </summary>
    /// <param name="targetCulture"></param>
    /// <param name="availableCultures"></param>
    /// <returns></returns>
    public IEnumerable<CultureInfo> Matches(CultureInfo targetCulture, IReadOnlyList<CultureInfo> availableCultures)
    {
        var bestMatches = availableCultures
            .Where(x => x.Name == targetCulture.Name).ToArray();
        if (bestMatches.Length != 0) return bestMatches;
        var targetParentName = targetCulture.Parent.Name;
        bestMatches =
        [
            .. availableCultures
                .Where(x => x.Name == targetParentName)
        ];
        if (bestMatches.Length != 0) return bestMatches;
        bestMatches =
        [
            .. availableCultures
                .Where(x => x.Parent.Name == targetParentName)
        ];
        return bestMatches.Length != 0 ? bestMatches : [];
    }
}