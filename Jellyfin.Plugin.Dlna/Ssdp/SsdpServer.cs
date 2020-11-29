using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Networking.Udp;
using Jellyfin.Plugin.Dlna.Configuration;
using Jellyfin.Plugin.Dlna.EventArgs;
using Jellyfin.Plugin.Dlna.Model;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Dlna.Ssdp
{
    /// <summary>
    /// Publishing SSDP devices and responding to search requests.
    ///
    /// Is designed to work in conjunction with DlnaServer and DlnaPlayTo.
    /// </summary>
    public class SsdpServer : ISsdpServer
    {
        private static readonly object _creationLock = new();
        private static ISsdpServer? _instance;

        private readonly object _synchroniser;
        private readonly ILogger _logger;
        private readonly Hashtable _listeners;
        private readonly Hashtable _senders;
        private readonly INetworkManager _networkManager;
        private readonly Dictionary<string, List<SsdpEventHandler>> _events;
        private readonly IConfigurationManager _config;
        private readonly object _eventFireLock;
        private string _bootId = "1";
        private string _nextBootId = "2";
        private string _configId = "1";
        private IPNetAddress[] _interfaces;
        private bool _running;
        private bool _eventfire;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsdpServer"/> class.
        /// </summary>
        /// <param name="config">The <see cref="IConfigurationManager"/> instance.</param>
        /// <param name="logger">The logger instance.<see cref="ILogger"/>.</param>
        /// <param name="interfaces">Interfaces to use for the server.</param>
        /// <param name="networkManager">The <see cref="INetworkManager"/> instance to use.</param>
        private SsdpServer(
            IConfigurationManager config,
            ILogger logger,
            IPNetAddress[] interfaces,
            INetworkManager networkManager)
        {
            _logger = logger;
            _eventFireLock = new object();
            _synchroniser = new object();
            _listeners = new Hashtable();
            _senders = new Hashtable();
            _events = new();
            PermittedDevices = Array.Empty<IPNetAddress>();
            DeniedDevices = Array.Empty<IPNetAddress>();
            _config = config;
            _interfaces = interfaces;
            _networkManager = networkManager;
            Configuration = config.GetConfiguration<SsdpConfiguration>("ssdp");
            ValidateConfiguration();
        }

        /// <summary>
        /// Gets the SSDP static instance.
        /// </summary>
        public static ISsdpServer Instance => GetInstance();

        /// <summary>
        /// Gets or sets the Dlna level supported by this server.
        /// </summary>
        public DlnaVersion DlnaVersion
        {
            get => Configuration.DlnaVersion;
            set => Configuration.DlnaVersion = value;
        }

        /// <summary>
        /// Gets or sets the host name to be used in SSDP packets.
        /// </summary>
        public string UserAgent
        {
            get => Configuration.UserAgent;
            set => Configuration.UserAgent = value;
        }

        /// <summary>
        /// Gets the BOOTID.UPNP.ORG value.
        /// </summary>
        public string BootId => _bootId;

        /// <summary>
        /// Gets the CONFIGID.UPNP.ORG value.
        /// </summary>
        public string ConfigId => _configId;

        /// <summary>
        /// Gets the NEXTBOOTID.UPNP.ORG value.
        /// </summary>
        public string NextBootId => _nextBootId;

        /// <summary>
        /// Gets a value indicating the ssdp configuration.
        /// </summary>
        public SsdpConfiguration Configuration { get; }

        /// <summary>
        /// Gets a value indicating the number of times each UDP packet should be sent.
        /// </summary>
        public int UdpSendCount
        {
            get => Configuration.UdpSendCount;

            private set => Configuration.UdpSendCount = value;
        }

        /// <summary>
        /// Gets or sets the port range to use to select ports from (excludes port 1900).
        /// </summary>
        public string UdpPortRange
        {
            get => Configuration.UdpPortRange;

            set => Configuration.UdpPortRange = value;
        }

        /// <summary>
        /// Gets or sets the list of permitted devices.
        /// </summary>
        public IEnumerable<IPNetAddress> PermittedDevices { get; set; }

        /// <summary>
        /// Gets the list of blacklisted devices.
        /// </summary>
        public IEnumerable<IPNetAddress> DeniedDevices { get; private set; }

        /// <summary>
        /// Gets a value indicating whether detailed DNLA debug logging is active.
        /// </summary>
        public bool Tracing
        {
            get => Configuration.EnableSsdpTracing;

            private set => Configuration.EnableSsdpTracing = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the tracing filter to be applied.
        /// </summary>
        private IPAddress? TracingFilter { get; set; }

        /// <summary>
        /// Gets or creates the singleton instance that is used by all objects.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfigurationManager"/> instance.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> instance.</param>
        /// <param name="interfaces">Interfaces to use for the server.</param>
        /// <param name="networkManager">The <see cref="INetworkManager"/> instance to use.</param>
        /// <returns>The <see cref="SsdpService"/> singleton instance.</returns>
        public static ISsdpServer GetOrCreateInstance(
            IConfigurationManager configuration,
            ILoggerFactory loggerFactory,
            IPNetAddress[] interfaces,
            INetworkManager networkManager)
        {
            // As this class is used in multiple areas, we only want to create it once.
            lock (_creationLock)
            {
                if (_instance == null)
                {
                    _instance = new SsdpServer(configuration, loggerFactory.CreateLogger<SsdpServer>(), interfaces, networkManager);
                }
                else
                {
                    _instance.UpdateInterfaces(interfaces);
                }
            }

            return _instance;
        }

        /// <summary>
        /// Returns the SSDP instance.
        /// </summary>
        /// <returns>The SSDP static instance.</returns>
        public static ISsdpServer GetInstance()
        {
            return _instance ?? throw new NullReferenceException("Ssdp Server has not been instantiated.");
        }

        /// <summary>
        /// Increase the value of BOOTID.UPNP.ORG .
        /// </summary>
        public void IncreaseBootId()
        {
            _bootId = _nextBootId;
            int nextBootId = int.Parse(_nextBootId);
            nextBootId++;
            _nextBootId = nextBootId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Adds an event.
        /// </summary>
        /// <param name="action">The string to event on.</param>
        /// <param name="handler">The handler to call.</param>
        public void AddEvent(string action, SsdpEventHandler handler)
        {
            lock (_synchroniser)
            {
                if (!_events.ContainsKey(action))
                {
                    _events[action] = new List<SsdpEventHandler>();
                }

                // Ensure we only add the handler once.
                if (_events[action].IndexOf(handler) == -1)
                {
                    _events[action].Add(handler);
                }
            }

            Start();
        }

        /// <summary>
        /// Returns the status of the tracing.
        /// </summary>
        /// <param name="address">The <see cref="IPAddress"/> to match.</param>
        /// <param name="address2">Optional second <see cref="IPAddress"/> to match.</param>
        /// <returns>True if this address is being traced.</returns>
        public bool IsTracing(IPAddress address, IPAddress? address2 = null)
        {
            return Tracing && (TracingFilter == null || TracingFilter.Equals(address)
                || (TracingFilter != null && TracingFilter.Equals(address2)));
        }

        /// <summary>
        /// Called when the configuration has changed.
        /// </summary>
        public void UpdateConfiguration()
        {
            ValidateConfiguration();
            _config.SaveConfiguration("ssdp", Configuration);
        }

        /// <summary>
        /// Removes an event.
        /// </summary>
        /// <param name="action">The event to remove.</param>
        /// <param name="handler">The handler to remove.</param>
        public void DeleteEvent(string action, SsdpEventHandler handler)
        {
            lock (_synchroniser)
            {
                if (_events.ContainsKey(action))
                {
                    _events[action].Remove(handler);
                    if (_events[action].Count == 0)
                    {
                        _events.Remove(action);
                    }
                }
            }

            if (_events.Count == 0)
            {
                Stop();
            }
        }

        /// <summary>
        /// Multicasts an SSDP package, across all relevant interfaces types.
        /// </summary>
        /// <param name="values">Values that make up the message.</param>
        /// <param name="classification">Classification of message to send.</param>
        /// <param name="limitToFamily">If provided, contains the address family of the message that we are advertising. e.g. Don't advertise IP4 across IP6.</param>
        /// <param name="sendCount">Optional value indicating the number of times to transmit the message.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        public async Task SendMulticastSsdp(Dictionary<string, string> values, string classification, AddressFamily? limitToFamily = null, int? sendCount = null)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var addHost = values.ContainsKey("HOST");
            foreach (var ipEntry in _senders.Keys)
            {
                var addr = (IPAddress)ipEntry;
                if (addr == null
                    || (limitToFamily != null && limitToFamily != addr.AddressFamily)
                    || (addr.AddressFamily == AddressFamily.InterNetworkV6 && addr.ScopeId == 0))
                {
                    continue;
                }

                if (addHost)
                {
                    string mcast = addr.AddressFamily == AddressFamily.InterNetwork
                        ? UdpHelper.SsdpMulticastIPv4 + ":1900"
                        : addr.IsIPv6LinkLocal
                            ? $"[{UdpHelper.SsdpMulticastIPv6LinkLocal}]:1900"
                            : $"[{UdpHelper.SsdpMulticastIPv6SiteLocal}]:1900";

                    values["HOST"] = mcast;
                }

                var message = BuildMessage(classification, values);

                var client = (UdpProcess?)_senders[ipEntry];
                if (client != null)
                {
                    values["SEARCHPORT.UPNP.ORG"] = client.LocalEndPoint.Port.ToString();
                    await client.SendMulticast(1900, message, sendCount ?? Configuration.UdpSendCount).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogError("Unable to find client for {Address}", addr);
                }
            }
        }

        /// <summary>
        /// Unicasts an SSDP message.
        /// </summary>
        /// <param name="values">Values that make up the message.</param>
        /// <param name="classification">Classification of message to send.</param>
        /// <param name="localIp">Local endpoint to use.</param>
        /// <param name="endPoint">Remote endpoint to transmit to.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        public async Task SendUnicastSsdp(Dictionary<string, string> values, string classification, IPAddress localIp, IPEndPoint endPoint)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var message = BuildMessage(classification, values);

            await SendMessageAsync(message, localIp, endPoint, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the UDP port that is assigned to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">The <see cref="IPAddress"/>.</param>
        /// <returns>UDP port assigned, or 1900 if not found.</returns>
        public int GetPortFor(IPAddress address)
        {
            var client = (UdpProcess?)_senders[address];
            return client?.LocalEndPoint.Port ?? 1900;
        }

        /// <summary>
        /// Restarts the service with a different set of interfaces.
        /// </summary>
        /// <param name="interfaces">An array of <see cref="IPNetAddress"/> containing a list of interfaces.</param>
        public void UpdateInterfaces(IPNetAddress[] interfaces)
        {
            if (_running && !_interfaces.SequenceEqual(interfaces))
            {
                Stop();
                _interfaces = interfaces;
                Start();
            }
            else
            {
                _interfaces = interfaces;
            }
        }

        /// <summary>
        /// Builds an SSDP message.
        /// </summary>
        /// <param name="header">SSDP Header string.</param>
        /// <param name="values">SSDP parameters.</param>
        /// <returns>Formatted string.</returns>
        private static string BuildMessage(string header, Dictionary<string, string> values)
        {
            var builder = new StringBuilder(512);
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0}\r\n", header);

            foreach (var (key, value) in values)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}\r\n", key, value);
            }

            builder.Append("\r\n\r\n");
            return builder.ToString();
        }

        /// <summary>
        /// Updates the SSDP tracing filter.
        /// </summary>
        private void UpdateTracingFilter()
        {
            var enabled = Configuration.EnableSsdpTracing;
            _logger.LogDebug("SSDP Tracing : {Filter}", enabled);
            TracingFilter = null;

            var filter = Configuration.SsdpTracingFilter;
            if (!string.IsNullOrEmpty(filter))
            {
                if (IPAddress.TryParse(filter, out var ip))
                {
                    TracingFilter = ip;
                    _logger.LogDebug("SSDP Tracing filtering on: {Filter}", filter);
                }
                else
                {
                    _logger.LogError("SSDP Tracing filter {Filter} is invalid. Ignoring.", filter);
                }
            }

            UdpProcess client;
            foreach (var i in _listeners.Values)
            {
                if (i == null)
                {
                    continue;
                }

                client = (UdpProcess)i;
                client.TracingFilter = TracingFilter;
                client.Tracing = enabled;
            }

            foreach (var i in _senders.Values)
            {
                if (i == null)
                {
                    continue;
                }

                client = (UdpProcess)i;
                client.TracingFilter = TracingFilter;
                client.Tracing = enabled;
            }
        }

        /// <summary>
        /// Initialises the server, and starts listening on all internal interfaces.
        /// </summary>
        private void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            _logger.LogDebug("EnableMultiSocketBinding : {EnableMultiSocketBinding}", UdpHelper.EnableMultiSocketBinding);

            foreach (var ip in _interfaces)
            {
                if ((_networkManager.IpClassType == IpClassType.Ip6Only && ip.AddressFamily == AddressFamily.InterNetwork)
                    || (_networkManager.IpClassType == IpClassType.Ip4Only && ip.AddressFamily == AddressFamily.InterNetworkV6))
                {
                    continue;
                }

                UdpProcess? client = UdpHelper.CreateMulticastClient(
                    ip.Address,
                    1900,
                    ProcessMessage,
                    _logger,
                    OnFailure);

                if (client != null)
                {
                    client.TracingFilter = TracingFilter;
                    client.Tracing = Tracing;
                    _listeners[ip.Address] = client;
                    _logger.LogDebug("Successfully created a multicast client on {Address}:1900", ip.Address);
                }

                // Create the port used for sending.
                int port = UdpHelper.GetPort(Configuration.UdpPortRange);
                if (port == 1900)
                {
                    port = 1901;
                }

                client = UdpHelper.CreateMulticastClient(
                    ip.Address,
                    port,
                    ProcessMessage,
                    _logger,
                    OnFailure);

                if (client == null)
                {
                    _logger.LogError("Failed to create a unicast client on {Address}:{Port}", ip.Address, port);
                    continue;
                }

                client.TracingFilter = TracingFilter;
                client.Tracing = Tracing;
                _senders[ip.Address] = client;
                _logger.LogDebug("Successfully created a unicast client on {Address}:{Port}", ip.Address, client.LocalEndPoint.Port);
            }
        }

        /// <summary>
        /// Stops the server and frees up resources.
        /// </summary>
        private void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            foreach (var listener in _listeners.Values)
            {
                try
                {
                    ((UdpProcess)listener)?.Close();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // Ignore and continue.
                }
            }

            _listeners.Clear();

            foreach (var sender in _senders.Values)
            {
                try
                {
                    ((UdpProcess)sender)?.Close();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // Ignore and continue.
                }
            }

            _senders.Clear();
        }

        /// <summary>
        /// Sends a message to the SSDP multicast address and port.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="localIpAddress">The interface IP to use.</param>
        /// <param name="endPoint">The destination endpoint.</param>
        /// <param name="restrict">True if the transmission should be restricted to the LAN.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task SendMessageAsync(string message, IPAddress localIpAddress, IPEndPoint endPoint, bool restrict = false)
        {
            if (restrict)
            {
                if (!_networkManager.IsInLocalNetwork(endPoint.Address))
                {
                    _logger.LogDebug("FILTERED: Sending to non-LAN address: {Address}.", endPoint.Address);
                    return;
                }
            }

            var client = _senders[localIpAddress];
            if (client != null)
            {
                await ((UdpProcess)client).SendUnicast(message, endPoint, UdpSendCount).ConfigureAwait(false);
            }
            else
            {
                _logger.LogError("Unable to find socket for {Address}", localIpAddress);
            }
        }

        private void OnFailure(UdpProcess client, Exception? ex = null, string? msg = null)
        {
            _listeners.Remove(client.LocalEndPoint.Address);
            _logger.LogError(ex, msg);
        }

        /// <summary>
        /// Sends a packet via unicast.
        /// </summary>
        /// <param name="data">Packet to send.</param>
        private Dictionary<string, string> ParseMessage(string data)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = data.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                var i = line.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                if (i != -1)
                {
                    string propertyName = line[..i].ToUpper(CultureInfo.InvariantCulture);
                    if (!result.ContainsKey(propertyName))
                    {
                        result.Add(propertyName, line[(i + 1)..].Trim());
                    }
                    else
                    {
                        _logger.LogDebug("{Name} appears twice : {Data}", propertyName, data);
                    }
                }
                else
                {
                    i = line.IndexOf('*', StringComparison.OrdinalIgnoreCase);
                    if (i != -1)
                    {
                        result.Add("ACTION", line[..(i - 1)].Trim());
                    }
                    else if (line.StartsWith("HTTP", StringComparison.OrdinalIgnoreCase))
                    {
                        result["ACTION"] = line;
                    }
                }
            }

            if (!result.ContainsKey("ACTION"))
            {
                result["ACTION"] = string.Empty;
            }

            return result;
        }

        /// <summary>
        /// Processes a SSDP message.
        /// </summary>
        /// <param name="client">The client from which we received the message.</param>
        /// <param name="data">The data to process.</param>
        /// <param name="receivedFrom">The remote endpoint.</param>
        private Task ProcessMessage(UdpProcess client, string data, IPEndPoint receivedFrom)
        {
            // If permitted device list is empty, then all devices are permitted.
            if (PermittedDevices.Any() || DeniedDevices.Any())
            {
                if (PermittedDevices.FirstOrDefault(p => p.Contains(receivedFrom.Address)) != null)
                {
                    // _logger.LogDebug("DLNA device at {Address} is not in the permitted list.", receivedFrom.Address);
                    return Task.CompletedTask;
                }

                if (DeniedDevices.FirstOrDefault(p => p.Contains(receivedFrom.Address)) != null)
                {
                    // _logger.LogDebug("DLNA device at {Address} is in the denied list.", receivedFrom.Address);
                    return Task.CompletedTask;
                }
            }

            // otherwise fallback to checking its on the LAN.
            else if (!_networkManager.IsInLocalNetwork(receivedFrom.Address))
            {
                // Not from the local LAN, so ignore it.
                return Task.CompletedTask;
            }

            var msg = ParseMessage(data);
            string action = msg["ACTION"];

            var localIpAddress = client.LocalEndPoint.Address;
            if (localIpAddress.Equals(IPAddress.Any))
            {
                // received on the ANY port - so we need to find correct interface to respond on.
                // If no interface contains the address, we'll use the first one assigned.
                localIpAddress = (_interfaces.FirstOrDefault(i => i.Contains(receivedFrom.Address)) ?? _interfaces.First()).Address;
            }

            foreach (var i in _senders.Values)
            {
                if (i == null)
                {
                    continue;
                }

                var transmitter = (UdpProcess?)_senders[receivedFrom.Address];
                if (transmitter?.LocalEndPoint.Port == receivedFrom.Port)
                {
                    // Don't query ourselves.
                    return Task.CompletedTask;
                }
            }

            List<SsdpEventHandler> handlers;

            lock (_synchroniser)
            {
                if (!_events.ContainsKey(action))
                {
                    return Task.CompletedTask;
                }

                handlers = _events[action].ToList();
            }

            var args = new SsdpEventArgs(msg, receivedFrom, localIpAddress);
            if (DlnaVersion > DlnaVersion.Version1
                && msg.TryGetValue("SEARCHPORT.UPNP.ORG", out var alternative)
                && int.TryParse(alternative, out int port))
            {
                args.ReceivedFrom.Port = port;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler.Invoke(args);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    _logger.LogError(ex, "Error firing event: {Action}", action);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handler for network change events.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Network availability information.</param>
        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            _logger.LogDebug("Network availability changed.");
            OnNetworkChanged();
        }

        /// <summary>
        /// Handler for network change events.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event arguments.</param>
        private void OnNetworkAddressChanged(object? sender, System.EventArgs e)
        {
            _logger.LogDebug("Network address change detected.");
            OnNetworkChanged();
        }

        /// <summary>
        /// Async task that waits for 2 seconds before reinitializing the settings, as typically these events fire multiple times in succession.
        /// </summary>
        /// <returns>The network change task.</returns>
        private async Task OnNetworkChangeAsync()
        {
            try
            {
                await Task.Delay(2000).ConfigureAwait(false);

                if (_running)
                {
                    Stop();

                    // Increase the network change count;
                    int nextId = int.Parse(_configId);
                    nextId++;
                    if (nextId > 99)
                    {
                        nextId = 1;
                    }

                    _configId = nextId.ToString(CultureInfo.InvariantCulture);

                    Start();
                }
            }
            finally
            {
                _eventfire = false;
            }
        }

        /// <summary>
        /// Called when the configuration has changed.
        /// </summary>
        private void ValidateConfiguration()
        {
            Configuration.UdpSendCount = Math.Clamp(Configuration.UdpSendCount, 1, 5);
            Configuration.UdpPortRange = string.IsNullOrEmpty(Configuration.UdpPortRange) ? "49152-65535" : Configuration.UdpPortRange;
            UdpHelper.EnableMultiSocketBinding = Configuration.EnableMultiSocketBinding;
            Configuration.DlnaVersion = (DlnaVersion)Math.Clamp(Configuration.Version, 0, 2);
            PermittedDevices = _networkManager.CreateIPCollection(Configuration.PermittedDevices, false, false);
            DeniedDevices = _networkManager.CreateIPCollection(Configuration.PermittedDevices, true, false);
            UpdateTracingFilter();
        }

        /// <summary>
        /// Triggers our event, and re-loads interface information.
        /// </summary>
        private void OnNetworkChanged()
        {
            lock (_eventFireLock)
            {
                if (_eventfire)
                {
                    return;
                }

                _logger.LogDebug("Network Address Change Event.");

                // As network events tend to fire one after the other only fire once every second.
                _eventfire = true;
                _ = OnNetworkChangeAsync();
            }
        }
    }
}
