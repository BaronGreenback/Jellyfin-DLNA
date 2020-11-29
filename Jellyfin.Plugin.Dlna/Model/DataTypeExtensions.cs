namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="DataTypeExtensions" />.
    /// </summary>
    public static class DataTypeExtensions
    {
        /// <summary>
        /// Converts a DataType to a string value.
        /// </summary>
        /// <param name="value">The <see cref="DataType"/>.</param>
        /// <returns>The string representation.</returns>
        public static string ToDlnaString(this DataType value)
        {
            return value.ToString().Replace("_", ".", System.StringComparison.Ordinal)[2..];
        }
    }
}
