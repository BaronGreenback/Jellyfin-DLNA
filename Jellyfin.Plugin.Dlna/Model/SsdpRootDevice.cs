using System;
using System.Net;
using Jellyfin.Networking.Udp;
using MediaBrowser.Common.Net;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Represents a 'root' device, a device that has no parent. Used for publishing devices and for the root device in a tree of discovered devices.
    /// </summary>
    /// <remarks>
    /// <para>Child (embedded) devices are represented by the <see cref="SsdpDevice"/> in the <see cref="SsdpDevice.Services"/> property.</para>
    /// <para>Root devices contain some information that applies to the whole device tree and is therefore not present on child devices, such as <see cref="CacheLifetime"/> and <see cref="Location"/>.</para>
    /// </remarks>
    /// <remarks>
    /// Adapted from code taken from RSSDP which is Copyright (c) 2015 Troy Willmot, and 2015-2018 Luke Pulverenti.
    /// </remarks>
    public class SsdpRootDevice : SsdpDevice, IEquatable<SsdpRootDevice>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SsdpRootDevice"/> class.
        /// </summary>
        /// <param name="cacheLifetime">Cache lifetime.</param>
        /// <param name="location">Location.</param>
        /// <param name="address">IP Address.</param>
        /// <param name="uuid">UDN.</param>
        /// <param name="deviceType">The type of the device.</param>
        public SsdpRootDevice(TimeSpan cacheLifetime, Uri location, IPNetAddress address, Guid uuid, string deviceType)
            : base(uuid, deviceType, "device")
        {
            CacheLifetime = cacheLifetime;
            Location = location?.ToString() ?? throw new ArgumentNullException(nameof(location));
            NetAddress = address ?? throw new ArgumentNullException(nameof(address));
        }

        /// <summary>
        /// Gets specifies how long clients can cache this device's details for. Optional but defaults to <see cref="TimeSpan.Zero"/> which means no-caching.
        /// Recommended value is half an hour.
        /// </summary>
        /// <remarks>
        /// <para>Specify <see cref="TimeSpan.Zero"/> to indicate no caching allowed.</para>
        /// <para>Also used to specify how often to rebroadcast alive notifications.</para>
        /// <para>The UPnP/SSDP specifications indicate this should not be less than 1800 seconds (half an hour), but this is not enforced by this library.</para>
        /// </remarks>
        public TimeSpan CacheLifetime { get; }

        /// <summary>
        /// Gets the URL used to retrieve the description document for this device/tree. Required.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Gets the address used to check if the received message from same interface with this device/tree. Required.
        /// </summary>
        public IPNetAddress NetAddress { get; }

        /*
        /// <summary>
        /// Returns this object as a string.
        /// </summary>
        /// <returns>String representation of this object.</returns>
        public override string ToString()
        {
            return $"{DeviceType}-{Uuid}-{Location}";
        }
        */

        /// <summary>
        /// Equality Method. Used by List{SsdpRoot}.Contains.
        /// </summary>
        /// <param name="other">Item to compare.</param>
        /// <returns>True if other matches this object.</returns>
        public bool Equals(SsdpRootDevice? other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(ToString(), other.ToString(), StringComparison.Ordinal) && NetAddress.Equals(other.NetAddress);
        }

        /// <summary>
        /// Equality method.
        /// </summary>
        /// <param name="other">Item to compare.</param>
        /// <returns>True if other matches this object.</returns>
        public override bool Equals(object? other)
        {
            if (other is SsdpRootDevice otherDevice)
            {
                return Equals(otherDevice);
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this object.
        /// </summary>
        /// <returns>Hash Code.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
