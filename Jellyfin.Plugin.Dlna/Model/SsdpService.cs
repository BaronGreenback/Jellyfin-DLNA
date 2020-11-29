using System;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Represents a device that is a descendant of a <see cref="SsdpRootDevice"/> instance.
    /// </summary>
    public class SsdpService : SsdpDevice
    {
        private SsdpRootDevice? _rootDevice;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsdpService"/> class.
        /// </summary>
        /// <param name="uuid">The device uuid.</param>
        /// <param name="deviceType">The device type.</param>
        public SsdpService(Guid uuid, string deviceType)
            : base(uuid, deviceType, "service")
        {
        }

        /// <summary>
        /// Gets the <see cref="SsdpRootDevice"/> that is this device's first ancestor.
        /// If this device is itself an <see cref="SsdpRootDevice"/>, then returns a reference to itself.
        /// </summary>
        public SsdpRootDevice? RootDevice
        {
            get => _rootDevice;

            internal set
            {
                _rootDevice = value;
                ChangeRoot(value);
            }
        }
    }
}
