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
        var parentName = targetCulture.Parent.Name;

        return Find(x => x.Name == targetCulture.Name)
               ?? Find(x => x.Name == parentName)
               ?? Find(x => x.Parent.Name == parentName)
               ?? [];

        IEnumerable<CultureInfo>? Find(Func<CultureInfo, bool> predicate)
        {
            var matches = availableCultures.Where(predicate).ToArray();
            return matches.Length != 0 ? matches : null;
        }
    }
}