using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Jellyfin.Data.Entities;
using Jellyfin.Plugin.Dlna.Culture;
using Jellyfin.Plugin.Dlna.Model;
using Jellyfin.Plugin.Dlna.Ssdp;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Net;
using Microsoft.Extensions.Logging;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;
using Genre = MediaBrowser.Controller.Entities.Genre;
using Movie = MediaBrowser.Controller.Entities.Movies.Movie;
using MusicAlbum = MediaBrowser.Controller.Entities.Audio.MusicAlbum;
using Season = MediaBrowser.Controller.Entities.TV.Season;
using Series = MediaBrowser.Controller.Entities.TV.Series;
using XmlAttribute = MediaBrowser.Model.Dlna.XmlAttribute;

namespace Jellyfin.Plugin.Dlna.Didl
{
    /// <summary>
    /// Defines the <see cref="DidlBuilder" />.
    /// </summary>
    public class DidlBuilder
    {
        private const string NsDc = "http://purl.org/dc/elements/1.1/";
        private const string NsDidl = "urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/";
        private const string NsDlna = "urn:schemas-dlna-org:metadata-1-0/";
        private const string NsUpnp = "urn:schemas-upnp-org:metadata-1-0/upnp/";
        private readonly (int Width, int Height) _iconDefault;
        private readonly DeviceProfile _profile;
        private readonly IImageProcessor _imageProcessor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILocalizationManager _localization;
        private readonly ILogger _logger;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IUserDataManager _userDataManager;
        private readonly string _serverAddress;
        private readonly string? _accessToken;
        private readonly string[] _peopleTypes = { PersonType.Director, PersonType.Writer, PersonType.Producer, PersonType.Composer, "creator" };
        private readonly User? _user;

        /// <summary>
        /// Initializes a new instance of the <see cref="DidlBuilder"/> class, used for creating DLNA DIDL-Lite XML.
        /// </summary>
        /// <param name="profile">The <see cref="DeviceProfile"/> instance to use.</param>
        /// <param name="user">The <see cref="User"/> under which to operate.</param>
        /// <param name="imageProcessor">The <see cref="IImageProcessor"/> instance to use.</param>
        /// <param name="serverAddress">The server address to use.</param>
        /// <param name="accessToken">The accessToken to embed in the output.</param>
        /// <param name="userDataManager">The <see cref="IUserDataManager"/> instance to use.</param>
        /// <param name="localization">The <see cref="ILocalizationManager"/> instance to use.</param>
        /// <param name="mediaSourceManager">The <see cref="IMediaSourceManager"/> instance to use.</param>
        /// <param name="logger">The <see cref="ILogger"/> instance to use.</param>
        /// <param name="mediaEncoder">The <see cref="IMediaEncoder"/> instance to use.</param>
        /// <param name="libraryManager">The <see cref="ILibraryManager"/> instance to use.</param>
        public DidlBuilder(
            DeviceProfile profile,
            User? user,
            IImageProcessor imageProcessor,
            string serverAddress,
            string? accessToken,
            IUserDataManager userDataManager,
            ILocalizationManager localization,
            IMediaSourceManager mediaSourceManager,
            ILogger logger,
            IMediaEncoder mediaEncoder,
            ILibraryManager libraryManager)
        {
            _profile = profile;
            _user = user;
            _imageProcessor = imageProcessor;
            _serverAddress = serverAddress;
            _accessToken = accessToken;
            _userDataManager = userDataManager;
            _localization = localization;
            _mediaSourceManager = mediaSourceManager;
            _logger = logger;
            _mediaEncoder = mediaEncoder;
            _libraryManager = libraryManager;
            _iconDefault.Width = SsdpServer.Instance.Configuration.DefaultIconWidth;
            _iconDefault.Height = SsdpServer.Instance.Configuration.DefaultIconHeight;
        }

        /// <summary>
        /// Return true if the <paramref name="id"/> is a root item.
        /// </summary>
        /// <param name="id">The id to search for.</param>
        /// <returns>Result of the operation.</returns>
        public static bool IsIdRoot(string id)
            => string.IsNullOrWhiteSpace(id)
                || string.Equals(id, "0", StringComparison.Ordinal) // Samsung sometimes uses 1 as root
                || string.Equals(id, "1", StringComparison.Ordinal);

