using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// Defines the <see cref="StateVariable" />.
    /// </summary>
    public class StateVariable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StateVariable"/> class.
        /// </summary>
        /// <param name="name">The <see cref="StateVariableType"/>.</param>
        /// <param name="dataType">The <see cref="DataType"/>.</param>
        /// <param name="sendEvents">True if the variable sends events.</param>
        public StateVariable(StateVariableType name, DataType dataType, bool sendEvents)
        {
            Name = name;
            DataType = dataType;
            SendsEvents = sendEvents;
            AllowedValues = Array.Empty<string>();
            AllowedValueRange = null;
        }

        /// <summary>
        /// Gets the name of the state variable.
        /// </summary>
        public StateVariableType Name { get; }

        /// <summary>
        /// Gets the data type of the state variable.
        /// </summary>
        public DataType DataType { get; }

        /// <summary>
        /// Gets a value indicating whether it sends events.
        /// </summary>
        public bool SendsEvents { get; }

        /// <summary>
        /// Gets the allowed values.
        /// </summary>
        public IReadOnlyList<string>? AllowedValues { get; init; }

        /// <summary>
        /// Gets the allowed values range.
        /// </summary>
        public IDictionary<string, string>? AllowedValueRange { get; init; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
