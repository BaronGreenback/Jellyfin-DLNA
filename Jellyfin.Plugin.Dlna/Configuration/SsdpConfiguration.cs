using System;
using System.Xml.Serialization;
using Jellyfin.Plugin.Dlna.Model;

namespace Jellyfin.Plugin.Dlna.Configuration
{
    /// <summary>
    /// Defines the <see cref="SsdpConfiguration" />.
    /// </summary>
    public class SsdpConfiguration
    {
        private string _userAgent = "DLNADOC/1.50 UPnP/{DlnaVersion} Jellyfin/{AppVersion}";
        private string? _userAgentCache;

        /// <summary>
        /// Gets or sets the Jellyfin version to use.
        /// </summary>
        [XmlIgnore]
        public static string JellyfinVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the USERAGENT that is sent to devices.
        /// </summary>
        public string UserAgent
        {
            get => _userAgent;
            set
            {
                _userAgent = value;
                _userAgentCache = null;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the default icon width.
        /// </summary>
        public int DefaultIconWidth { get; set; } = 48;

        /// <summary>
        /// Gets or sets a value indicating the default icon height.
        /// </summary>
        public int DefaultIconHeight { get; set; } = 48;

        /// <summary>
        /// Gets or sets a value indicating whether detailed SSDP logs are sent to the console/log.
        /// "Emby.Dlna": "Debug" must be set in logging.default.json for this property to have any effect.
        /// </summary>
        public bool EnableSsdpTracing { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an IP address is to be used to filter the detailed SSDP logs
        /// that are being sent to the console/log.
        /// If the setting "Emby.Dlna": "Debug" must be set in logging.default.json for this property to work.
        /// Shared with PlayTo plugin.
        /// </summary>
        public string SsdpTracingFilter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of times SSDP UDP messages are sent.
        /// </summary>
        public int UdpSendCount { get; set; } = 2;

        /// <summary>
        /// Gets or sets the range of UDP ports to use in communications as well as 1900.
        /// Default is the dynamic port range.
        /// </summary>
        public string UdpPortRange { get; set; } = "49152-65535";

        /// <summary>
        /// Gets or sets the Dlna version that the SSDP server supports. <see cref="DlnaVersion"/>.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether multi-socket binding is enabled.
        /// </summary>
        public bool EnableMultiSocketBinding { get; set; }

        /// <summary>
        /// Gets or sets the list of device IPs/subnets which are permitted to connect, or are explicitly denied.
        /// </summary>
        public string[] PermittedDevices { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the Dlna version that the SSDP server supports.
        /// </summary>
        [XmlIgnore]
        public DlnaVersion DlnaVersion
        {
            get => (DlnaVersion)Version;
            set => Version = (int)value;
        }

        /// <summary>
        /// Gets the parsed UserAgent value.
        /// </summary>
        /// <returns>The UserAgent string.</returns>
        public string GetUserAgent()
        {
            if (_userAgentCache != null)
            {
                return _userAgentCache;
            }

            if (string.IsNullOrEmpty(JellyfinVersion))
            {
                throw new ArgumentNullException("JF cannot be empty.");
            }

            var version = DlnaVersion switch
            {
                DlnaVersion.Version1 => "1.0",
                DlnaVersion.Version1_1 => "1.1",
                _ => "2.0"
            };

            _userAgentCache = _userAgent.Replace("{DlnaVersion}", version, StringComparison.OrdinalIgnoreCase)
                    .Replace("{AppVersion}", JellyfinVersion, StringComparison.OrdinalIgnoreCase);
            return _userAgentCache;
        }
    }
}