        /// <summary>
        /// Outputs the root attributes to the <see cref="XmlWriter"/>.
        /// </summary>
        /// <param name="profile">The <see cref="DeviceProfile"/> instance to use.</param>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        public static void WriteXmlRootAttributes(DeviceProfile profile, XmlWriter writer)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (profile.XmlRootAttributes != null)
            {
                for (int a = 0; a < profile.XmlRootAttributes.Length; a++)
                {
                    var att = profile.XmlRootAttributes[a];
                    int index = att.Name.IndexOf(':');
                    if (index == -1)
                    {
                        writer.WriteAttributeString(att.Name, att.Value);
                    }
                    else
                    {
                        writer.WriteAttributeString(att.Name[..index], att.Name[(index + 1)..], null, att.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Gets an Image Url.
        /// </summary>
        /// <param name="item">The image <see cref="BaseItem"/>.</param>
        /// <returns>The api url.</returns>
        public string? GetImageUrl(BaseItem item)
        {
            var imageInfo = GetImageInfo(item);

            if (imageInfo == null)
            {
                return null;
            }

            var (url, _, _) = GetImageUrl(
                imageInfo,
                _profile.MaxAlbumArtWidth ?? _iconDefault.Width,
                _profile.MaxAlbumArtHeight ?? _iconDefault.Height,
                "jpg");

            return url;
        }

        /// <summary>
        /// Returns an XML document describing the object in <paramref name="item"/>.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="user">The <see cref="User"/> instance to use.</param>
        /// <param name="context">A <see cref="BaseItem"/> that describes the context in which <paramref name="item"/> is to be viewed.</param>
        /// <param name="deviceId">The id of the device whose profile will be used.</param>
        /// <param name="filter">The filter to use in selecting the properties to export.</param>
        /// <param name="streamInfo">The <see cref="StreamInfo"/> containing additional information.</param>
        /// <returns>The XML representation.</returns>
        public string GetItemDidl(BaseItem item, User? user, Folder? context, string deviceId, (bool All, string[] Fields) filter, StreamInfo streamInfo)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = false,
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment,
                CheckCharacters = false
            };

            var builder = new StringBuilder(8192);

            using var writer = XmlWriter.Create(builder, settings);
            writer.WriteStartElement(string.Empty, "DIDL-Lite", NsDidl);
            writer.WriteAttributeString("xmlns", "dc", null, NsDc);
            writer.WriteAttributeString("xmlns", "dlna", null, NsDlna);
            writer.WriteAttributeString("xmlns", "upnp", null, NsUpnp);
            WriteXmlRootAttributes(_profile, writer);
            WriteItemElement(writer, item, user, context, null, deviceId, filter, streamInfo);
            writer.WriteFullEndElement();
            writer.Flush();

            return builder.ToString();
        }

        /// <summary>
        /// Writes a dlna element to the provided <see cref="XmlWriter"/>.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="user">The <see cref="User"/> under which to operate.</param>
        /// <param name="context">A <see cref="BaseItem"/> that describes the context in which <paramref name="item"/> is to be viewed.</param>
        /// <param name="contextStubType">The context's type as a <see cref="StubType"/>.</param>
        /// <param name="deviceId">The deviceId to use with the audio/video stream.</param>
        /// <param name="filter">The filter to use in selecting the properties to export.</param>
        /// <param name="streamInfo">The <see cref="StreamInfo"/> containing additional information.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Writer is not null.")]
        public void WriteItemElement(
            XmlWriter writer,
            BaseItem item,
            User? user,
            Folder? context,
            StubType? contextStubType,
            string deviceId,
            (bool All, string[] Fields) filter,
            StreamInfo? streamInfo = null)
        {
            writer.WriteStartElement(string.Empty, "item", NsDidl);
            writer.WriteAttributeString("restricted", "1");
            writer.WriteAttributeString("id", item.Id.ToString("N", CultureInfo.InvariantCulture));

            if (context != null)
            {
                writer.WriteAttributeString("parentID", GetClientId(context.Id, contextStubType));
            }
            else
            {
                var parent = item.DisplayParentId;
                if (!parent.Equals(Guid.Empty))
                {
                    writer.WriteAttributeString("parentID", GetClientId(parent, null));
                }
            }

            var mediaType = GetMediaType(item);
            AddCommonFields(item, null, context, writer, filter, mediaType);
            AddGeneralProperties(item, writer);
            AddSamsungBookmarkInfo(item, user, writer, streamInfo);
            if (item is IHasMediaSources)
            {
                if (mediaType == DlnaProfileType.Audio)
                {
                    AddAudioResource(writer, item, deviceId, filter, streamInfo);
                }
                else if (mediaType == DlnaProfileType.Video)
                {
                    AddVideoResource(writer, item, deviceId, filter, streamInfo);
                }
            }

            AddCover(item, null, writer, mediaType);
            writer.WriteFullEndElement();
        }

        /// <summary>
        /// Adds a folder element.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="stubType">The folder's <see cref="StubType"/>, or null.</param>
        /// <param name="context">A <see cref="BaseItem"/> that describes the context in which <paramref name="item"/> is to be viewed.</param>
        /// <param name="childCount">The number of items to describe.</param>
        /// <param name="filter">The filter to use in selecting the properties to export.</param>
        /// <param name="noId">True if the element has no id.</param>
        public void WriteFolderElement(
            XmlWriter writer,
            BaseItem item,
            StubType? stubType,
            Folder? context,
            int childCount,
            (bool All, string[] Fields) filter,
            bool noId = false)
        {
            const string ParentId = "parentID";

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            writer.WriteStartElement(string.Empty, "container", NsDidl);
            writer.WriteAttributeString("restricted", "1");
            writer.WriteAttributeString("searchable", "1");
            writer.WriteAttributeString("childCount", childCount.ToString(CultureDefault.UsCulture));

            var clientId = GetClientId(item.Id, stubType);

            if (noId)
            {
                writer.WriteAttributeString("id", "0");
                writer.WriteAttributeString(ParentId, "-1");
            }
            else
            {
                writer.WriteAttributeString("id", clientId);
                writer.WriteAttributeString(ParentId, GetParentId());
            }

            var mediaType = GetMediaType(item);
            AddCommonFields(item, stubType, context, writer, filter, mediaType);
            AddGeneralProperties(item, writer);
            AddCover(item, stubType, writer, mediaType);
            writer.WriteFullEndElement();

            string GetParentId()
            {
                if (stubType == StubType.ContinueWatching || stubType == StubType.Latest)
                {
                    // Continue watching, parent is it's normal self.
                    return item.Id.ToString("N", CultureInfo.InvariantCulture);
                }

                if (item.Parent != null && item.Parent.IsRoot)
                {
                    if (stubType == StubType.Folder)
                    {
                        var name = _localization.GetLocalizedString("Folders");
                        return _libraryManager.GetNamedView(name, CollectionType.Folders, string.Empty).Id.ToString("N", CultureInfo.InvariantCulture);
                    }

                    // if the parent is the root, return zero.
                    return "0";
                }

                var emptyGuid = item.DisplayParentId.Equals(Guid.Empty);
                if (context != null)
                {
                    // if we are a root, or have an empty guid, return zero.
                    if (context.IsRoot || emptyGuid)
                    {
                        return "0";
                    }

                    // otherwise return our parent's id.
                    return GetClientId(context.Id, stubType);
                }

                if (!emptyGuid)
                {
                    // if we have a guid, then return that.
                    return GetClientId(item.DisplayParentId, stubType);
                }

                return "0";
            }
        }

        /// <summary>
        /// Returns a client Id.
        /// </summary>
        /// <param name="idValue">The <see cref="Guid"/>.</param>
        /// <param name="stubType">The <see cref="StubType"/>, or null.</param>
        /// <returns>The client id.</returns>
        private static string GetClientId(Guid idValue, StubType? stubType)
        {
            var id = idValue.ToString("N", CultureInfo.InvariantCulture);

            if (stubType.HasValue)
            {
                id = stubType.Value.ToString().ToLowerInvariant() + "_" + id;
            }

            return id;
        }

        /// <summary>
        /// Parses <see cref="BaseItem.MediaType"/> into a <see cref="DlnaProfileType"/>.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> instance.</param>
        /// <returns>The <see cref="DlnaProfileType"/> equivalent.</returns>
        /// <exception cref="ArgumentException">If <see cref="BaseItem.MediaType"/> is unknown.</exception>
        private static DlnaProfileType? GetMediaType(BaseItem item)
        {
            if (string.Equals(item.MediaType, MediaType.Audio, StringComparison.OrdinalIgnoreCase))
            {
                return DlnaProfileType.Audio;
            }

            if (string.Equals(item.MediaType, MediaType.Video, StringComparison.OrdinalIgnoreCase))
            {
                return DlnaProfileType.Video;
            }

            if (string.Equals(item.MediaType, MediaType.Photo, StringComparison.OrdinalIgnoreCase))
            {
                return DlnaProfileType.Photo;
            }

            return null;
        }

        /// <summary>
        /// Returns the first parent with an image, that appears below the user root.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <returns>A <see cref="BaseItem"/>, or null if one does not exist.</returns>
        private static BaseItem? GetFirstParentWithImageBelowUserRoot(BaseItem item)
        {
            while (true)
            {
                if (item == null)
                {
                    return null;
                }

                if (item.HasImage(ImageType.Primary))
                {
                    return item;
                }

                var parent = item.GetParent();
                switch (parent)
                {
                    case UserRootFolder:
                        // terminate in case we went past user root folder (unlikely?)
                    case Folder { IsRoot: true }:
                        return null;
                    default:
                        item = parent;
                        break;
                }
            }
        }

        /// <summary>
        /// Gets complete episode number.
        /// </summary>
        /// <param name="episode">The <see cref="Episode"/> to describe.</param>
        /// <returns>For single episodes returns just the number. For double episodes - current and ending numbers.</returns>
        private static string GetEpisodeIndexFullName(Episode episode)
        {
            if (!episode.IndexNumber.HasValue)
            {
                return string.Empty;
            }

            var name = episode.IndexNumber.Value.ToString("00", CultureInfo.InvariantCulture);

            if (episode.IndexNumberEnd.HasValue)
            {
                name += "-" + episode.IndexNumberEnd.Value.ToString("00", CultureInfo.InvariantCulture);
            }

            return name;
        }

        /// <summary>
        /// Gets episode number formatted as 'S##E##'.
        /// </summary>
        /// <param name="episode">The <see cref="Episode"/>.</param>
        /// <returns>Formatted episode number.</returns>
        private static string GetEpisodeNumberDisplayName(Episode episode)
        {
            var seasonNumber = episode.Season?.IndexNumber;

            var name = seasonNumber.HasValue ? "S" + seasonNumber.Value.ToString("00", CultureInfo.InvariantCulture) : string.Empty;

            var indexName = GetEpisodeIndexFullName(episode);

            if (!string.IsNullOrWhiteSpace(indexName))
            {
                name += "E" + indexName;
            }

            return name;
        }

        /// <summary>
        /// If present, returns the mime type of <paramref name="mediaProfile"/>. Otherwise it extracts it from <paramref name="url"/>.
        /// </summary>
        /// <param name="url">The Uri.</param>
        /// <param name="mediaProfile">Optional. The <see cref="ResponseProfile"/> instance.</param>
        /// <returns>The mime-type.</returns>
        private static string GetMimeType(string url, ResponseProfile? mediaProfile)
        {
            if (mediaProfile == null || string.IsNullOrEmpty(mediaProfile.MimeType))
            {
                var uri = new Uri(url);
                var len = uri.Segments.Length;
                var filename = len == 0 ? uri.LocalPath : uri.Segments[len - 1];

                return MimeTypes.GetMimeType(filename, true)!;
            }

            return mediaProfile.MimeType;
        }

        /// <summary>
        /// Adds a Video Resource.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="video">The <see cref="BaseItem"/> video to describe.</param>
        /// <param name="deviceId">The deviceId to use with the audio/video stream.</param>
        /// <param name="filter">The filter to use in selecting the properties to export.</param>
        /// <param name="streamInfo">The <see cref="StreamInfo"/> containing additional information.</param>
        private void AddVideoResource(XmlWriter writer, BaseItem video, string deviceId, (bool All, string[] Fields) filter, StreamInfo? streamInfo)
        {
            if (streamInfo == null)
            {
                var sources = _mediaSourceManager.GetStaticMediaSources(video, true, _user);

                streamInfo = new StreamBuilder(_mediaEncoder, _logger)
                    .BuildVideoItem(
                        new VideoOptions
                        {
                            ItemId = video.Id,
                            MediaSources = sources.ToArray(),
                            Profile = _profile,
                            DeviceId = deviceId,
                            MaxBitrate = _profile.MaxStreamingBitrate
                        });

                if (streamInfo == null)
                {
                    return;
                }
            }

            var targetWidth = streamInfo.TargetWidth;
            var targetHeight = streamInfo.TargetHeight;

            if (streamInfo.Container != null)
            {
                var contentFeatureList = ContentFeatureBuilder.BuildVideoHeader(
                    _profile,
                    streamInfo.Container,
                    streamInfo.TargetVideoCodec.Length > 0 ? streamInfo.TargetVideoCodec[0] : null,
                    streamInfo.TargetAudioCodec.Length > 0 ? streamInfo.TargetAudioCodec[0] : null,
                    targetWidth,
                    targetHeight,
                    streamInfo.TargetVideoBitDepth,
                    streamInfo.TargetVideoBitrate,
                    streamInfo.TargetTimestamp,
                    streamInfo.IsDirectStream,
                    streamInfo.RunTimeTicks ?? 0,
                    streamInfo.TargetVideoProfile,
                    streamInfo.TargetVideoLevel,
                    streamInfo.TargetFramerate ?? 0,
                    streamInfo.TargetPacketLength,
                    streamInfo.TranscodeSeekInfo,
                    streamInfo.IsTargetAnamorphic,
                    streamInfo.IsTargetInterlaced,
                    streamInfo.TargetRefFrames,
                    streamInfo.TargetVideoStreamCount,
                    streamInfo.TargetAudioStreamCount,
                    streamInfo.TargetVideoCodecTag,
                    streamInfo.IsTargetAVC);

                foreach (var contentFeature in contentFeatureList)
                {
                    AddVideoResource(writer, filter, contentFeature, streamInfo);
                }
            }

            var subtitleProfiles = streamInfo.GetSubtitleProfiles(_mediaEncoder, false, _serverAddress, _accessToken);

            foreach (var subtitle in subtitleProfiles)
            {
                if (subtitle.DeliveryMethod != SubtitleDeliveryMethod.External)
                {
                    continue;
                }

                var subtitleAdded = AddSubtitleElement(writer, subtitle);

                if (subtitleAdded && _profile.EnableSingleSubtitleLimit)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Adds a Subtitle Element.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="info">The <see cref="SubtitleStreamInfo"/> containing the subtitles.</param>
        /// <returns><c>True</c> if the operation was successful.</returns>
        private bool AddSubtitleElement(XmlWriter writer, SubtitleStreamInfo info)
        {
            var subtitleProfile = Array.Find(
                _profile.SubtitleProfiles,
                i => i.Method == SubtitleDeliveryMethod.External && string.Equals(info.Format, i.Format, StringComparison.OrdinalIgnoreCase));

            if (subtitleProfile == null)
            {
                return false;
            }

            var subtitleMode = subtitleProfile.DidlMode;
            if (string.Equals(subtitleMode, "smi", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteStartElement(string.Empty, "res", NsDidl);
                writer.WriteAttributeString("protocolInfo", "http-get:*:smi/caption:*");
                writer.WriteString(XmlUtilities.EncodeUrl(info.Url));
                writer.WriteFullEndElement();
                return true;
            }

            if (string.Equals(subtitleMode, "CaptionInfoEx", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteStartElement("sec", "CaptionInfoEx", null);
                writer.WriteAttributeString("sec", "type", null, info.Format.ToLowerInvariant());
                writer.WriteString(XmlUtilities.EncodeUrl(info.Url));
                writer.WriteFullEndElement();
                return true;
            }

            writer.WriteStartElement(string.Empty, "res", NsDidl);
            writer.WriteAttributeString("protocolInfo", string.Format(CultureInfo.InvariantCulture, "http-get:*:text/{0}:*", info.Format.ToLowerInvariant()));
            writer.WriteString(XmlUtilities.EncodeUrl(info.Url));
            writer.WriteFullEndElement();
            return true;
        }

        /// <summary>
        /// Adds a video resource.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="filter">The filter to use in selecting the properties to export.</param>
        /// <param name="contentFeatures">The additional DLNA content features that are supported. eg. DLNA.ORG_PN=MPEG_PS_PAL.</param>
        /// <param name="streamInfo">The <see cref="StreamInfo"/> of the video.</param>
        private void AddVideoResource(XmlWriter writer, (bool All, string[] Fields) filter, string contentFeatures, StreamInfo streamInfo)
        {
            writer.WriteStartElement(string.Empty, "res", NsDidl);

            var url = streamInfo.ToUrl(_serverAddress, _accessToken, "&dlna=true");

            var mediaSource = streamInfo.MediaSource;
            if (mediaSource?.RunTimeTicks != null)
            {
                writer.WriteAttributeString("duration", TimeSpan.FromTicks(mediaSource.RunTimeTicks.Value).ToString("c", CultureDefault.UsCulture));
            }

            if (filter.Contains("res@size"))
            {
                if (streamInfo.IsDirectStream || streamInfo.EstimateContentLength)
                {
                    var size = streamInfo.TargetSize;

                    if (size.HasValue)
                    {
                        writer.WriteAttributeString("size", size.Value.ToString(CultureDefault.UsCulture));
                    }
                }
            }

            var totalBitrate = streamInfo.TargetTotalBitrate;
            var targetSampleRate = streamInfo.TargetAudioSampleRate;
            var targetChannels = streamInfo.TargetAudioChannels;

            var targetWidth = streamInfo.TargetWidth;
            var targetHeight = streamInfo.TargetHeight;

            if (targetChannels.HasValue)
            {
                writer.WriteAttributeString("nrAudioChannels", targetChannels.Value.ToString(CultureDefault.UsCulture));
            }

            if (targetWidth.HasValue && targetHeight.HasValue && filter.Contains("res@resolution"))
            {
                writer.WriteAttributeString("resolution", $"{targetWidth.Value}x{targetHeight.Value}");
            }

            if (targetSampleRate.HasValue)
            {
                writer.WriteAttributeString("sampleFrequency", targetSampleRate.Value.ToString(CultureDefault.UsCulture));
            }

            if (totalBitrate.HasValue && totalBitrate.Value != 0)
            {
                writer.WriteAttributeString("bitrate", totalBitrate.Value.ToString(CultureDefault.UsCulture));
            }

            var mediaProfile = _profile.GetVideoMediaProfile(
                streamInfo.Container,
                streamInfo.TargetAudioCodec.Length > 0 ? streamInfo.TargetAudioCodec[0] : null,
                streamInfo.TargetVideoCodec.Length > 0 ? streamInfo.TargetVideoCodec[0] : null,
                streamInfo.TargetAudioBitrate,
                targetWidth,
                targetHeight,
                streamInfo.TargetVideoBitDepth,
                streamInfo.TargetVideoProfile,
                streamInfo.TargetVideoLevel,
                streamInfo.TargetFramerate ?? 0,
                streamInfo.TargetPacketLength,
                streamInfo.TargetTimestamp,
                streamInfo.IsTargetAnamorphic,
                streamInfo.IsTargetInterlaced,
                streamInfo.TargetRefFrames,
                streamInfo.TargetVideoStreamCount,
                streamInfo.TargetAudioStreamCount,
                streamInfo.TargetVideoCodecTag,
                streamInfo.IsTargetAVC);

            string mimeType = GetMimeType(url, mediaProfile);
            writer.WriteAttributeString("protocolInfo", $"http-get:*:{mimeType}:{contentFeatures}");
            writer.WriteString(XmlUtilities.EncodeUrl(url));
            writer.WriteFullEndElement();
        }

        /// <summary>
        /// Returns the display name for the item.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="itemStubType">The item's <see cref="StubType"/> classification.</param>
        /// <param name="context">A <see cref="BaseItem"/> that describes the context in which <paramref name="item"/> is to be viewed.</param>
        /// <remark>
        /// If <paramref name="context"/> is a season, this will return a string containing just episode number and name.
        /// Otherwise the result will include series names and season number.
        /// </remark>
        /// <returns>The display name.</returns>
        private string GetDisplayName(BaseItem item, StubType? itemStubType, BaseItem? context)
        {
            if (itemStubType.HasValue)
            {
                switch (itemStubType.Value)
                {
                    case StubType.AlbumArtists: return _localization.GetLocalizedString("HeaderAlbumArtists");
                    case StubType.Albums: return _localization.GetLocalizedString("Albums");
                    case StubType.Artists: return _localization.GetLocalizedString("Artists");
                    case StubType.Collections: return _localization.GetLocalizedString("Collections");
                    case StubType.ContinueWatching: return _localization.GetLocalizedString("HeaderContinueWatching");
                    case StubType.FavoriteAlbums: return _localization.GetLocalizedString("HeaderFavoriteAlbums");
                    case StubType.FavoriteArtists: return _localization.GetLocalizedString("HeaderFavoriteArtists");
                    case StubType.FavoriteEpisodes: return _localization.GetLocalizedString("HeaderFavoriteEpisodes");
                    case StubType.Favorites: return _localization.GetLocalizedString("Favorites");
                    case StubType.FavoriteSeries: return _localization.GetLocalizedString("HeaderFavoriteShows");
                    case StubType.FavoriteSongs: return _localization.GetLocalizedString("HeaderFavoriteSongs");
                    case StubType.Genres: return _localization.GetLocalizedString("Genres");
                    case StubType.Latest: return _localization.GetLocalizedString("Latest");
                    case StubType.Movies: return _localization.GetLocalizedString("Movies");
                    case StubType.NextUp: return _localization.GetLocalizedString("HeaderNextUp");
                    case StubType.Playlists: return _localization.GetLocalizedString("Playlists");
                    case StubType.Series: return _localization.GetLocalizedString("Shows");
                    case StubType.Songs: return _localization.GetLocalizedString("Songs");
                }
            }

            if (item is MusicAlbum album && itemStubType == StubType.Folder)
            {
                return album.FileNameWithoutExtension;
            }

            return item is Episode episode
                ? GetEpisodeDisplayName(episode, context)
                : item.Name;
        }

        /// <summary>
        /// Gets episode display name appropriate for the given context.
        /// </summary>
        /// <remarks>
        /// If <paramref name="context"/> is a season, this will return a string containing just episode number and name.
        /// Otherwise the result will include series names and season number.
        /// </remarks>
        /// <param name="episode">The <see cref="Episode"/> to describe.</param>
        /// <param name="context">A <see cref="BaseItem"/> that describes the context in which <paramref name="episode"/> is to be viewed.</param>
        /// <returns>Formatted name of the episode.</returns>
        private string GetEpisodeDisplayName(Episode episode, BaseItem? context)
        {
            if (context is Season season)
            {
                // This is a special embedded within a season
                if (episode.ParentIndexNumber.HasValue && episode.ParentIndexNumber.Value == 0
                    && season.IndexNumber.HasValue && season.IndexNumber.Value != 0)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        _localization.GetLocalizedString("ValueSpecialEpisodeName"),
                        episode.Name);
                }

                // inside a season use simple format (ex. '12 - Episode Name')
                return GetEpisodeIndexFullName(episode) + " - " + episode.Name;
            }

            // outside a season include series and season details (ex. 'TV Show - S05E11 - Episode Name')
            return episode.SeriesName + " - " + GetEpisodeNumberDisplayName(episode) + " - " + episode.Name;
        }

        /// <summary>
        /// Adds an audio resource.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="audio">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="deviceId">The deviceId to use with the audio stream.</param>
        /// <param name="filter">The filter to use in selecting the properties to export.</param>
        /// <param name="streamInfo">The <see cref="StreamInfo"/> containing additional information.</param>
        private void AddAudioResource(XmlWriter writer, BaseItem audio, string deviceId, (bool All, string[] Fields) filter, StreamInfo? streamInfo = null)
        {
            writer.WriteStartElement(string.Empty, "res", NsDidl);

            if (streamInfo == null)
            {
                var sources = _mediaSourceManager.GetStaticMediaSources(audio, true, _user);

                streamInfo = new StreamBuilder(_mediaEncoder, _logger).BuildAudioItem(
                    new AudioOptions
                    {
                        ItemId = audio.Id,
                        MediaSources = sources.ToArray(),
                        Profile = _profile,
                        DeviceId = deviceId
                    });

                if (streamInfo == null)
                {
                    return;
                }
            }

            var url = streamInfo.ToUrl(_serverAddress, _accessToken, "&dlna=true");

            var mediaSource = streamInfo.MediaSource;
            if (mediaSource?.RunTimeTicks != null)
            {
                writer.WriteAttributeString("duration", TimeSpan.FromTicks(mediaSource.RunTimeTicks.Value).ToString("c", CultureDefault.UsCulture));
            }

            if ((streamInfo.IsDirectStream || streamInfo.EstimateContentLength) && filter.Contains("res@size"))
            {
                var size = streamInfo.TargetSize;

                if (size.HasValue)
                {
                    writer.WriteAttributeString("size", size.Value.ToString(CultureDefault.UsCulture));
                }
            }

            var targetAudioBitrate = streamInfo.TargetAudioBitrate;
            var targetSampleRate = streamInfo.TargetAudioSampleRate;
            var targetChannels = streamInfo.TargetAudioChannels;
            var targetAudioBitDepth = streamInfo.TargetAudioBitDepth;

            if (targetChannels.HasValue)
            {
                writer.WriteAttributeString("nrAudioChannels", targetChannels.Value.ToString(CultureDefault.UsCulture));
            }

            if (targetSampleRate.HasValue)
            {
                writer.WriteAttributeString("sampleFrequency", targetSampleRate.Value.ToString(CultureDefault.UsCulture));
            }

            if (targetAudioBitrate.HasValue)
            {
                writer.WriteAttributeString("bitrate", targetAudioBitrate.Value.ToString(CultureDefault.UsCulture));
            }

            if (streamInfo.Container != null)
            {
                var audioCodec = streamInfo.TargetAudioCodec.Length > 0 ? streamInfo.TargetAudioCodec[0] : null;
                var mediaProfile = _profile.GetAudioMediaProfile(
                    streamInfo.Container,
                    audioCodec,
                    targetChannels,
                    targetAudioBitrate,
                    targetSampleRate,
                    targetAudioBitDepth);

                var contentFeatures = ContentFeatureBuilder.BuildAudioHeader(
                    _profile,
                    streamInfo.Container,
                    audioCodec,
                    targetAudioBitrate,
                    targetSampleRate,
                    targetChannels,
                    targetAudioBitDepth,
                    streamInfo.IsDirectStream,
                    streamInfo.RunTimeTicks ?? 0,
                    streamInfo.TranscodeSeekInfo);

                var mimeType = GetMimeType(url, mediaProfile);
                writer.WriteAttributeString("protocolInfo", $"http-get:*:{mimeType}:{contentFeatures}");
            }

            writer.WriteString(XmlUtilities.EncodeUrl(url));
            writer.WriteFullEndElement();
        }

        /// <summary>
        /// Adds a Samsung Bookmark.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="user">The <see cref="User"/> under which to operate.</param>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="streamInfo">The <see cref="StreamInfo"/> containing the bookmark data.</param>
        private void AddSamsungBookmarkInfo(BaseItem item, User? user, XmlWriter writer, StreamInfo? streamInfo)
        {
            if (_profile.XmlRootAttributes == null || !item.SupportsPositionTicksResume || item is Folder)
            {
                return;
            }

            XmlAttribute? secAttribute = null;
            for (int a = 0; a < _profile.XmlRootAttributes.Length; a++)
            {
                var attribute = _profile.XmlRootAttributes[a];
                if (!string.Equals(attribute.Name, "xmlns:sec", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                secAttribute = attribute;
                break;
            }

            // Not a Samsung device
            if (secAttribute == null)
            {
                return;
            }

            var userdata = _userDataManager.GetUserData(user, item);
            var playbackPositionTicks = streamInfo != null && streamInfo.StartPositionTicks > 0
                ? streamInfo.StartPositionTicks
                : userdata.PlaybackPositionTicks;

            if (playbackPositionTicks <= 0)
            {
                return;
            }

            var elementValue = string.Format(
                CultureInfo.InvariantCulture,
                "BM={0}",
                Convert.ToInt32(TimeSpan.FromTicks(playbackPositionTicks).TotalSeconds));
            writer.WriteElementString("sec", "dcmInfo", secAttribute.Value, elementValue);
        }

        /// <summary>
        /// Adds fields used by both items and folders.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="itemStubType">The item's <see cref="StubType"/>, or null.</param>
        /// <param name="context">A <see cref="BaseItem"/> that describes the context in which <paramref name="item"/> is to be viewed.</param>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="filter">The filter to use in selecting the properties to export.</param>
        /// <param name="mediaType">A <see cref="DlnaProfileType"/> containing the media type of <paramref name="item"/>.</param>
        private void AddCommonFields(
            BaseItem item,
            StubType? itemStubType,
            BaseItem? context,
            XmlWriter writer,
            (bool All, string[] Fields) filter,
            DlnaProfileType? mediaType)
        {
            writer.WriteElementString("dc", "title", NsDc, GetDisplayName(item, itemStubType, context));

            // More types here
            // http://oss.linn.co.uk/repos/Public/LibUpnpCil/DidlLite/UpnpAv/Test/TestDidlLite.cs
            writer.WriteStartElement("upnp", "class", NsUpnp);
            WriteObjectClass(writer, item, itemStubType, mediaType);
            writer.WriteFullEndElement();

            if (filter.Contains("dc:date"))
            {
                if (item.PremiereDate.HasValue)
                {
                    writer.WriteElementString("dc", "date", NsDc, item.PremiereDate.Value.ToString("o", CultureInfo.InvariantCulture));
                }
            }

            if (filter.Contains("upnp:genre"))
            {
                for (var a = 0; a < item.Genres.Length; a++)
                {
                    writer.WriteElementString("upnp", "genre", NsUpnp, item.Genres[a]);
                }
            }

            for (var a = 0; a < item.Studios.Length; a++)
            {
                writer.WriteElementString("upnp", "publisher", NsUpnp, item.Studios[a]);
            }

            if (item is not Folder)
            {
                if (filter.Contains("dc:description"))
                {
                    var desc = item.Overview;

                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        writer.WriteElementString("dc", "description", NsDc, desc);
                    }
                }
            }

            if (!string.IsNullOrEmpty(item.OfficialRating))
            {
                if (filter.Contains("dc:rating"))
                {
                    writer.WriteElementString("dc", "rating", NsDc, item.OfficialRating);
                }

                if (filter.Contains("upnp:rating"))
                {
                    writer.WriteElementString("upnp", "rating", NsUpnp, item.OfficialRating);
                }
            }

            AddPeople(item, writer);
        }

        /// <summary>
        /// Adds an Object Class.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="stubType">The item's <see cref="StubType"/>, or null.</param>
        /// <param name="mediaType">A <see cref="DlnaProfileType"/> containing the media type of <paramref name="item"/>.</param>
        private void WriteObjectClass(XmlWriter writer, BaseItem item, StubType? stubType, DlnaProfileType? mediaType)
        {
            if ((item.IsDisplayedAsFolder || stubType.HasValue) && !_profile.RequiresPlainFolders)
            {
                switch (item)
                {
                    case MusicAlbum:
                        writer.WriteString("object.container.album.musicAlbum");
                        return;
                    case MusicArtist:
                        writer.WriteString("object.container.person.musicArtist");
                        return;
                    case Series:
                    case Season:
                    case BoxSet:
                    case Video:
                        writer.WriteString("object.container.album.videoAlbum");
                        return;
                    case Playlist:
                        writer.WriteString("object.container.playlistContainer");
                        return;
                    case PhotoAlbum:
                        writer.WriteString("object.container.album.photoAlbum");
                        return;
                    default:
                        writer.WriteString("object.container.storageFolder");
                        return;
                }
            }

            if (mediaType == DlnaProfileType.Audio)
            {
                writer.WriteString("object.item.audioItem.musicTrack");
                return;
            }

            if (mediaType == DlnaProfileType.Photo)
            {
                writer.WriteString("object.item.imageItem.photo");
                return;
            }

            if (mediaType == DlnaProfileType.Video)
            {
                if (!_profile.RequiresPlainVideoItems && item is Movie)
                {
                    writer.WriteString("object.item.videoItem.movie");
                    return;
                }

                if (!_profile.RequiresPlainVideoItems && item is MusicVideo)
                {
                    writer.WriteString("object.item.videoItem.musicVideoClip");
                    return;
                }

                writer.WriteString("object.item.videoItem");
                return;
            }

            switch (item)
            {
                case MusicGenre:
                    writer.WriteString(_profile.RequiresPlainFolders ? "object.container.storageFolder" : "object.container.genre.musicGenre");
                    return;

                case Genre:
                    writer.WriteString(_profile.RequiresPlainFolders ? "object.container.storageFolder" : "object.container.genre");
                    return;

                default:
                    writer.WriteString("object.item");
                    return;
            }
        }

        /// <summary>
        /// Adds a person.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        private void AddPeople(BaseItem item, XmlWriter writer)
        {
            if (!item.SupportsPeople)
            {
                return;
            }

            // Seeing some LG models locking up due content with large lists of people
            // The actual issue might just be due to processing a more metadata than it can handle
            var people = _libraryManager.GetPeople(
                new InternalPeopleQuery
                {
                    ItemId = item.Id,
                    Limit = 6
                });

            foreach (var actor in people)
            {
                var type = PersonType.Actor;
                for (int a = 0; a < _peopleTypes.Length; a++)
                {
                    var peopleType = _peopleTypes[a];
                    if (string.Equals(peopleType, actor.Type, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(peopleType, actor.Role, StringComparison.OrdinalIgnoreCase))
                    {
                        type = peopleType;
                        break;
                    }
                }

                writer.WriteElementString("upnp", type.ToLowerInvariant(), NsUpnp, actor.Name);
            }
        }

        /// <summary>
        /// Adds general properties.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        private void AddGeneralProperties(BaseItem item, XmlWriter writer)
        {
            var hasAlbumArtists = item as IHasAlbumArtist;

            if (item is IHasArtist hasArtists)
            {
                foreach (var artist in hasArtists.Artists)
                {
                    writer.WriteElementString("upnp", "artist", NsUpnp, artist);
                    writer.WriteElementString("dc", "creator", NsDc, artist);

                    // If it doesn't support album artists (musicvideo), then tag as both
                    if (hasAlbumArtists == null)
                    {
                        AddAlbumArtist(writer, artist);
                    }
                }

                writer.WriteString("\r\n");
            }

            if (hasAlbumArtists != null)
            {
                foreach (var albumArtist in hasAlbumArtists.AlbumArtists)
                {
                    AddAlbumArtist(writer, albumArtist);
                }

                writer.WriteString("\r\n");
            }

            if (!string.IsNullOrWhiteSpace(item.Album))
            {
                writer.WriteElementString("upnp", "album", NsUpnp, item.Album);
            }

            if (!item.IndexNumber.HasValue)
            {
                return;
            }

            writer.WriteElementString("upnp", "originalTrackNumber", NsUpnp, item.IndexNumber.Value.ToString(CultureDefault.UsCulture));

            if (item is Episode)
            {
                writer.WriteElementString("upnp", "episodeNumber", NsUpnp, item.IndexNumber.Value.ToString(CultureDefault.UsCulture));
            }
        }

        /// <summary>
        /// Adds an Album Artist.
        /// </summary>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="name">The name to add.</param>
        private void AddAlbumArtist(XmlWriter writer, string name)
        {
            try
            {
                writer.WriteStartElement("upnp", "artist", NsUpnp);
                writer.WriteAttributeString("role", "AlbumArtist");
                writer.WriteString(name);
                writer.WriteFullEndElement();
            }
            catch (XmlException ex)
            {
                _logger.LogError(ex, "Error adding xml value: {Value}", name);
            }
        }

        /// <summary>
        /// Adds a Cover.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="stubType">The item's <see cref="StubType"/>.</param>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="mediaType">A <see cref="DlnaProfileType"/> containing the media type of <paramref name="item"/>.</param>
        private void AddCover(BaseItem item, StubType? stubType, XmlWriter writer, DlnaProfileType? mediaType)
        {
            var imageInfo = GetImageInfo(item);

            if (imageInfo == null)
            {
                return;
            }

            var (url, _, _) = GetImageUrl(
                imageInfo,
                _profile.MaxAlbumArtWidth ?? _iconDefault.Width,
                _profile.MaxAlbumArtHeight ?? _iconDefault.Height,
                "jpg");

            var isPhoto = mediaType == DlnaProfileType.Photo;
            if (!isPhoto)
            {
                writer.WriteStartElement("upnp", "albumArtURI", NsUpnp);

                if (!string.IsNullOrEmpty(_profile.AlbumArtPn))
                {
                    writer.WriteAttributeString("dlna", "profileID", NsDlna, _profile.AlbumArtPn);
                }

                writer.WriteString(XmlUtilities.EncodeUrl(url));
                writer.WriteFullEndElement();

                (url, _, _) = GetImageUrl(
                    imageInfo,
                    _profile.MaxIconWidth ?? _iconDefault.Width,
                    _profile.MaxIconHeight ?? _iconDefault.Height,
                    "jpg");

                writer.WriteElementString("upnp", "icon", NsUpnp, XmlUtilities.EncodeUrl(url));

                if (!_profile.EnableAlbumArtInDidl && mediaType.HasValue && !stubType.HasValue)
                {
                    return;
                }
            }

            if (!_profile.EnableSingleAlbumArtLimit || isPhoto)
            {
                AddImageResElement(item, writer, 4096, 4096, "jpg", "JPEG_LRG");
                AddImageResElement(item, writer, 1024, 768, "jpg", "JPEG_MED");
                AddImageResElement(item, writer, 640, 480, "jpg", "JPEG_SM");
                AddImageResElement(item, writer, 4096, 4096, "png", "PNG_LRG");
                AddImageResElement(item, writer, 160, 160, "png", "PNG_TN");
            }

            AddImageResElement(item, writer, 160, 160, "jpg", "JPEG_TN");
        }

        /// <summary>
        /// Adds an Image Resource Element.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="writer">The <see cref="XmlWriter"/> instance to write to.</param>
        /// <param name="maxWidth">The maximum width for the image.</param>
        /// <param name="maxHeight">The maximum height for the image.</param>
        /// <param name="format">The image format to use.</param>
        /// <param name="orgPn">It's ORG_PN value.</param>
        private void AddImageResElement(
            BaseItem item,
            XmlWriter writer,
            int maxWidth,
            int maxHeight,
            string format,
            string orgPn)
        {
            var imageInfo = GetImageInfo(item);

            if (imageInfo == null)
            {
                return;
            }

            var (url, width, height) = GetImageUrl(imageInfo, maxWidth, maxHeight, format);

            // Images must have a reported size or many clients (Bubble upnp), will only use the first thumbnail
            // rather than using a larger one when available
            width ??= maxWidth;
            height ??= maxHeight;

            if (width > _profile.MaxAlbumArtWidth || height > _profile.MaxAlbumArtHeight)
            {
                return;
            }

            writer.WriteStartElement(string.Empty, "res", NsDidl);

            var contentFeatures = ContentFeatureBuilder.BuildImageHeader(_profile, format, width, height, imageInfo.IsDirectStream, orgPn);

            writer.WriteAttributeString(
                "protocolInfo",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "http-get:*:{0}:{1}",
                    MimeTypes.GetMimeType("file." + format),
                    contentFeatures));

            writer.WriteAttributeString("resolution", $"{width}x{height}");
            writer.WriteString(XmlUtilities.EncodeUrl(url));
            writer.WriteFullEndElement();
        }

        /// <summary>
        /// Returns information about an image.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> which has the image.</param>
        /// <returns>A <see cref="ImageDownloadInfo"/> containing the information.</returns>
        private ImageDownloadInfo? GetImageInfo(BaseItem item)
        {
            if (item.HasImage(ImageType.Primary))
            {
                return GetImageInfo(item, ImageType.Primary);
            }

            if (item.HasImage(ImageType.Thumb))
            {
                return GetImageInfo(item, ImageType.Thumb);
            }

            if ((item is Channel) && item.HasImage(ImageType.Backdrop))
            {
                return GetImageInfo(item, ImageType.Backdrop);
            }

            switch (item)
            {
                // For audio tracks without art use album art if available.
                case Audio audioItem:
                    {
                        var album = audioItem.AlbumEntity;
                        return album != null && album.HasImage(ImageType.Primary)
                            ? GetImageInfo(album, ImageType.Primary)
                            : null;
                    }

                // Don't look beyond album/playlist level. Metadata service may assign an image from a different album/show to the parent folder.
                case MusicAlbum:
                case Playlist:
                    return null;
            }

            // For other item types check parents, but be aware that image retrieved from a parent may be not suitable for this media item.
            var parentWithImage = GetFirstParentWithImageBelowUserRoot(item);
            return parentWithImage != null ? GetImageInfo(parentWithImage, ImageType.Primary) : null;
        }

        /// <summary>
        /// Returns the Image information for an item.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/> to describe.</param>
        /// <param name="type">The image type to return as a <see cref="ImageType"/>.</param>
        /// <returns>The images' <see cref="ImageDownloadInfo"/>.</returns>
        private ImageDownloadInfo GetImageInfo(BaseItem item, ImageType type)
        {
            var imageInfo = item.GetImageInfo(type, 0);
            string? tag = _imageProcessor.GetImageCacheTag(item, type);

            int? width = imageInfo.Width;
            int? height = imageInfo.Height;

            if (width <= 0 || height <= 0)
            {
                width = null;
                height = null;
            }

            var inputFormat = Path.GetExtension(imageInfo.Path)
                .Replace(".jpeg", ".jpg", StringComparison.OrdinalIgnoreCase);

            return new ImageDownloadInfo(item.Id, type, tag, width, height, inputFormat);
        }

        /// <summary>
        /// Returns an image url.
        /// </summary>
        /// <param name="info">The <see cref="ImageDownloadInfo"/>.</param>
        /// <param name="maxWidth">The maximum width.</param>
        /// <param name="maxHeight">The maximum height.</param>
        /// <param name="format">The image format.</param>
        /// <returns>A named tuple of containing the url, width, and height"/>.</returns>
        private (string Url, int? Width, int? Height) GetImageUrl(ImageDownloadInfo info, int maxWidth, int maxHeight, string format)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/Items/{1}/Images/{2}/0/{3}/{4}/{5}/{6}/0/0",
                _serverAddress,
                info.ItemId.ToString("N", CultureInfo.InvariantCulture),
                info.Type,
                info.ImageTag,
                format,
                maxWidth.ToString(CultureInfo.InvariantCulture),
                maxHeight.ToString(CultureInfo.InvariantCulture));

            var width = info.Width;
            var height = info.Height;

            info.IsDirectStream = false;

            if (width.HasValue && height.HasValue)
            {
                var newSize = DrawingUtils.Resize(new ImageDimensions(width.Value, height.Value), 0, 0, maxWidth, maxHeight);
                var normalizedFormat = format.Replace("jpeg", "jpg", StringComparison.OrdinalIgnoreCase);

                if (string.Equals(info.Format, normalizedFormat, StringComparison.OrdinalIgnoreCase))
                {
                    info.IsDirectStream = maxWidth >= newSize.Width && maxHeight >= newSize.Height;
                    _logger.LogDebug("Direct stream meets size requirements : {DirectStream}", info.IsDirectStream);
                }

                return (url, newSize.Width, newSize.Height);
            }

            // just lie
            info.IsDirectStream = true;

            return (url, width, height);
        }

        private class ImageDownloadInfo
        {
            public ImageDownloadInfo(Guid itemId, ImageType type, string tag, int? width, int? height, string format)
            {
                ItemId = itemId;
                Type = type;
                Width = width;
                Height = height;
                Format = format;
                ImageTag = tag;
            }

            /// <summary>
            /// Gets the image Id.
            /// </summary>
            internal Guid ItemId { get; }

            /// <summary>
            /// Gets the image Tag.
            /// </summary>
            internal string? ImageTag { get; }

            /// <summary>
            /// Gets the type of the image.
            /// </summary>
            internal ImageType Type { get; }

            /// <summary>
            /// Gets the image's width.
            /// </summary>
            internal int? Width { get; }

            /// <summary>
            /// Gets the image's height.
            /// </summary>
            internal int? Height { get; }

            /// <summary>
            /// Gets or sets a value indicating whether it can be direct streamed.
            /// </summary>
            internal bool IsDirectStream { get; set; }

            /// <summary>
            /// Gets the format of the image.
            /// </summary>
            internal string? Format { get; }
        }
    }
}
