using System.Collections.Generic;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="ServiceAction" />.
    /// </summary>
    public class ServiceAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceAction"/> class.
        /// </summary>
        /// <param name="name">Name of the action.</param>
        public ServiceAction(string name)
        {
            Name = name;
            ArgumentList = new();
        }

        /// <summary>
        /// Gets the name of the action.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the ArgumentList.
        /// </summary>
        public List<Argument> ArgumentList { get; }
    }
}
