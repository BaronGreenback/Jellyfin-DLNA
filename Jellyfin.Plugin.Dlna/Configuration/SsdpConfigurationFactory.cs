using System.Collections.Generic;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Dlna.Configuration
{
    /// <summary>
    /// Defines the <see cref="SsdpConfigurationFactory" />.
    /// </summary>
    public class SsdpConfigurationFactory : IConfigurationFactory
    {
        /// <summary>
        /// Get the configuration store.
        /// </summary>
        /// <returns>The <see cref="IEnumerable{ConfigurationStore}"/>.</returns>
        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new[]
            {
                new ConfigurationStore
                {
                    Key = "ssdp",
                    ConfigurationType = typeof(SsdpConfiguration)
                }
            };
        }
    }
}
