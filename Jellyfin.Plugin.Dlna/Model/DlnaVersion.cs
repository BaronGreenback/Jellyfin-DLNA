namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="DlnaVersion"/>.
    /// </summary>
    public enum DlnaVersion
    {
        /// <summary>
        /// Version 1 supported (default).
        /// </summary>
        Version1 = 0,

        /// <summary>
        ///  Version 1.1 supported.
        /// </summary>
        Version1_1 = 1,

        /// <summary>
        /// Version 2.0 supported. (experimental).
        /// </summary>
        Version2 = 2
    }
}
