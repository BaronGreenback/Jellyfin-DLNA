using System;
using System.Globalization;
using System.Security;
using System.Text;
using Jellyfin.Plugin.Dlna.Culture;
using Jellyfin.Plugin.Dlna.Model;
using MediaBrowser.Controller;
using MediaBrowser.Model.Dlna;

namespace Jellyfin.Plugin.Dlna.Didl
{
    /// <summary>
    /// Defines the <see cref="DescriptionXmlBuilder" />.
    /// </summary>
    public class DescriptionXmlBuilder
    {
        private const string PngImage = "image/png";
        private const string JpgImage = "image/jpeg";
        private static readonly DeviceService[] _services =
        {
            new(
                "urn:schemas-upnp-org:service:ContentDirectory:1",
                "urn:upnp-org:serviceId:ContentDirectory",
                "/ContentDirectory/ContentDirectory.xml",
                "/ContentDirectory/Control",
                "/ContentDirectory/Events"),

            new(
                "urn:schemas-upnp-org:service:ConnectionManager:1",
                "urn:upnp-org:serviceId:ConnectionManager",
                "/ConnectionManager/ConnectionManager.xml",
                "/ConnectionManager/Control",
                "/ConnectionManager/Events"),
            new(
                "urn:microsoft.com:service:X_MS_MediaReceiverRegistrar:1",
                "urn:microsoft.com:serviceId:X_MS_MediaReceiverRegistrar",
                "/MediaReceiverRegistrar/MediaReceiverRegistrar.xml",
                "/MediaReceiverRegistrar/control",
                "/MediaReceiverRegistrar/events")
        };

        private static DeviceIcon[]? _icons;

        private readonly Guid _serverId;
        private readonly string _serverIdStr;
        private readonly string _serverName;
        private readonly DlnaVersion _dlnaVersion;
        private readonly IServerApplicationHost _appHost;

        private readonly string _serverAddress;
        private readonly bool _enableMediaReceiverRegistrar;
        private readonly DeviceProfile _profile;

        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptionXmlBuilder"/> class.
        /// </summary>
        /// <param name="profile">The <see cref="DeviceProfile"/> instance.</param>
        /// <param name="enableMediaReceiverRegistrar">Set to true to enable MSMediaReceiverRegistrar.</param>
        /// <param name="serverId">The UDN of the server as a GUID.</param>
        /// <param name="appHost">The <see cref="IServerApplicationHost"/> instance.</param>
        /// <param name="serverAddress">The url of the home page.</param>
        /// <param name="serverName">The server name to use.</param>
        /// <param name="dlnaVersion">The dlna version to support.</param>
        public DescriptionXmlBuilder(
            DeviceProfile profile,
            bool enableMediaReceiverRegistrar,
            Guid serverId,
            string serverAddress,
            IServerApplicationHost appHost,
            string serverName,
            DlnaVersion dlnaVersion)
        {
            _profile = profile;
            _serverId = serverId;
            _serverIdStr = serverId.ToString("N", CultureInfo.InvariantCulture);
            _serverName = serverName;
            _serverAddress = serverAddress;
            _enableMediaReceiverRegistrar = enableMediaReceiverRegistrar;
            _appHost = appHost;
            _dlnaVersion = dlnaVersion;
            _icons ??= new[]
            {
                new DeviceIcon(_serverIdStr, PngImage, 24, 240, 240, "/logo240.png"),
                new DeviceIcon(_serverIdStr, JpgImage, 24, 240, 240, "/logo240.jpg"),
                new DeviceIcon(_serverIdStr, PngImage, 24, 120, 120, "/logo120.png"),
                new DeviceIcon(_serverIdStr, JpgImage, 24, 120, 120, "/logo120.jpg"),
                new DeviceIcon(_serverIdStr, PngImage, 24, 48, 48, "/logo48.png"),
                new DeviceIcon(_serverIdStr, JpgImage, 24, 48, 48, "/logo48.jpg")
            };
        }

        /// <summary>
        /// Builds the XML response.
        /// </summary>
        /// <returns>The XML string.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder(2048);
            builder.Append("<?xml version=\"1.0\"?><root xmlns=\"urn:schemas-upnp-org:device-1-0\" xmlns:dlna=\"urn:schemas-dlna-org:device-1-0\"><specVersion><major>");

