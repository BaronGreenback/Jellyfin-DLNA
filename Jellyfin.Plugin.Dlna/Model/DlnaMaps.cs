using System.Globalization;
using MediaBrowser.Model.Dlna;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="DlnaMaps"/>.
    /// </summary>
    internal static class DlnaMaps
    {
        /// <summary>
        /// Converts a <see cref="DlnaFlags"/> to a string.
        /// </summary>
        /// <param name="flags">The <see cref="DlnaFlags"/>.</param>
        /// <returns>A string representation.</returns>
        public static string FlagsToString(DlnaFlags flags)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:X8}{1:D24}", (ulong)flags, 0);
        }

        /// <summary>
        /// Returns the Organisational Op Value.
        /// </summary>
        /// <param name="hasKnownRuntime">True if there is a known runtime.</param>
        /// <param name="isDirectStream">True if it is a direct stream.</param>
        /// <param name="profileTranscodeSeekInfo">A <see cref="TranscodeSeekInfo"/>.</param>
        /// <returns>The Organisational Op Value.</returns>
        public static string GetOrgOpValue(bool hasKnownRuntime, bool isDirectStream, TranscodeSeekInfo profileTranscodeSeekInfo)
        {
            if (!hasKnownRuntime)
            {
                return "00";
            }

            string orgOp = string.Empty;

            // Time-based seeking currently only possible when transcoding
            orgOp += isDirectStream ? "0" : "1";

            // Byte-based seeking only possible when not transcoding
            orgOp += isDirectStream || profileTranscodeSeekInfo == TranscodeSeekInfo.Bytes ? "1" : "0";

            return orgOp;

            // No seeking is available if we don't know the content runtime
        }

        /// <summary>
        /// Returns the image org op value.
        /// </summary>
        /// <returns>String containing the value.</returns>
        public static string GetImageOrgOpValue()
        {
            // Time-based seeking currently only possible when transcoding
            string orgOp = "0";

            // Byte-based seeking only possible when not transcoding
            orgOp += "0";

            return orgOp;
        }
    }
}
