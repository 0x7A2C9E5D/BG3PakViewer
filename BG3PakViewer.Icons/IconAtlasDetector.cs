using System.Runtime.InteropServices;
using BG3PakViewer.Utils;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Icons;

/// <summary>
///     The grid of an icon atlas: the spacing between two neighboring grid lines on each axis.
/// </summary>
/// <param name="CellWidth">Horizontal spacing between two grid lines, in pixels.</param>
/// <param name="CellHeight">Vertical spacing between two grid lines, in pixels.</param>
/// <remarks>
///     The two axes are measured independently because icon cells are not necessarily square;
///     character portraits for instance are taller than they are wide.
/// </remarks>
public readonly record struct IconAtlasGrid(int CellWidth, int CellHeight);

/// <summary>
///     Detects BG3 icon atlases and the grid they lay their icons out on.
/// </summary>
/// <remarks>
///     Icon atlases (<c>Icons_Items.dds</c>, <c>Portraits_Gustav.dds</c>, …) pack their images on a
///     regular grid, but the grid itself is not stored in the DDS: only the content shows it. The
///     icons are separated by either transparent gutters or a plain seam where two pictures meet, so
///     the grid is recovered by extracting edges (Canny), projecting them onto each axis and then
///     looking for the spacing at which the projected edge energy lines up best.
/// </remarks>
public static class IconAtlasDetector
{
    /// <summary>
    ///     Smallest grid spacing considered, in pixels.
    /// </summary>
    private const int MinCellSize = 8;

    /// <summary>
    ///     Largest grid spacing considered, in pixels. Icon atlases do not use bigger cells.
    /// </summary>
    private const int MaxCellSize = 512;

    /// <summary>
    ///     How many grid lines a spacing has to produce before it is trusted.
    /// </summary>
    private const int MinGridLines = 2;

    /// <summary>
    ///     Hysteresis thresholds handed to <see cref="Cv2.Canny" />.
    /// </summary>
    private const double CannyLowThreshold = 50;

    /// <summary>
    ///     Hysteresis thresholds handed to <see cref="Cv2.Canny" />.
    /// </summary>
    private const double CannyHighThreshold = 150;

    /// <summary>
    ///     A lag correlating this fraction of the best lag still describes the same grid, so the search
    ///     settles on the smallest such lag instead of one of its multiples.
    /// </summary>
    private const double PitchTolerance = 0.7;

    /// <summary>
    ///     How periodic a profile has to be before its strongest lag is accepted as a grid. Flat
    ///     profiles (a single picture) score far below this.
    /// </summary>
    private const double MinPeriodicity = 0.15;

