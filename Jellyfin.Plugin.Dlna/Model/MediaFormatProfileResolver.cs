using System;
using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="MediaFormatProfileResolver"/>.
    /// </summary>
    internal static class MediaFormatProfileResolver
    {
        /// <summary>
        /// Resolves a video format.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="videoCodec">The video codec.</param>
        /// <param name="audioCodec">The audio codec.</param>
        /// <param name="width">Optional. The width.</param>
        /// <param name="height">Optional. The height.</param>
        /// <param name="timestampType">A <see cref="TransportStreamTimestamp"/>.</param>
        /// <returns>An array of <see cref="MediaFormatProfile"/>.</returns>
        public static MediaFormatProfile[] ResolveVideoFormat(string container, string videoCodec, string audioCodec, int? width, int? height, TransportStreamTimestamp timestampType)
        {
            if (string.Equals(container, "asf", StringComparison.OrdinalIgnoreCase))
            {
                var val = ResolveVideoASFFormat(videoCodec, audioCodec, width, height);
                return val.HasValue ? new[] { val.Value } : Array.Empty<MediaFormatProfile>();
            }

            if (string.Equals(container, "mp4", StringComparison.OrdinalIgnoreCase))
            {
                var val = ResolveVideoMP4Format(videoCodec, audioCodec, width, height);
                return val.HasValue ? new[] { val.Value } : Array.Empty<MediaFormatProfile>();
            }

            if (string.Equals(container, "avi", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediaFormatProfile.AVI };
            }

            if (string.Equals(container, "mkv", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediaFormatProfile.MATROSKA };
            }

            if (string.Equals(container, "mpeg2ps", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(container, "ts", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediaFormatProfile.MPEG_PS_NTSC, MediaFormatProfile.MPEG_PS_PAL };
            }

            if (string.Equals(container, "mpeg1video", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediaFormatProfile.MPEG1 };
            }

            if (string.Equals(container, "mpeg2ts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(container, "mpegts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(container, "m2ts", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveVideoMPEG2TSFormat(videoCodec, audioCodec, width, height, timestampType);
            }

            if (string.Equals(container, "flv", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediaFormatProfile.FLV };
            }

            if (string.Equals(container, "wtv", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediaFormatProfile.WTV };
            }

            if (string.Equals(container, "3gp", StringComparison.OrdinalIgnoreCase))
            {
                var val = ResolveVideo3GPFormat(videoCodec, audioCodec);
                return val.HasValue ? new[] { val.Value } : Array.Empty<MediaFormatProfile>();
            }

            if (string.Equals(container, "ogv", StringComparison.OrdinalIgnoreCase) || string.Equals(container, "ogg", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediaFormatProfile.OGV };
            }

            return Array.Empty<MediaFormatProfile>();
        }

        /// <summary>
        /// Resolves an audio format.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="bitrate">Optional. The bitrate.</param>
        /// <param name="frequency">Optional. The frequency.</param>
        /// <param name="channels">Optional. The number of channels.</param>
        /// <returns>A <see cref="MediaFormatProfile"/> or null if unable to resolve.</returns>
        public static MediaFormatProfile? ResolveAudioFormat(string container, int? bitrate, int? frequency, int? channels)
        {
            if (string.Equals(container, "asf", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveAudioASFFormat(bitrate);
            }

            if (string.Equals(container, "mp3", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.MP3;
            }

            if (string.Equals(container, "lpcm", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveAudioLPCMFormat(frequency, channels);
            }

            if (string.Equals(container, "mp4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(container, "aac", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveAudioMP4Format(bitrate);
            }

            if (string.Equals(container, "adts", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveAudioADTSFormat(bitrate);
            }

            if (string.Equals(container, "flac", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.FLAC;
            }

            if (string.Equals(container, "oga", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(container, "ogg", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.OGG;
            }

            return null;
        }

        /// <summary>
        /// Resolves an image format.
        /// </summary>
        /// <param name="container">Container.</param>
        /// <param name="width">Optional image width.</param>
        /// <param name="height">Optional image height.</param>
        /// <returns>An <see cref="MediaFormatProfile"/> containing the best match.</returns>
        public static MediaFormatProfile? ResolveImageFormat(string container, int? width, int? height)
        {
            if (string.Equals(container, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(container, "jpg", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveImageJPGFormat(width, height);
            }

            if (string.Equals(container, "png", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveImagePNGFormat(width, height);
            }

            if (string.Equals(container, "gif", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.GIF_LRG;
            }

            if (string.Equals(container, "raw", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.RAW;
            }

            return null;
        }

        private static MediaFormatProfile[] ResolveVideoMPEG2TSFormat(string videoCodec, string audioCodec, int? width, int? height, TransportStreamTimestamp timestampType)
        {
            string suffix = timestampType switch
            {
                TransportStreamTimestamp.None => "_ISO",
                TransportStreamTimestamp.Valid => "_T",
                _ => string.Empty
            };

            string resolution = "S";
            if (width > 720 || height > 576)
            {
                resolution = "H";
            }

            if (string.Equals(videoCodec, "mpeg2video", StringComparison.OrdinalIgnoreCase))
            {
                var list = new List<MediaFormatProfile>
                {
                    ValueOf("MPEG_TS_SD_NA" + suffix),
                    ValueOf("MPEG_TS_SD_EU" + suffix),
                    ValueOf("MPEG_TS_SD_KO" + suffix)
                };

                if ((timestampType == TransportStreamTimestamp.Valid) && string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(MediaFormatProfile.MPEG_TS_JP_T);
                }

                return list.ToArray();
            }

            if (string.Equals(videoCodec, "h264", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(audioCodec, "lpcm", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { MediaFormatProfile.AVC_TS_HD_50_LPCM_T };
                }

                if (string.Equals(audioCodec, "dts", StringComparison.OrdinalIgnoreCase))
                {
                    return timestampType == TransportStreamTimestamp.None ? new[] { MediaFormatProfile.AVC_TS_HD_DTS_ISO } : new[] { MediaFormatProfile.AVC_TS_HD_DTS_T };
                }

                if (string.Equals(audioCodec, "mp2", StringComparison.OrdinalIgnoreCase))
                {
                    return timestampType == TransportStreamTimestamp.None
                        ? new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "AVC_TS_HP_{0}D_MPEG1_L2_ISO", resolution)) }
                        : new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "AVC_TS_HP_{0}D_MPEG1_L2_T", resolution)) };
                }

                if (string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "AVC_TS_MP_{0}D_AAC_MULT5{1}", resolution, suffix)) };
                }

                if (string.Equals(audioCodec, "mp3", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "AVC_TS_MP_{0}D_MPEG1_L3{1}", resolution, suffix)) };
                }

                if (string.IsNullOrEmpty(audioCodec) ||
                    string.Equals(audioCodec, "ac3", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "AVC_TS_MP_{0}D_AC3{1}", resolution, suffix)) };
                }
            }
            else if (string.Equals(videoCodec, "vc1", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "ac3", StringComparison.OrdinalIgnoreCase))
                {
                    if (width > 720 || height > 576)
                    {
                        return new[] { MediaFormatProfile.VC1_TS_AP_L2_AC3_ISO };
                    }

                    return new[] { MediaFormatProfile.VC1_TS_AP_L1_AC3_ISO };
                }

                if (string.Equals(audioCodec, "dts", StringComparison.OrdinalIgnoreCase))
                {
                    suffix = string.Equals(suffix, "_ISO", StringComparison.OrdinalIgnoreCase) ? suffix : "_T";

                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "VC1_TS_HD_DTS{0}", suffix)) };
                }
            }
            else if (string.Equals(videoCodec, "mpeg4", StringComparison.OrdinalIgnoreCase) || string.Equals(videoCodec, "msmpeg4", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "MPEG4_P2_TS_ASP_AAC{0}", suffix)) };
                }

                if (string.Equals(audioCodec, "mp3", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "MPEG4_P2_TS_ASP_MPEG1_L3{0}", suffix)) };
                }

                if (string.Equals(audioCodec, "mp2", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "MPEG4_P2_TS_ASP_MPEG2_L2{0}", suffix)) };
                }

                if (string.Equals(audioCodec, "ac3", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { ValueOf(string.Format(CultureInfo.InvariantCulture, "MPEG4_P2_TS_ASP_AC3{0}", suffix)) };
                }
            }

            return Array.Empty<MediaFormatProfile>();
        }

        private static MediaFormatProfile ValueOf(string value)
        {
            return (MediaFormatProfile)Enum.Parse(typeof(MediaFormatProfile), value, true);
        }

        private static MediaFormatProfile? ResolveVideoMP4Format(string videoCodec, string audioCodec, int? width, int? height)
        {
            if (string.Equals(videoCodec, "h264", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(audioCodec, "lpcm", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.AVC_MP4_LPCM;
                }

                if (string.IsNullOrEmpty(audioCodec) ||
                    string.Equals(audioCodec, "ac3", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.AVC_MP4_MP_SD_AC3;
                }

                if (string.Equals(audioCodec, "mp3", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.AVC_MP4_MP_SD_MPEG1_L3;
                }

                if (width.HasValue && height.HasValue)
                {
                    return width.Value switch
                    {
                        <= 720 when (height.Value <= 576) && string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase) => MediaFormatProfile.AVC_MP4_MP_SD_AAC_MULT5,
                        <= 1280 when (height.Value <= 720) && string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase) => MediaFormatProfile.AVC_MP4_MP_HD_720p_AAC,
                        <= 1920 when (height.Value <= 1080) && string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase) => MediaFormatProfile.AVC_MP4_MP_HD_1080i_AAC,
                        _ => null,
                    };
                }
            }
            else if (string.Equals(videoCodec, "mpeg4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(videoCodec, "msmpeg4", StringComparison.OrdinalIgnoreCase))
            {
                if (width.HasValue && height.HasValue && width.Value <= 720 && height.Value <= 576)
                {
                    if (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase))
                    {
                        return MediaFormatProfile.MPEG4_P2_MP4_ASP_AAC;
                    }

                    if (string.Equals(audioCodec, "ac3", StringComparison.OrdinalIgnoreCase) || string.Equals(audioCodec, "mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        return MediaFormatProfile.MPEG4_P2_MP4_NDSD;
                    }
                }
                else if (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.MPEG4_P2_MP4_SP_L6_AAC;
                }
            }
            else if (string.Equals(videoCodec, "h263", StringComparison.OrdinalIgnoreCase) && string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.MPEG4_H263_MP4_P0_L10_AAC;
            }

            return null;
        }

        private static MediaFormatProfile? ResolveVideo3GPFormat(string videoCodec, string audioCodec)
        {
            if (string.Equals(videoCodec, "h264", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.AVC_3GPP_BL_QCIF15_AAC;
                }
            }
            else if (string.Equals(videoCodec, "mpeg4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(videoCodec, "msmpeg4", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "wma", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.MPEG4_P2_3GPP_SP_L0B_AAC;
                }

                if (string.Equals(audioCodec, "amrnb", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.MPEG4_P2_3GPP_SP_L0B_AMR;
                }
            }
            else if (string.Equals(videoCodec, "h263", StringComparison.OrdinalIgnoreCase) && string.Equals(audioCodec, "amrnb", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.MPEG4_H263_3GPP_P0_L10_AMR;
            }

            return null;
        }

        private static MediaFormatProfile? ResolveVideoASFFormat(string videoCodec, string audioCodec, int? width, int? height)
        {
            if (string.Equals(videoCodec, "wmv", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "wma", StringComparison.OrdinalIgnoreCase) || string.Equals(videoCodec, "wmapro", StringComparison.OrdinalIgnoreCase)))
            {
                if (width.HasValue && height.HasValue)
                {
                    if ((width.Value <= 720) && (height.Value <= 576))
                    {
                        if (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "wma", StringComparison.OrdinalIgnoreCase))
                        {
                            return MediaFormatProfile.WMVMED_FULL;
                        }

                        return MediaFormatProfile.WMVMED_PRO;
                    }
                }

                if (string.IsNullOrEmpty(audioCodec) || string.Equals(audioCodec, "wma", StringComparison.OrdinalIgnoreCase))
                {
                    return MediaFormatProfile.WMVHIGH_FULL;
                }

                return MediaFormatProfile.WMVHIGH_PRO;
            }

            if (string.Equals(videoCodec, "vc1", StringComparison.OrdinalIgnoreCase))
            {
                if (width.HasValue && height.HasValue)
                {
                    return width.Value switch
                    {
                        <= 720 when height.Value <= 576 => MediaFormatProfile.VC1_ASF_AP_L1_WMA,
                        <= 1280 when height.Value <= 720 => MediaFormatProfile.VC1_ASF_AP_L2_WMA,
                        <= 1920 when height.Value <= 1080 => MediaFormatProfile.VC1_ASF_AP_L3_WMA,
                        _ => null,
                    };
                }

                return null;
            }

            if (string.Equals(videoCodec, "mpeg2video", StringComparison.OrdinalIgnoreCase))
            {
                return MediaFormatProfile.DVR_MS;
            }

            return null;
        }

        private static MediaFormatProfile ResolveAudioASFFormat(int? bitrate)
        {
            return bitrate <= 193 ? MediaFormatProfile.WMA_BASE : MediaFormatProfile.WMA_FULL;
        }

        private static MediaFormatProfile? ResolveAudioLPCMFormat(int? frequency, int? channels)
        {
            if (frequency.HasValue && channels.HasValue)
            {
                return frequency.Value switch
                {
                    44100 when channels.Value == 1 => MediaFormatProfile.LPCM16_44_MONO,
                    44100 when channels.Value == 2 => MediaFormatProfile.LPCM16_44_STEREO,
                    48000 when channels.Value == 1 => MediaFormatProfile.LPCM16_48_MONO,
                    48000 when channels.Value == 2 => MediaFormatProfile.LPCM16_48_STEREO,
                    _ => null
                };
            }

            return MediaFormatProfile.LPCM16_48_STEREO;
        }

        private static MediaFormatProfile ResolveAudioMP4Format(int? bitrate)
        {
            return bitrate <= 320 ? MediaFormatProfile.AAC_ISO_320 : MediaFormatProfile.AAC_ISO;
        }

        private static MediaFormatProfile ResolveAudioADTSFormat(int? bitrate)
        {
            return bitrate <= 320 ? MediaFormatProfile.AAC_ADTS_320 : MediaFormatProfile.AAC_ADTS;
        }

        private static MediaFormatProfile ResolveImageJPGFormat(int? width, int? height)
        {
            if (width.HasValue && height.HasValue)
            {
                return width switch
                {
                    <= 160 when height <= 160 => MediaFormatProfile.JPEG_TN,
                    <= 640 when height <= 480 => MediaFormatProfile.JPEG_SM,
                    <= 1024 when height <= 768 => MediaFormatProfile.JPEG_MED,
                    _ => MediaFormatProfile.JPEG_LRG
                };
            }

            return MediaFormatProfile.JPEG_SM;
        }

        private static MediaFormatProfile ResolveImagePNGFormat(int? width, int? height)
        {
            return ((width <= 160) && (height <= 160)) ? MediaFormatProfile.PNG_TN : MediaFormatProfile.PNG_LRG;
        }
    }
}
