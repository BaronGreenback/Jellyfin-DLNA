using System.Globalization;

namespace Jellyfin.Plugin.Dlna.Culture
{
    /// <summary>
    /// Helper class for culture info across the code base.
    /// </summary>
    public static class CultureDefault
    {
        /// <summary>
        /// US Culture Info.
        /// </summary>
        public static readonly CultureInfo UsCulture = CultureInfo.ReadOnly(new CultureInfo("en-US"));
    }
}