    /// <summary>
    ///     Determines whether the given package path can hold an icon atlas.
    /// </summary>
    /// <param name="fullPath">The full path of the file inside the package.</param>
    /// <returns><c>true</c> when the file is a texture that may hold an icon atlas.</returns>
    public static bool IsIconAtlas(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        var normalized = fullPath.Replace('\\', '/');
        if (!FileExtensions.IsTextureFormat(Path.GetExtension(normalized))) return false;

        if (normalized.Contains("Textures/Icons/", StringComparison.OrdinalIgnoreCase)) return true;

        var fileName = Path.GetFileName(normalized);
        return fileName.StartsWith("Icons_", StringComparison.OrdinalIgnoreCase)
               || fileName.Contains("Portraits", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Detects the icon grid of an atlas image.
    /// </summary>
    /// <param name="image">The decoded atlas image.</param>
    /// <returns>The grid spacing of both axes, or <c>null</c> when the image shows no grid.</returns>
    public static IconAtlasGrid? DetectGrid(Image image)
    {
        var width = image.Width;
        var height = image.Height;
        if (width < MinCellSize * MinGridLines || height < MinCellSize * MinGridLines) return null;

        using var bgra = ToMat(image);
        using var edges = BuildEdgeMap(bgra);

        var cellWidth = FindSpacing(ColumnProfile(edges, width), width);
        var cellHeight = FindSpacing(RowProfile(edges, height), height);

        if (cellWidth is null || cellHeight is null) return null;
        return new IconAtlasGrid(cellWidth.Value, cellHeight.Value);
    }

    /// <summary>
    ///     Copies the pixels of an ImageSharp image into an OpenCV matrix.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <returns>A <c>CV_8UC4</c> matrix holding the image, in BGRA order.</returns>
    private static Mat ToMat(Image image)
    {
        var bgra = image as Image<Bgra32> ?? image.CloneAs<Bgra32>();

        try
        {
            var width = bgra.Width;
            var height = bgra.Height;
            var mat = new Mat(height, width, MatType.CV_8UC4);
            var rowBytes = new byte[width * 4];

            bgra.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    MemoryMarshal.AsBytes(accessor.GetRowSpan(y)).CopyTo(rowBytes);
                    Marshal.Copy(rowBytes, 0, IntPtr.Add(mat.Data, (int)(y * mat.Step())), rowBytes.Length);
                }
            });

            return mat;
        }
        finally
        {
            if (!ReferenceEquals(bgra, image)) bgra.Dispose();
        }
    }

    /// <summary>
    ///     Extracts the edges of an image, both from its luminance and from its alpha channel.
    /// </summary>
    /// <param name="bgra">The image to analyze.</param>
    /// <returns>A binary edge map.</returns>
    /// <remarks>
    ///     The gutters between icons are usually fully transparent, and the color channels under a
    ///     transparent pixel often hold stale data, so the alpha channel shows seams that the
    ///     luminance does not.
    /// </remarks>
    private static Mat BuildEdgeMap(Mat bgra)
    {
        // All the bitmaps handed to Canny are 8 bit single channel, so their size is predictable.
        // The luma edge map is produced last so that the greyscale image can be released early.
        var alpha = new Mat();
        Cv2.ExtractChannel(bgra, alpha, 3);

        var alphaEdges = new Mat();
        Cv2.Canny(alpha, alphaEdges, CannyLowThreshold, CannyHighThreshold);
        alpha.Dispose();

        var grayEdges = new Mat();
        using (var gray = new Mat())
        {
            Cv2.CvtColor(bgra, gray, ColorConversionCodes.BGRA2GRAY);
            Cv2.Canny(gray, grayEdges, CannyLowThreshold, CannyHighThreshold);
        }

        var edges = new Mat();
        Cv2.BitwiseOr(grayEdges, alphaEdges, edges);
        grayEdges.Dispose();
        alphaEdges.Dispose();

        return edges;
    }

    /// <summary>
    ///     Sums the edge map along the rows, giving the edge energy of every column.
    /// </summary>
    /// <param name="edges">The edge map.</param>
    /// <param name="width">The width of the edge map.</param>
    /// <returns>The per-column edge energy.</returns>
    private static int[] ColumnProfile(Mat edges, int width)
    {
        using var sums = new Mat();
        Cv2.Reduce(edges, sums, ReduceDimension.Row, ReduceTypes.Sum, MatType.CV_32S);

        var profile = new int[width];
        for (var x = 0; x < width; x++) profile[x] = sums.At<int>(0, x);
        return profile;
    }

    /// <summary>
    ///     Sums the edge map along the columns, giving the edge energy of every row.
    /// </summary>
    /// <param name="edges">The edge map.</param>
    /// <param name="height">The height of the edge map.</param>
    /// <returns>The per-row edge energy.</returns>
    private static int[] RowProfile(Mat edges, int height)
    {
        using var sums = new Mat();
        Cv2.Reduce(edges, sums, ReduceDimension.Column, ReduceTypes.Sum, MatType.CV_32S);

        var profile = new int[height];
        for (var y = 0; y < height; y++) profile[y] = sums.At<int>(y, 0);
        return profile;
    }

    /// <summary>
    ///     Finds the spacing of the grid lines in an edge energy profile.
    /// </summary>
    /// <param name="profile">The per-column or per-row edge energy.</param>
    /// <param name="length">The number of columns or rows.</param>
    /// <returns>The spacing in pixels, or <c>null</c> when no grid stands out.</returns>
    /// <remarks>
    ///     Icons are separated either by transparent gutters or by a bare seam, and the artwork itself
    ///     is far too busy for the seam to stand out in the raw profile: on a dense icon atlas a fifth of
    ///     all pixels are Canny edges, so a single seam column is not measurably above average. What is
    ///     measurable is the <em>repetition</em> of the seams, so the search is driven by the
    ///     autocorrelation of the profile rather than by its amplitude.
    /// </remarks>
    private static int? FindSpacing(int[] profile, int length)
    {
        if (length < MinCellSize * MinGridLines) return null;

        // Neighboring rows/columns of an atlas are nearly identical away from a seam, so the
        // difference of the profile cancels the artwork and leaves the seam as a spike. Without this
        // a slowly varying profile correlates best with itself at the smallest lag, which is not a grid.
        var difference = new int[length - 1];
        for (var i = 0; i < difference.Length; i++) difference[i] = profile[i + 1] - profile[i];

        var maxSpacing = Math.Min(length / 2, MaxCellSize);

        var correlation = Autocorrelate(difference, maxSpacing);

        var best = 0.0;
        for (var lag = MinCellSize; lag <= maxSpacing; lag++)
            if (correlation[lag] > best)
                best = correlation[lag];

        if (best < MinPeriodicity) return null;

        // The pitch is the *smallest* lag at which the profile genuinely repeats. A longer lag that also
        // repeats is one of its multiples rather than the pitch itself - a color scheme that alternates
        // every other portrait makes the correlation at twice the pitch stronger than at the pitch - so
        // taking the strongest lag would fuse neighboring icons into one cell.
        var threshold = best * PitchTolerance;
        for (var spacing = MinCellSize; spacing <= maxSpacing; spacing++)
        {
            if (correlation[spacing] < threshold) continue;

            // A shoulder of a wider peak is not a period; the lag has to be a peak of its own.
            if (spacing > MinCellSize && correlation[spacing] < correlation[spacing - 1]) continue;
            if (spacing < maxSpacing && correlation[spacing] < correlation[spacing + 1]) continue;
            if (length / spacing < MinGridLines) continue;

            return spacing;
        }

        return null;
    }

    /// <summary>
    ///     Normalised autocorrelation of a profile.
    /// </summary>
    /// <param name="profile">The profile to correlate with itself.</param>
    /// <param name="maxLag">The largest lag to compute.</param>
    /// <returns>The correlation for every lag up to <paramref name="maxLag" />, where 1 means the profile repeats exactly.</returns>
    private static double[] Autocorrelate(int[] profile, int maxLag)
    {
        var mean = profile.Average();
        var centered = new double[profile.Length];
        double energy = 0;
        for (var i = 0; i < profile.Length; i++)
        {
            centered[i] = profile[i] - mean;
            energy += centered[i] * centered[i];
        }

        var correlation = new double[maxLag + 1];
        if (energy <= 0) return correlation;

        for (var lag = 1; lag <= maxLag; lag++)
        {
            double sum = 0;
            for (var i = 0; i + lag < profile.Length; i++) sum += centered[i] * centered[i + lag];
            correlation[lag] = sum / energy;
        }

        return correlation;
    }
}