            switch (_dlnaVersion)
            {
                case DlnaVersion.Version1:
                    builder.Append("1</major><minor>0");
                    break;
                case DlnaVersion.Version1_1:
                    builder.Append("1</major><minor>1");
                    break;
                case DlnaVersion.Version2:
                    builder.Append("2</major><minor>0");
                    break;
            }

            builder.Append("</minor></specVersion><device>");

            // Add device properties

            builder.Append("<dlna:X_DLNACAP/>")
                .Append("<dlna:X_DLNADOC xmlns:dlna=\"urn:schemas-dlna-org:device-1-0\">DMS-1.50</dlna:X_DLNADOC><dlna:X_DLNADOC xmlns:dlna=\"urn:schemas-dlna-org:device-1-0\">M-DMS-1.50</dlna:X_DLNADOC><deviceType>urn:schemas-upnp-org:device:MediaServer:1</deviceType><friendlyName>")
                .Append(SecurityElement.Escape(_serverName))
                .Append("</friendlyName><manufacturer>Jellyfin</manufacturer><manufacturerURL>https://github.com/jellyfin/jellyfin</manufacturerURL><modelDescription>UPnP/AV ");

            switch (_dlnaVersion)
            {
                case DlnaVersion.Version1:
                    builder.Append("1.0");
                    break;
                case DlnaVersion.Version1_1:
                    builder.Append("1.1");
                    break;
                case DlnaVersion.Version2:
                    builder.Append("2.0");
                    break;
            }

            builder.Append(" Compliant Media Server</modelDescription><modelName>Jellyfin Server</modelName><modelURL>https://github.com/jellyfin/jellyfin</modelURL><modelNumber>")
                .Append(_appHost.ApplicationVersionString)
                .Append("</modelNumber><serialNumber>")
                .Append(_serverIdStr)
                .Append("</serialNumber><UPC/><UDN>uuid:")
                .Append(_serverId.ToString("D", CultureInfo.InvariantCulture))
                .Append("</UDN>");

            if (!string.IsNullOrEmpty(_profile.SonyAggregationFlags))
            {
                builder.Append("<av:aggregationFlags xmlns:av=\"urn:schemas-sony-com:av\">")
                    .Append(SecurityElement.Escape(_profile.SonyAggregationFlags))
                    .Append("</av:aggregationFlags>");
            }

            builder.Append("<presentationURL>")
                .Append(XmlUtilities.EncodeUrl(_serverAddress))
                .Append("/web/index.html</presentationURL><iconList>");

            // Append Icon List

            for (var i = 0; i < _icons!.Length; i++)
            {
                var icon = _icons[i];
                builder.Append("<icon><mimetype>")
                    .Append(icon.MimeType)
                    .Append("</mimetype><width>")
                    .Append(icon.Width.ToString(CultureDefault.UsCulture))
                    .Append("</width><height>")
                    .Append(icon.Height.ToString(CultureDefault.UsCulture))
                    .Append("</height><depth>")
                    .Append(icon.Depth.ToString(CultureDefault.UsCulture))
                    .Append("</depth><url>")
                    .Append(icon.Url)
                    .Append("</url></icon>");
            }

            builder.Append("</iconList><serviceList>");

            // Append service list

            int serviceCount = _enableMediaReceiverRegistrar ? 3 : 2;
            for (var i = 0; i < serviceCount; i++)
            {
                var service = _services[i];
                builder.Append("<service><serviceType>")
                    .Append(service.ServiceType)
                    .Append("</serviceType><serviceId>")
                    .Append(service.ServiceId)
                    .Append("</serviceId><SCPDURL>/dlna/")
                    .Append(_serverIdStr)
                    .Append(service.ScpdUrl)
                    .Append("</SCPDURL><controlURL>/dlna/")
                    .Append(_serverIdStr)
                    .Append(service.ControlUrl)
                    .Append("</controlURL><eventSubURL>/dlna/")
                    .Append(_serverIdStr)
                    .Append(service.EventSubUrl)
                    .Append("</eventSubURL></service>");
            }

            builder.Append("</serviceList></device></root>");
            return builder.ToString();
        }
    }
}
