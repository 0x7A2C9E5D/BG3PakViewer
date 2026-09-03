namespace BG3PakViewer.Services.ExportStrategies;

/// <summary>How a source file may be written to a target format.</summary>
internal enum ExportOperation
{
    /// <summary>Export to this format is not supported for the given source file.</summary>
    Forbidden,
    /// <summary>Copy the source bytes as-is to the target path.</summary>
    RawCopy,
    /// <summary>Decode the source and encode it into the target format.</summary>
    Convert
}
