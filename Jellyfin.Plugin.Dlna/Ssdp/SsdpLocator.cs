using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Dlna.EventArgs;
using Jellyfin.Plugin.Dlna.Model;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Dlna.Ssdp
{
    /// <summary>
    /// Searches the network for a particular device, device types, or UPnP service types.
    /// Listens for broadcast notifications of device availability and raises events to indicate changes in status.
    /// </summary>
    /// <remarks>
    /// Part of this code are taken from RSSDP.
    /// Copyright (c) 2015 Troy Willmot.
    /// Copyright (c) 2015-2018 Luke Pulverenti.
    /// </remarks>
    public class SsdpLocator : IDisposable
    {
        private readonly object _timerLock;
        private readonly object _deviceLock;
        private readonly ILogger _logger;
        private readonly TimeSpan _defaultSearchWaitTime;
        private readonly TimeSpan _oneSecond;
        private Timer? _broadcastTimer;
        private bool _disposed;
        private bool _initial = true;
        private int _initialInterval;
        private bool _started;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsdpLocator"/> class.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfigurationManager"/> instance.</param>
        /// <param name="logger">The <see cref="ILogger"/> instance.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> instance.</param>
        /// <param name="interfaces">An array of <see cref="IPNetAddress"/> of interface addresses to listen on.</param>
        /// <param name="networkManager">The <see cref="INetworkManager"/> instance.</param>
        public SsdpLocator(
            IConfigurationManager configuration,
            ILogger logger,
            ILoggerFactory loggerFactory,
            IPNetAddress[] interfaces,
            INetworkManager networkManager)
        {
            _timerLock = new object();
            _deviceLock = new object();
            _logger = logger;
            _defaultSearchWaitTime = TimeSpan.FromSeconds(4);
            _oneSecond = TimeSpan.FromSeconds(1);
            Devices = new List<DiscoveredSsdpDevice>();
            Server = SsdpServer.GetOrCreateInstance(
                configuration,
                loggerFactory,
                interfaces,
                networkManager);
        }

        /// <summary>
        /// Raised when a new device is discovered.
        /// </summary>
        public event EventHandler<DiscoveredSsdpDevice>? DeviceDiscovered;

        /// <summary>
        /// Raised when a notification is received that indicates a device has shutdown or otherwise become unavailable.
        /// </summary>
        public event EventHandler<DiscoveredSsdpDevice>? DeviceLeft;

        /// <summary>
        /// Gets or sets the initial interval between broadcasts.
        /// </summary>
        public int InitialInterval
        {
            get => _initialInterval;
            set
            {
                if (_initialInterval == value)
                {
                    return;
                }

                _initialInterval = value;
                _initial = true;
                if (_started)
                {
                    Start();
                }
            }
        }

        /// <summary>
        /// Gets or sets the interval between broadcasts.
        /// </summary>
        public int Interval { get; set; }

        /// <summary>
        /// Gets the SSDP server instance.
        /// </summary>
        public ISsdpServer Server { get; }

        /// <summary>
        /// Gets the list of the devices located.
        /// </summary>
        private List<DiscoveredSsdpDevice> Devices { get; }

        /// <summary>
        /// Slows down the discovery poll rate. <see cref="InitialInterval"/>, <see cref="Interval"/>.
        /// </summary>
        public void SlowDown()
        {
            _initial = false;
        }

        /// <summary>
        /// Starts the periodic broadcasting of M-SEARCH requests.
        /// </summary>
        public void Start()
        {
            if (!_started)
            {
                Server.AddEvent("HTTP/1.1 200 OK", IncomingMessage);
                Server.AddEvent("NOTIFY", ProcessNotificationMessage);
            }

            _started = true;

            if (_initialInterval == -1)
            {
                return;
            }

            var period = TimeSpan.FromSeconds(_initial ? InitialInterval : Interval) * 1000;
            lock (_timerLock)
            {
                if (_broadcastTimer == null)
                {
                    _broadcastTimer = new Timer(OnBroadcastTimerCallback, null, TimeSpan.FromSeconds(5), period);
                }
                else
                {
                    _broadcastTimer.Change(Timeout.InfiniteTimeSpan, period);
                }
            }
        }

        /// <summary>
        /// Disposes this object instance and all internally managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Searches the list of known devices and returns the one matching the criteria.
        /// </summary>
        /// <param name="devices">List of devices to search.</param>
        /// <param name="usn">USN criteria.</param>
        /// <returns>A <see cref="List{DiscoveredSsdpDevice}"/> containing the matching devices.</returns>
        private static List<DiscoveredSsdpDevice> FindExistingDevices(IEnumerable<DiscoveredSsdpDevice> devices, string usn)
        {
            return devices.Where(d => string.Equals(d.Usn, usn, StringComparison.Ordinal)).ToList();
        }

        /// <summary>
        /// Disposes this object and all internal resources. Stops listening for all network messages.
        /// </summary>
        /// <param name="disposing">True if managed resources should be disposed, or false is only unmanaged resources should be cleaned up.</param>
        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _disposed = true;
            Server.DeleteEvent("HTTP/1.1 200 OK", IncomingMessage);
            Server.DeleteEvent("NOTIFY", ProcessNotificationMessage);
            _logger.LogDebug("Disposing instance.");
            lock (_timerLock)
            {
                _broadcastTimer?.Dispose();
                _broadcastTimer = null;
            }
        }

        /// <summary>
        /// Removes old entries from the cache and transmits a discovery message.
        /// </summary>
        /// <param name="state">Not used.</param>
        private async void OnBroadcastTimerCallback(object? state)
        {
            try
            {
                RemoveExpiredDevicesFromCache();

                if (_initialInterval != -1)
                {
                    _logger.LogDebug("Sending discovery message.");
                    await BroadcastDiscoverMessage(SearchTimeToMxValue(_defaultSearchWaitTime)).ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
                // Do nothing.
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(ex, "SearchAsync failed.");
            }
        }

        /// <summary>
        /// Adds or updates the discovered device list.
        /// </summary>
        /// <param name="device">Device to add.</param>
        private void AddOrUpdateDiscoveredDevice(DiscoveredSsdpDevice device)
        {
            bool isNewDevice;
            lock (_deviceLock)
            {
                var existingDevice = FindExistingDevice(device);
                if (existingDevice == null)
                {
                    Devices.Add(device);
                    _logger.LogDebug("Found DLNA Device : {DescriptionLocation}", device.Location);
                    isNewDevice = true;
                }
                else
                {
                    Devices.Remove(existingDevice);
                    Devices.Add(device);

                    // If existingDevice was a placeholder, then this is still a new device.
                    isNewDevice = string.IsNullOrEmpty(existingDevice.Usn);
                }
            }

            if (isNewDevice)
            {
                DeviceDiscovered?.Invoke(this, device);
            }
        }

        /// <summary>
        /// Broadcasts a SSDP M-SEARCH request.
        /// </summary>
        /// <param name="mxValue">Mx value for the packet.</param>
        private Task BroadcastDiscoverMessage(TimeSpan mxValue)
        {
            const string SsdpSearch = "M-SEARCH * HTTP/1.1";

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MAN"] = "\"ssdp:discover\"",
                ["MX"] = mxValue.Seconds.ToString(CultureInfo.CurrentCulture),
                ["ST"] = "urn:schemas-upnp-org:device:MediaRenderer:1",
                ["USER-AGENT"] = Server.UserAgent,
                ["HOST"] = string.Empty
            };

            if (Server.DlnaVersion == DlnaVersion.Version2)
            {
                values["CPFN.UPNP.ORG"] = "Jellyfin Server";
            }

            Server.SendMulticastSsdp(values, SsdpSearch);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Ensures we know about any transmitting SSDP device.
        /// </summary>
        /// <param name="args">A <see cref="SsdpEventArgs"/> containing details of the event.</param>
        private void IncomingMessage(SsdpEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            DiscoveredSsdpDevice device;
            try
            {
                device = new DiscoveredSsdpDevice(DateTimeOffset.Now, "ST", args.Message, args.ReceivedFrom);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // corrupt SSDP message. Ignore.
                return;
            }

            AddOrUpdateDiscoveredDevice(device);
        }

        /// <summary>
        /// Processes a notification message.
        /// </summary>
        /// <param name="e">A <see cref="SsdpEventArgs"/> containing details of the event.</param>
        private void ProcessNotificationMessage(SsdpEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            DiscoveredSsdpDevice device;
            try
            {
                device = new DiscoveredSsdpDevice(DateTimeOffset.Now, "NT", e.Message, e.ReceivedFrom);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // Corrupt SSDP message, so ignore.
                return;
            }

            var notificationType = e.Message["NTS"];

            // Process alive packets.
            if (string.Equals(notificationType, "ssdp:alive", StringComparison.Ordinal))
            {
                if (Server.IsTracing(e.LocalIpAddress))
                {
                    _logger.LogDebug("SsdpAlive <- {DescriptionLocation} ", device.Location);
                }

                AddOrUpdateDiscoveredDevice(device);

                return;
            }

            // Process byebye packets.
            if (!string.Equals(notificationType, "ssdp:byebye", StringComparison.Ordinal) || string.IsNullOrEmpty(device.NotificationType))
            {
                return;
            }

            // Process ByeBye Notification.
            if (DeviceDied(device.Usn))
            {
                return;
            }

            if (Server.IsTracing(device.Endpoint.Address))
            {
                _logger.LogDebug("ByeBye: {Device}", device);
            }

            DeviceLeft?.Invoke(this, device);
        }

        /// <summary>
        /// Removes expired devices from the cache.
        /// </summary>
        private void RemoveExpiredDevicesFromCache()
        {
            DiscoveredSsdpDevice[] expiredDevices;

            lock (_deviceLock)
            {
                expiredDevices = (from device in Devices where device.IsExpired() select device).ToArray();

                foreach (var device in expiredDevices)
                {
                    Devices.Remove(device);
                }
            }

            // Don't do this inside lock because DeviceDied raises an event which means public code may execute during lock and cause problems.
            foreach (var expiredUsn in (from expiredDevice in expiredDevices select expiredDevice.Usn).Distinct())
            {
                DeviceDied(expiredUsn);
            }
        }

        /// <summary>
        /// Removes a device from the cache.
        /// </summary>
        /// <param name="deviceUsn">USN of the device.</param>
        /// <returns>True if the operation succeeded.</returns>
        private bool DeviceDied(string deviceUsn)
        {
            List<DiscoveredSsdpDevice>? existingDevices;
            lock (_deviceLock)
            {
                existingDevices = FindExistingDevices(Devices, deviceUsn);
                foreach (var existingDevice in existingDevices)
                {
                    Devices.Remove(existingDevice);
                }
            }

            if (existingDevices.Count == 0)
            {
                return false;
            }

            foreach (var removedDevice in existingDevices)
            {
                DeviceLeft?.Invoke(this, removedDevice);
            }

            return true;
        }

        /// <summary>
        /// Searches the list of known devices and returns the one matching the criteria.
        /// </summary>
        /// <param name="discovered">The discovered device. <see cref="DiscoveredSsdpDevice"/>.</param>
        /// <returns>Device if located, or null if not.</returns>
        private DiscoveredSsdpDevice? FindExistingDevice(DiscoveredSsdpDevice discovered)
        {
            return Devices.FirstOrDefault(d => d.Location.Equals(discovered.Location))
                ?? Devices.FirstOrDefault(d => d.Endpoint.Equals(discovered.Endpoint)
                    && string.IsNullOrEmpty(d.Usn))
                ?? Devices.FirstOrDefault(d => string.Equals(d.NotificationType, discovered.NotificationType, StringComparison.Ordinal)
                    && string.Equals(d.Usn, discovered.Usn, StringComparison.Ordinal));
        }

        /// <summary>
        /// Reduces the wait time by one.
        /// </summary>
        /// <param name="searchWaitTime">Timespan to reduce.</param>
        /// <returns>The resultant timespan.</returns>
        private TimeSpan SearchTimeToMxValue(TimeSpan searchWaitTime)
        {
            if (searchWaitTime.TotalSeconds < 2 || searchWaitTime == TimeSpan.Zero)
            {
                return _oneSecond;
            }

            return searchWaitTime.Subtract(_oneSecond);
        }
    }
}
