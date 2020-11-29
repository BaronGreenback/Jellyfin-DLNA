namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="DeviceIcon" />.
    /// </summary>
    public class DeviceIcon
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceIcon"/> class.
        /// </summary>
        /// <param name="serverId">The server's id.</param>
        /// <param name="mimeType">The icon's mime type.</param>
        /// <param name="depth">The icon's depth.</param>
        /// <param name="width">The icon's width.</param>
        /// <param name="height">The icon's height.</param>
        /// <param name="url">The icon's url.</param>
        public DeviceIcon(string serverId, string mimeType, int depth, int width, int height, string url)
        {
            MimeType = mimeType;
            Depth = depth;
            Width = width;
            Height = height;
            Url = "/dlna/" + serverId + url;
        }

        /// <summary>
        /// Gets the Url.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets the MimeType.
        /// </summary>
        public string MimeType { get; }

        /// <summary>
        /// Gets the Width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the Height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the Depth.
        /// </summary>
        public int Depth { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Height}x{Width}";
        }
    }
}
