using System;
using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="ContentFeatureBuilder"/>.
    /// </summary>
    public static class ContentFeatureBuilder
    {
        /// <summary>
        /// Builds a image header.
        /// </summary>
        /// <param name="profile">A <see cref="DeviceProfile"/>.</param>
        /// <param name="container">The container.</param>
        /// <param name="width">Optional width.</param>
        /// <param name="height">Optional height.</param>
        /// <param name="isDirectStream">True if the image is via direct stream.</param>
        /// <param name="orgPn">Optional organisation.</param>
        /// <returns>A string representation.</returns>
        public static string BuildImageHeader(
            DeviceProfile profile,
            string container,
            int? width,
            int? height,
            bool isDirectStream,
            string? orgPn = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            string orgOp = ";DLNA.ORG_OP=" + DlnaMaps.GetImageOrgOpValue();

            // 0 = native, 1 = transcoded
            var orgCi = isDirectStream ? ";DLNA.ORG_CI=0" : ";DLNA.ORG_CI=1";

            const DlnaFlags FlagValue = DlnaFlags.BackgroundTransferMode |
                                        DlnaFlags.InteractiveTransferMode |
                                        DlnaFlags.DlnaV15;

            string dlnaflags = string.Format(
                CultureInfo.InvariantCulture,
                ";DLNA.ORG_FLAGS={0}",
                DlnaMaps.FlagsToString(FlagValue));

            if (string.IsNullOrEmpty(orgPn))
            {
                var mediaProfile = profile.GetImageMediaProfile(
                    container,
                    width,
                    height);

                orgPn = mediaProfile?.OrgPn;

                if (string.IsNullOrEmpty(orgPn))
                {
                    orgPn = GetImageOrgPnValue(container, width, height);
                }
            }

            if (string.IsNullOrEmpty(orgPn))
            {
                return orgOp.TrimStart(';') + orgCi + dlnaflags;
            }

            return "DLNA.ORG_PN=" + orgPn + orgOp + orgCi + dlnaflags;
        }

        /// <summary>
        /// Builds an audio header.
        /// </summary>
        /// <param name="profile">A <see cref="DeviceProfile"/>.</param>
        /// <param name="container">The container.</param>
        /// <param name="audioCodec">The audio codec.</param>
        /// <param name="audioBitrate">Optional. The audio bitrate.</param>
        /// <param name="audioSampleRate">Optional. The sample audio rate.</param>
        /// <param name="audioChannels">Optional. The number of audio channels.</param>
        /// <param name="audioBitDepth">Optional. The audio bit depth.</param>
        /// <param name="isDirectStream">True if being accessed by audio stream.</param>
        /// <param name="runtimeTicks">Optional. The runtime ticks.</param>
        /// <param name="transcodeSeekInfo">An instance of <see cref="TranscodeSeekInfo"/>.</param>
        /// <returns>A string representation.</returns>
        public static string BuildAudioHeader(
            DeviceProfile profile,
            string container,
            string? audioCodec,
            int? audioBitrate,
            int? audioSampleRate,
            int? audioChannels,
            int? audioBitDepth,
            bool isDirectStream,
            long? runtimeTicks,
            TranscodeSeekInfo transcodeSeekInfo)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            // first bit means Time based seek supported, second byte range seek supported (not sure about the order now), so 01 = only byte seek, 10 = time based, 11 = both, 00 = none
            string orgOp = ";DLNA.ORG_OP=" + DlnaMaps.GetOrgOpValue(runtimeTicks > 0, isDirectStream, transcodeSeekInfo);

            // 0 = native, 1 = transcoded
            string orgCi = isDirectStream ? ";DLNA.ORG_CI=0" : ";DLNA.ORG_CI=1";

            const DlnaFlags FlagValue = DlnaFlags.StreamingTransferMode |
                                        DlnaFlags.BackgroundTransferMode |
                                        DlnaFlags.InteractiveTransferMode |
                                        DlnaFlags.DlnaV15;

            // if (isDirectStream)
            // {
            //     flagValue = flagValue | DlnaFlags.ByteBasedSeek;
            // }
            //  else if (runtimeTicks.HasValue)
            // {
            //     flagValue = flagValue | DlnaFlags.TimeBasedSeek;
            // }

            string dlnaflags = string.Format(
                CultureInfo.InvariantCulture,
                ";DLNA.ORG_FLAGS={0}",
                DlnaMaps.FlagsToString(FlagValue));

            var mediaProfile = profile.GetAudioMediaProfile(
                container,
                audioCodec,
                audioChannels,
                audioBitrate,
                audioSampleRate,
                audioBitDepth);

            var orgPn = mediaProfile?.OrgPn;

            if (string.IsNullOrEmpty(orgPn))
            {
                orgPn = GetAudioOrgPnValue(container, audioBitrate, audioSampleRate, audioChannels);
            }

            if (string.IsNullOrEmpty(orgPn))
            {
                return orgOp.TrimStart(';') + orgCi + dlnaflags;
            }

            return "DLNA.ORG_PN=" + orgPn + orgOp + orgCi + dlnaflags;
        }

        /// <summary>
        /// Builds a video header.
        /// </summary>
        /// <param name="profile">A <see cref="DeviceProfile"/>.</param>
        /// <param name="container">The container.</param>
        /// <param name="videoCodec">The video codec.</param>
        /// <param name="audioCodec">The audio codec.</param>
        /// <param name="width">Optional. The width.</param>
        /// <param name="height">Optional. The height.</param>
        /// <param name="bitDepth">Optional. The bit depth.</param>
        /// <param name="videoBitrate">Optional. The video bitrate.</param>
        /// <param name="timestamp">Optional. A <see cref="TransportStreamTimestamp"/>.</param>
        /// <param name="isDirectStream">True if the stream is playable by directStream.</param>
        /// <param name="runtimeTicks">Optional. Runtime ticks.</param>
        /// <param name="videoProfile">The video profile.</param>
        /// <param name="videoLevel">Optional. The video level.</param>
        /// <param name="videoFramerate">Optional. The video framerate.</param>
        /// <param name="packetLength">Optional. The packet length.</param>
        /// <param name="transcodeSeekInfo">The <see cref="TranscodeSeekInfo"/>.</param>
        /// <param name="isAnamorphic">Optional. True, if anamorphic.</param>
        /// <param name="isInterlaced">Optional. True, if interlaced.</param>
        /// <param name="refFrames">Optional. The number of reference frames.</param>
        /// <param name="numVideoStreams">Optional. The number of video streams.</param>
        /// <param name="numAudioStreams">Optional. The number of audio streams.</param>
        /// <param name="videoCodecTag">The video codec tag.</param>
        /// <param name="isAvc">True is AVC.</param>
        /// <returns>A list containing the video information.</returns>
        public static List<string> BuildVideoHeader(
            DeviceProfile profile,
            string container,
            string? videoCodec,
            string? audioCodec,
            int? width,
            int? height,
            int? bitDepth,
            int? videoBitrate,
            TransportStreamTimestamp timestamp,
            bool isDirectStream,
            long? runtimeTicks,
            string? videoProfile,
            double? videoLevel,
            float? videoFramerate,
            int? packetLength,
            TranscodeSeekInfo transcodeSeekInfo,
            bool? isAnamorphic,
            bool? isInterlaced,
            int? refFrames,
            int? numVideoStreams,
            int? numAudioStreams,
            string? videoCodecTag,
            bool? isAvc)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            // first bit means Time based seek supported, second byte range seek supported (not sure about the order now), so 01 = only byte seek, 10 = time based, 11 = both, 00 = none
            string orgOp = ";DLNA.ORG_OP=" + DlnaMaps.GetOrgOpValue(runtimeTicks > 0, isDirectStream, transcodeSeekInfo);

            // 0 = native, 1 = transcoded
            string orgCi = isDirectStream ? ";DLNA.ORG_CI=0" : ";DLNA.ORG_CI=1";

            var flagValue = DlnaFlags.StreamingTransferMode |
                                DlnaFlags.BackgroundTransferMode |
                                DlnaFlags.InteractiveTransferMode |
                                DlnaFlags.DlnaV15;

            if (isDirectStream)
            {
                flagValue |= DlnaFlags.ByteBasedSeek;
            }
            else if (runtimeTicks.HasValue)
            {
                flagValue |= DlnaFlags.TimeBasedSeek;
            }

            string dlnaflags = string.Format(CultureInfo.InvariantCulture, ";DLNA.ORG_FLAGS={0}", DlnaMaps.FlagsToString(flagValue));

            var mediaProfile = profile.GetVideoMediaProfile(
                container,
                audioCodec,
                videoCodec,
                width,
                height,
                bitDepth,
                videoBitrate,
                videoProfile,
                videoLevel,
                videoFramerate,
                packetLength,
                timestamp,
                isAnamorphic,
                isInterlaced,
                refFrames,
                numVideoStreams,
                numAudioStreams,
                videoCodecTag,
                isAvc);

            var orgPnValues = new List<string>();

            if (mediaProfile != null && !string.IsNullOrEmpty(mediaProfile.OrgPn))
            {
                orgPnValues.AddRange(mediaProfile.OrgPn.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                foreach (var s in GetVideoOrgPnValue(container, videoCodec, audioCodec, width, height, timestamp))
                {
                    orgPnValues.Add(s.ToString());
                    break;
                }
            }

            var contentFeatureList = new List<string>();

            foreach (string orgPn in orgPnValues)
            {
                if (string.IsNullOrEmpty(orgPn))
                {
                    contentFeatureList.Add(orgOp.TrimStart(';') + orgCi + dlnaflags);
                }
                else
                {
                    contentFeatureList.Add("DLNA.ORG_PN=" + orgPn + orgCi + dlnaflags);
                }
            }

            if (orgPnValues.Count == 0)
            {
                contentFeatureList.Add(orgOp.TrimStart(';') + orgCi + dlnaflags);
            }

            return contentFeatureList;
        }

        private static string GetImageOrgPnValue(string container, int? width, int? height)
        {
            MediaFormatProfile? format = MediaFormatProfileResolver.ResolveImageFormat(
                container,
                width,
                height);

            return format?.ToString() ?? string.Empty;
        }

        private static string GetAudioOrgPnValue(string container, int? audioBitrate, int? audioSampleRate, int? audioChannels)
        {
            MediaFormatProfile? format = MediaFormatProfileResolver.ResolveAudioFormat(
                container,
                audioBitrate,
                audioSampleRate,
                audioChannels);

            return format?.ToString() ?? string.Empty;
        }

        private static MediaFormatProfile[] GetVideoOrgPnValue(string container, string? videoCodec, string? audioCodec, int? width, int? height, TransportStreamTimestamp timestamp)
        {
            return MediaFormatProfileResolver.ResolveVideoFormat(container, videoCodec ?? string.Empty, audioCodec ?? string.Empty, width, height, timestamp);
        }
    }
}
