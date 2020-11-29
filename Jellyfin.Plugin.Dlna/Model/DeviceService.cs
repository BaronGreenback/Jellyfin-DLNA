using System;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="DeviceService" />.
    /// </summary>
    public class DeviceService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceService"/> class.
        /// </summary>
        /// <param name="serviceType">The service type.</param>
        /// <param name="serviceId">The service id.</param>
        /// <param name="scpUrl">The url to the SCP.</param>
        /// <param name="controlUrl">The url to the control system.</param>
        /// <param name="eventSubUrl">The url to the event system.</param>
        public DeviceService(string serviceType, string serviceId, string scpUrl, string controlUrl, string eventSubUrl)
        {
            ServiceType = serviceType;
            ServiceId = serviceId;
            ScpdUrl = scpUrl;
            ControlUrl = controlUrl;
            EventSubUrl = eventSubUrl;
        }

        /// <summary>
        /// Gets the Service Type.
        /// </summary>
        public string ServiceType { get; }

        /// <summary>
        /// Gets the Service Id.
        /// </summary>
        public string ServiceId { get; }

        /// <summary>
        /// Gets the Scpd Url.
        /// </summary>
        public string ScpdUrl { get; private set; }

        /// <summary>
        /// Gets the Control Url.
        /// </summary>
        public string ControlUrl { get; private set; }

        /// <summary>
        /// Gets the EventSub Url.
        /// </summary>
        public string EventSubUrl { get; private set; }

        /// <summary>
        /// Normalize the class to the <paramref name="baseUrl"/>.
        /// </summary>
        /// <param name="baseUrl">The base url.</param>
        public void Normalise(string baseUrl)
        {
            ControlUrl = NormalizeUrl(baseUrl, ControlUrl);
            EventSubUrl = NormalizeUrl(baseUrl, EventSubUrl);
            ScpdUrl = NormalizeUrl(baseUrl, ScpdUrl);
        }

        private static string NormalizeUrl(string baseUrl, string url)
        {
            // If it's already a complete url, don't stick anything onto the front of it
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            if (!url.Contains('/', StringComparison.Ordinal))
            {
                url = "/dmr/" + url;
            }

            if (!url.StartsWith('/'))
            {
                url = "/" + url;
            }

            return baseUrl + url;
        }
    }
}
