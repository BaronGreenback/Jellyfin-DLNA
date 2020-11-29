using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.DeviceProfiles;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Dlna
{
    /// <summary>
    /// Defines the <see cref="ProfileHelper"/> class.
    /// </summary>
    public static class ProfileHelper
    {
        private static readonly Assembly _assembly = typeof(ProfileHelper).Assembly;

        /// <summary>
        /// Initialises the system profiles by loading them from the resource and into the profile manager.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> instance.</param>
        /// <param name="profileManager">The <see cref="IDeviceProfileManager"/> instance.</param>
        /// <param name="xmlSerializer">The <see cref="IXmlSerializer"/> instance.</param>
        /// <returns>A <see cref="Task"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Passed from DI.")]
        public static async Task ExtractSystemTemplates(ILogger logger, IDeviceProfileManager profileManager, IXmlSerializer xmlSerializer)
        {
            if (profileManager.Profiles.Count(p => p.ProfileType == DeviceProfileType.SystemTemplate) > 1)
            {
                // Don't load more than once.
                return;
            }

            // Load Resources into memory.
            const string NamespaceName = "Jellyfin.Plugin.Dlna.Profiles.Xml.";
            foreach (var name in _assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(NamespaceName, StringComparison.Ordinal))
                {
                    continue;
                }

                await using var stream = _assembly.GetManifestResourceStream(name);
                if (stream == null)
                {
                    logger.LogError("Unable to extract manifest resource for {Name}", name);
                    break;
                }

                var systemProfile = (DeviceProfile)xmlSerializer.DeserializeFromStream(typeof(DeviceProfile), stream);
                if (systemProfile != null)
                {
                    systemProfile.ProfileType = DeviceProfileType.SystemTemplate;
                    profileManager.AddProfile(systemProfile);
                }
            }
        }
    }
}
