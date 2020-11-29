using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using MediaBrowser.Common.Extensions;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Represents a discovered device, containing basic information about the device and the location of it's full device description document. Also provides convenience methods for retrieving the device description document.
    /// </summary>
    /// <remarks>
    /// Part of this code take from RSSDP.
    /// Copyright (c) 2015 Troy Willmot.
    /// Copyright (c) 2015-2018 Luke Pulverenti.
    /// </remarks>
    public class DiscoveredSsdpDevice
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveredSsdpDevice"/> class.
        /// </summary>
        /// <param name="asAt">Time data was received.</param>
        /// <param name="notificationType">Header name used for the notification type.</param>
        /// <param name="messageHeaders">Message headers.</param>
        /// <param name="endpoint">Message endpoint.</param>
        public DiscoveredSsdpDevice(DateTimeOffset asAt, string notificationType, Dictionary<string, string> messageHeaders, IPEndPoint endpoint)
        {
            if (messageHeaders == null)
            {
                throw new ArgumentNullException(nameof(messageHeaders));
            }

            AsAt = asAt;
            CacheLifetime = TimeSpan.Zero;
            Endpoint = endpoint;

            // ByeBye doesn't have a location, so we don't want to error here.
            messageHeaders.TryGetValue("LOCATION", out var loc);
            Location = loc ?? string.Empty;

            NotificationType = messageHeaders[notificationType];
            Usn = GetUuid(messageHeaders["USN"]);

            if (messageHeaders.TryGetValue("CACHE-CONTROL", out var cc))
            {
                if (!string.IsNullOrEmpty(cc))
                {
                    var values = cc.Split('=');
                    if (values.Length == 2 && string.Equals("max-age", values[0], StringComparison.Ordinal))
                    {
                        if (TimeSpan.TryParse(values[1], out var clt))
                        {
                            CacheLifetime = clt;
                        }
                    }
                }
            }

            Headers = messageHeaders;
        }

        /// <summary>
        /// Gets the type of notification, being either a uuid, device type, service type or upnp:rootdevice.
        /// </summary>
        public string NotificationType { get; }

        /// <summary>
        /// Gets the universal service name (USN) of the device.
        /// </summary>
        public string Usn { get; }

        /// <summary>
        /// Gets a URL pointing to the device description document for this device.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Gets the ip address of the device.
        /// </summary>
        public IPEndPoint Endpoint { get; }

        /// <summary>
        /// Gets the headers from the SSDP device response message.
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets the length of time this information is valid for (from the <see cref="AsAt"/> time).
        /// </summary>
        private TimeSpan CacheLifetime { get; }

        /// <summary>
        /// Gets the date and time this information was received.
        /// </summary>
        private DateTimeOffset AsAt { get; }

        /// <summary>
        /// Returns true if this device information has expired, based on the current date/time, and the <see cref="CacheLifetime"/> &amp; <see cref="AsAt"/> properties.
        /// </summary>
        /// <returns>True of this device information has expired.</returns>
        public bool IsExpired()
        {
            return CacheLifetime == TimeSpan.Zero || AsAt.Add(CacheLifetime) <= DateTimeOffset.Now;
        }

        /// <summary>
        /// Returns the device's <see cref="Usn"/> value.
        /// </summary>
        /// <returns>A string containing the device's universal service name.</returns>
        public override string ToString()
        {
            return Usn;
        }

        /// <summary>
        /// Extracts the uuid from the string.
        /// </summary>
        /// <param name="usn">The string to search.</param>
        /// <returns>The uuid in the string, or a new uuid if one cannot be found.</returns>
        private static string GetUuid(string usn)
        {
            const string UuidStr = "uuid:";
            const string UuidColonStr = "::";

            var index = usn.IndexOf(UuidStr, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
            {
                return usn.GetMD5().ToString("N", CultureInfo.InvariantCulture);
            }

            ReadOnlySpan<char> tmp = usn.AsSpan()[(index + UuidStr.Length)..];

            index = tmp.IndexOf(UuidColonStr, StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                tmp = tmp[..index];
            }

            index = tmp.IndexOf('{');
            if (index != -1)
            {
                int endIndex = tmp.IndexOf('}');
                if (endIndex != -1)
                {
                    tmp = tmp[(index + 1)..endIndex];
                }
            }

            return tmp.ToString();
        }
    }
}
