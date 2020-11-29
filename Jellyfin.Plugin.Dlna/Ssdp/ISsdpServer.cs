using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Jellyfin.Plugin.Dlna.Configuration;
using Jellyfin.Plugin.Dlna.EventArgs;
using Jellyfin.Plugin.Dlna.Model;
using MediaBrowser.Common.Net;

namespace Jellyfin.Plugin.Dlna.Ssdp
{
    /// <summary>
    /// Defines a delegate for SSDP events.
    /// </summary>
    /// <param name="args">The <see cref="SsdpEventArgs"/> arguments.</param>
    public delegate void SsdpEventHandler(SsdpEventArgs args);

    /// <summary>
    /// Interface for SsdpServer.
    /// </summary>
    public interface ISsdpServer
    {
        /// <summary>
        /// Gets a value indicating the ssdp configuration.
        /// </summary>
        SsdpConfiguration Configuration { get; }

        /// <summary>
        /// Gets or sets the range of UDP ports to use in communications as well as 1900.
        /// Shared with PlayTo plugin.
        /// </summary>
        string UdpPortRange { get; set; }

        /// <summary>
        /// Gets the number of times each udp packet should be sent.
        /// </summary>
        int UdpSendCount { get; }

        /// <summary>
        /// Gets or sets the Dlna level supported by this server.
        /// </summary>
        DlnaVersion DlnaVersion { get; set; }

        /// <summary>
        /// Gets or sets the host name to be used in SSDP packets.
        /// </summary>
        string UserAgent { get; set; }

        /// <summary>
        /// Gets the BOOTID.UPNP.ORG value.
        /// </summary>
        string BootId { get; }

        /// <summary>
        /// Gets the CONFIGID.UPNP.ORG value.
        /// </summary>
        string ConfigId { get; }

        /// <summary>
        /// Gets the NEXTBOOTID.UPNP.ORG value.
        /// </summary>
        string NextBootId { get; }

        /// <summary>
        /// Returns the status of the tracing.
        /// </summary>
        /// <param name="address">The <see cref="IPAddress"/> to match.</param>
        /// <param name="address2">Optional second <see cref="IPAddress"/> to match.</param>
        /// <returns>True if this address is being traced.</returns>
        bool IsTracing(IPAddress address, IPAddress? address2 = null);

        /// <summary>
        /// Adds an event.
        /// </summary>
        /// <param name="action">The string to event on.</param>
        /// <param name="handler">The handler to call.</param>
        void AddEvent(string action, SsdpEventHandler handler);

        /// <summary>
        /// Removes an event.
        /// </summary>
        /// <param name="action">The event to remove.</param>
        /// <param name="handler">The handler to remove.</param>
        void DeleteEvent(string action, SsdpEventHandler handler);

        /// <summary>
        /// Restarts the service, assigning a different set of interfaces.
        /// </summary>
        /// <param name="interfaces">An array of <see cref="IPNetAddress"/> containing a list of interfaces.</param>
        void UpdateInterfaces(IPNetAddress[] interfaces);

        /// <summary>
        /// Multicasts an SSDP package, across all relevant interfaces types.
        /// </summary>
        /// <param name="values">Values that make up the message.</param>
        /// <param name="classification">Classification of message to send.</param>
        /// <param name="limitToFamily">If provided, contains the address family of the message that we are advertising. eg. Don't advertise IP4 across IP6.</param>
        /// <param name="sendCount">Optional value indicating the number of times to transmit the message.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task SendMulticastSsdp(Dictionary<string, string> values, string classification, AddressFamily? limitToFamily = null, int? sendCount = null);

        /// <summary>
        /// Unicasts an SSDP message.
        /// </summary>
        /// <param name="values">Values that make up the message.</param>
        /// <param name="classification">Classification of message to send.</param>
        /// <param name="localIp">Local endpoint to use.</param>
        /// <param name="endPoint">Remote endpoint to transmit to.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task SendUnicastSsdp(Dictionary<string, string> values, string classification, IPAddress localIp, IPEndPoint endPoint);

        /// <summary>
        /// Called when the configuration has changed.
        /// </summary>
        void UpdateConfiguration();

        /// <summary>
        /// Returns the UDP port that is assigned to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">The <see cref="IPAddress"/>.</param>
        /// <returns>UDP port assigned, or 1900 if not found.</returns>
        int GetPortFor(IPAddress address);

        /// <summary>
        /// Increase the value of BOOTID.UPNP.ORG .
        /// </summary>
        void IncreaseBootId();
    }
}
