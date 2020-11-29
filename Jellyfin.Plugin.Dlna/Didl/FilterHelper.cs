using System;

namespace Jellyfin.Plugin.Dlna.Didl
{
    /// <summary>
    /// Defines the <see cref="FilterHelper" />.
    /// </summary>
    public static class FilterHelper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Filter"/> class.
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <returns>A tuple filter.</returns>
        public static (bool All, string[] Fields) Filter(string filter = "*")
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            return (string.Equals(filter, "*", StringComparison.Ordinal),
                filter.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Return true if the this object contains <paramref name="field"/>.
        /// </summary>
        /// <param name="filter">The filter tuple.</param>
        /// <param name="field">The field to compare.</param>
        /// <returns><c>True</c> if the <paramref name="field"/> is contained in this object.</returns>
        public static bool Contains(this (bool All, string[] Fields) filter, string field)
        {
            return filter.All || Array.Exists(filter.Fields, x => x.Equals(field, StringComparison.OrdinalIgnoreCase));
        }
    }
}
