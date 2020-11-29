using System;
using System.Globalization;
using System.Linq;
using System.Net;
using Jellyfin.Api.Helpers;
using Jellyfin.Api.Models.StreamingDtos;
using Jellyfin.DeviceProfiles;
using Jellyfin.Plugin.Dlna.Model;
using MediaBrowser.Controller.Devices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.Dlna.Helpers
{
    /// <summary>
    /// Defines the <see cref="DlnaStreamHelper"/>.
    /// </summary>
    public static class DlnaStreamHelper
    {
        /// <summary>
        /// Gets or sets the profile manager for this helper unit.
        /// </summary>
        public static IDeviceProfileManager? ProfileManager { get; set; }

        /// <summary>
        /// Streaming event handler callback.
        /// </summary>
        /// <param name="sender">Ignore.</param>
        /// <param name="args">Streaming information in a <see cref="StreamingHelpers"/>.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Created by event trigger.")]
#pragma warning disable IDE0060 // Remove unused parameter
        public static void StreamEventProcessor(object? sender, StreamEventArgs args)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            switch (args.Type)
            {
                case StreamEventType.OnHeaderProcessing:
                    ParseDlnaHeaders(args.Request!, args.StreamingRequest!);
                    break;

                case StreamEventType.OnStreamStart:
                    AddDlnaHeaders(args.State!, args.ResponseHeaders!, ProfileManager!, args.IsStaticallyStreamed, args.StartTimeTicks, args.Request!);
                    break;

                default:
                    ApplyDeviceProfileSettings(
                        args.State!,
                        args.DeviceManager!,
                        ProfileManager!,
                        args.Request!,
                        args.DeviceProfileId,
                        args.IsStaticallyStreamed);
                    break;
            }
        }

        /// <summary>
        /// Adds the dlna headers.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <param name="responseHeaders">The response headers.</param>
        /// <param name="profileManager">The <see cref="IDeviceProfileManager"/> instance.</param>
        /// <param name="isStaticallyStreamed">if set to <c>true</c> [is statically streamed].</param>
        /// <param name="startTimeTicks">The start time in ticks.</param>
        /// <param name="request">The <see cref="HttpRequest"/>.</param>
        private static void AddDlnaHeaders(
            StreamState state,
            IHeaderDictionary responseHeaders,
            IDeviceProfileManager profileManager,
            bool isStaticallyStreamed,
            long? startTimeTicks,
            HttpRequest request)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (responseHeaders == null)
            {
                throw new ArgumentNullException(nameof(responseHeaders));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var enableDlnaHeaders = request.Query.TryGetValue("dlna", out _) ||
                !string.Equals(request.Headers["GetContentFeatures.DLNA.ORG"], "1", StringComparison.Ordinal);

            if (!enableDlnaHeaders)
            {
                return;
            }

            var profile = state.DeviceProfile;

            StringValues transferMode = request.Headers["transferMode.dlna.org"];
            responseHeaders.Add("transferMode.dlna.org", string.IsNullOrEmpty(transferMode) ? "Streaming" : transferMode.ToString());
            responseHeaders.Add("realTimeInfo.dlna.org", "DLNA.ORG_TLAG=*");

            if (state.RunTimeTicks.HasValue)
            {
                if (string.Equals(request.Headers["getMediaInfo.sec"], "1", StringComparison.OrdinalIgnoreCase))
                {
                    var ms = TimeSpan.FromTicks(state.RunTimeTicks.Value).TotalMilliseconds;
                    responseHeaders.Add("MediaInfo.sec", string.Format(
                        CultureInfo.InvariantCulture,
                        "SEC_Duration={0};",
                        Convert.ToInt32(ms)));
                }

                if (!isStaticallyStreamed && profile != null)
                {
                    AddTimeSeekResponseHeaders(state, responseHeaders, startTimeTicks);
                }
            }

            // if the profile hasn't been assigned see if there is one that matches.
            profile ??= profileManager.GetProfile(
                request.Headers,
                request.HttpContext.Connection.RemoteIpAddress ?? IPAddress.Loopback,
                null);

            var audioCodec = state.ActualOutputAudioCodec;

            if (!state.IsVideoRequest)
            {
                responseHeaders.Add("contentFeatures.dlna.org", ContentFeatureBuilder.BuildAudioHeader(
                    profile,
                    state.OutputContainer,
                    audioCodec,
                    state.OutputAudioBitrate,
                    state.OutputAudioSampleRate,
                    state.OutputAudioChannels,
                    state.OutputAudioBitDepth,
                    isStaticallyStreamed,
                    state.RunTimeTicks,
                    state.TranscodeSeekInfo));
            }
            else
            {
                var videoCodec = state.ActualOutputVideoCodec;

                responseHeaders.Add(
                    "contentFeatures.dlna.org",
                    ContentFeatureBuilder.BuildVideoHeader(profile, state.OutputContainer, videoCodec, audioCodec, state.OutputWidth, state.OutputHeight, state.TargetVideoBitDepth, state.OutputVideoBitrate, state.TargetTimestamp, isStaticallyStreamed, state.RunTimeTicks, state.TargetVideoProfile, state.TargetVideoLevel, state.TargetFramerate, state.TargetPacketLength, state.TranscodeSeekInfo, state.IsTargetAnamorphic, state.IsTargetInterlaced, state.TargetRefFrames, state.TargetVideoStreamCount, state.TargetAudioStreamCount, state.TargetVideoCodecTag, state.IsTargetAVC).FirstOrDefault() ?? string.Empty);
            }
        }

        /// <summary>
        /// Parses dlna headers.
        /// </summary>
        /// <param name="request">A <see cref="HttpRequest"/> instance.</param>
        /// <param name="streamingRequest">A <see cref="StreamingRequestDto"/> instance.</param>
        private static void ParseDlnaHeaders(HttpRequest request, StreamingRequestDto streamingRequest)
        {
            if (streamingRequest.StartTimeTicks.HasValue)
            {
                return;
            }

            var timeSeek = request.Headers["TimeSeekRange.dlna.org"];
            streamingRequest.StartTimeTicks = ParseTimeSeekHeader(timeSeek.ToString());
        }

        /// <summary>
        /// Applies the device profile to the streamstate.
        /// </summary>
        /// <param name="state">A <see cref="StreamState"/> instance.</param>
        /// <param name="deviceManager">The <see cref="IDeviceManager"/> instance.</param>
        /// <param name="profileManager">The <see cref="IDeviceProfileManager"/> instance.</param>
        /// <param name="request">A <see cref="HttpRequest"/> instance.</param>
        /// <param name="deviceProfileId">Optional. Device profile id. </param>
        /// <param name="static">True if static.</param>
        private static void ApplyDeviceProfileSettings(
            StreamState state,
            IDeviceManager deviceManager,
            IDeviceProfileManager profileManager,
            HttpRequest request,
            string? deviceProfileId,
            bool? @static)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (deviceManager == null)
            {
                throw new ArgumentNullException(nameof(deviceManager));
            }

            if (!string.IsNullOrWhiteSpace(deviceProfileId))
            {
                state.DeviceProfile = profileManager.GetProfile(Guid.Parse(deviceProfileId), true);

                if (state.DeviceProfile == null)
                {
                    var caps = deviceManager.GetCapabilities(deviceProfileId);
                    state.DeviceProfile = profileManager.GetProfile(
                        request.Headers,
                        request.HttpContext.Connection.RemoteIpAddress ?? IPAddress.Loopback,
                        caps?.DeviceProfile);
                }
            }

            var profile = state.DeviceProfile;

            if (profile == null)
            {
                // Don't use settings from the default profile.
                // Only use a specific profile if it was requested.
                return;
            }

            var audioCodec = state.ActualOutputAudioCodec;
            var videoCodec = state.ActualOutputVideoCodec;

            var mediaProfile = !state.IsVideoRequest
                ? profile.GetAudioMediaProfile(state.OutputContainer, audioCodec, state.OutputAudioChannels, state.OutputAudioBitrate, state.OutputAudioSampleRate, state.OutputAudioBitDepth)
                : profile.GetVideoMediaProfile(
                    state.OutputContainer,
                    audioCodec,
                    videoCodec,
                    state.OutputWidth,
                    state.OutputHeight,
                    state.TargetVideoBitDepth,
                    state.OutputVideoBitrate,
                    state.TargetVideoProfile,
                    state.TargetVideoLevel,
                    state.TargetFramerate,
                    state.TargetPacketLength,
                    state.TargetTimestamp,
                    state.IsTargetAnamorphic,
                    state.IsTargetInterlaced,
                    state.TargetRefFrames,
                    state.TargetVideoStreamCount,
                    state.TargetAudioStreamCount,
                    state.TargetVideoCodecTag,
                    state.IsTargetAVC);

            if (mediaProfile != null)
            {
                state.MimeType = mediaProfile.MimeType;
            }

            if (@static.HasValue && @static.Value)
            {
                return;
            }

            var transcodingProfile = !state.IsVideoRequest ? profile.GetAudioTranscodingProfile(state.OutputContainer, audioCodec) : profile.GetVideoTranscodingProfile(state.OutputContainer, audioCodec, videoCodec);

            if (transcodingProfile == null)
            {
                return;
            }

            state.EstimateContentLength = transcodingProfile.EstimateContentLength;
            // state.EnableMpegtsM2TsMode = transcodingProfile.EnableMpegtsM2TsMode;
            state.TranscodeSeekInfo = transcodingProfile.TranscodeSeekInfo;

            if (state.VideoRequest == null)
            {
                return;
            }

            state.VideoRequest.CopyTimestamps = transcodingProfile.CopyTimestamps;
            state.VideoRequest.EnableSubtitlesInManifest = transcodingProfile.EnableSubtitlesInManifest;
        }

        /// <summary>
        /// Adds the dlna time seek headers to the response.
        /// </summary>
        /// <param name="state">The current <see cref="StreamState"/>.</param>
        /// <param name="responseHeaders">The <see cref="IHeaderDictionary"/> of the response.</param>
        /// <param name="startTimeTicks">The start time in ticks.</param>
        private static void AddTimeSeekResponseHeaders(StreamState state, IHeaderDictionary responseHeaders, long? startTimeTicks)
        {
            var runtimeSeconds = TimeSpan.FromTicks(state.RunTimeTicks!.Value).TotalSeconds.ToString(CultureInfo.InvariantCulture);
            var startSeconds = TimeSpan.FromTicks(startTimeTicks ?? 0).TotalSeconds.ToString(CultureInfo.InvariantCulture);

            responseHeaders.Add("TimeSeekRange.dlna.org", string.Format(
                CultureInfo.InvariantCulture,
                "npt={0}-{1}/{1}",
                startSeconds,
                runtimeSeconds));
            responseHeaders.Add("X-AvailableSeekRange", string.Format(
                CultureInfo.InvariantCulture,
                "1 npt={0}-{1}",
                startSeconds,
                runtimeSeconds));
        }

        /// <summary>
        /// Parses the time seek header.
        /// </summary>
        /// <param name="value">The time seek header string.</param>
        /// <returns>A nullable <see cref="long"/> representing the seek time in ticks.</returns>
        private static long? ParseTimeSeekHeader(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
            {
                return null;
            }

            const string Npt = "npt=";
            if (!value.StartsWith(Npt, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid timeseek header");
            }

            var index = value.IndexOf('-');
            value = index == -1
                ? value[Npt.Length..]
                : value[Npt.Length..index];
            if (value.IndexOf(':') == -1)
            {
                // Parses npt times in the format of '417.33'
                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds).Ticks;
                }

                throw new ArgumentException("Invalid timeseek header");
            }

            try
            {
                // Parses npt times in the format of '10:19:25.7'
                return TimeSpan.Parse(value).Ticks;
            }
            catch
            {
                throw new ArgumentException("Invalid timeseek header");
            }
        }
    }
}
