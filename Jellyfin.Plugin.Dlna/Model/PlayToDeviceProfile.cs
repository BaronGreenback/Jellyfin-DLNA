using System.Net;
using MediaBrowser.Model.Dlna;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="PlayToDeviceProfile" />.
    /// </summary>
    public class PlayToDeviceProfile : DeviceProfile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlayToDeviceProfile"/> class.
        /// </summary>
        /// <param name="name">The name of the device.</param>
        /// <param name="baseUrl">Base url of the device.</param>
        /// <param name="uuid">UUID of the device.</param>
        /// <param name="address">Address of the device.</param>
        public PlayToDeviceProfile(string name, string baseUrl, string uuid, string address)
        {
            BaseUrl = baseUrl;
            Uuid = uuid;
            Name = name;
            Address = address;
        }

        /// <summary>
        /// Gets the UUID.
        /// </summary>
        public string Uuid { get; }

        /// <summary>
        /// Gets the Base Url.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Gets the services the device supports.
        /// </summary>
        public DeviceService?[] Services { get; } = { null, null, null };
    }
}
