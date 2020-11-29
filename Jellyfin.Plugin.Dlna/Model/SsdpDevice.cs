using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Base class representing the common details of a root device, either to be published or that has been located.
    /// </summary>
    /// Part of this code are taken from RSSDP.
    /// Copyright (c) 2015 Troy Willmot.
    /// Copyright (c) 2015-2018 Luke Pulverenti.
    public abstract class SsdpDevice
    {
        private readonly IList<SsdpDevice> _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsdpDevice"/> class.
        /// Allows constructing a device with no parent. Should only be used from derived types that are or inherit
        /// from <see cref="SsdpRootDevice"/>.
        /// </summary>
        /// <param name="uuid">UDN.</param>
        /// <param name="deviceType">The device type.</param>
        /// <param name="deviceClass">The device class.</param>
        protected SsdpDevice(Guid uuid, string deviceType, string deviceClass)
        {
            DeviceTypeNamespace = "schemas-upnp-org";
            DeviceType = deviceType;
            DeviceClass = deviceClass;
            _services = new List<SsdpDevice>();
            Services = new ReadOnlyCollection<SsdpDevice>(_services);
            Uuid = uuid.ToString("D", CultureInfo.InvariantCulture);
            Udn = "uuid:" + Uuid;
        }

        /// <summary>
        /// Gets the core device type (not including namespace, version etc.). Required.
        /// </summary>
        /// <remarks><para>Defaults to the UPnP basic device type.</para></remarks>
        /// <seealso cref="DeviceTypeNamespace"/>
        /// <seealso cref="FullDeviceType"/>
        public string DeviceType { get; }

        /// <summary>
        /// Gets the device class.
        /// </summary>
        public string DeviceClass { get; }

        /// <summary>
        /// Gets the namespace for the <see cref="DeviceType"/> of this device. Optional, but defaults to UPnP schema so should be
        /// changed if <see cref="DeviceType"/> is not a UPnP device type.
        /// </summary>
        /// <remarks><para>Defaults to the UPnP standard namespace.</para></remarks>
        /// <seealso cref="DeviceType"/>
        /// <seealso cref="FullDeviceType"/>
        public string DeviceTypeNamespace { get; }

        /// <summary>
        /// Gets the full device type string.
        /// </summary>
        /// <remarks>
        /// <para>The format used is urn:<see cref="DeviceTypeNamespace"/>:device:<see cref="DeviceType"/>.</para>
        /// </remarks>
        public string FullDeviceType => $"urn:{DeviceTypeNamespace}:{DeviceClass}:{DeviceType}:1";

        /// <summary>
        /// Gets the universally unique identifier for this device (without the uuid: prefix). Required.
        /// </summary>
        public string Uuid { get; }

        /// <summary>
        /// Gets a unique device name for this device. Optional, not recommended to be explicitly set.
        /// </summary>
        public string Udn { get; }

        /// <summary>
        /// Gets a read-only enumerable set of <see cref="SsdpDevice"/> objects representing the services of this device.
        /// </summary>
        public IList<SsdpDevice> Services { get; }

        /// <summary>
        /// Returns the root device of this object.
        /// </summary>
        /// <returns>Root device in the structure.</returns>
        public SsdpRootDevice GetRootDevice()
        {
            SsdpDevice? rootDevice = this switch
            {
                SsdpRootDevice root => root,
                SsdpService embedded => embedded.RootDevice,
                _ => null
            };

            if (rootDevice == null)
            {
                throw new NullReferenceException("Root device cannot be null.");
            }

            return (SsdpRootDevice)rootDevice;
        }

        /// <summary>
        /// Adds a child service to the <see cref="Services"/> collection.
        /// </summary>
        /// <param name="service">The <see cref="SsdpService"/> instance to add.</param>
        /// <remarks>
        /// <para>If the device is already a member of the <see cref="Services"/> collection, this method does nothing.</para>
        /// <para>Also sets the <see cref="SsdpService.RootDevice"/> property of the added device and all descendant devices to the
        /// relevant <see cref="SsdpRootDevice"/> instance.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="service"/> argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the <paramref name="service"/> is already associated with a different
        /// <see cref="SsdpRootDevice"/> instance than used in this tree. Can occur if you try to add the same device instance to more than
        /// one tree. Also thrown if you try to add a device to itself.</exception>
        public void AddService(SsdpService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            SsdpDevice thisRootDevice = GetRootDevice();
            if (service.RootDevice != null && !service.RootDevice.Equals(thisRootDevice))
            {
                throw new InvalidOperationException("This device is already associated with a different root device (has been added as a child in another branch).");
            }

            if (service == this)
            {
                throw new InvalidOperationException("Can't add device to itself.");
            }

            lock (_services)
            {
                service.RootDevice = (SsdpRootDevice)thisRootDevice;
                if (!_services.Contains(service))
                {
                    _services.Add(service);
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{DeviceType} - {Uuid}";
        }

        /// <summary>
        /// Changes the root of a device.
        /// </summary>
        /// <param name="value">New root device.</param>
        protected void ChangeRoot(SsdpRootDevice? value)
        {
            lock (_services)
            {
                foreach (var embeddedDevice in Services)
                {
                    ((SsdpService)embeddedDevice).RootDevice = value;
                }
            }
        }
    }
}
