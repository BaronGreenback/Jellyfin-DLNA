namespace Jellyfin.Plugin.Dlna.Model
{
    /// <summary>
    /// DLNA Query parameter type, used when querying DLNA devices via SOAP.
    /// </summary>
    public class Argument
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Argument"/> class.
        /// </summary>
        /// <param name="name">The name of the argument.</param>
        /// <param name="direction">The <see cref="ArgumentDirection"/>.</param>
        /// <param name="variableType">The <see cref="StateVariableType"/>.</param>
        public Argument(string name, ArgumentDirection direction, StateVariableType variableType)
        {
            Name = name;
            Direction = direction;
            RelatedStateVariable = variableType;
        }

        /// <summary>
        /// Gets the name of the DLNA argument.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the direction of the parameter.
        /// </summary>
        public ArgumentDirection Direction { get; }

        /// <summary>
        /// Gets the related DLNA state variable for this argument.
        /// </summary>
        public StateVariableType RelatedStateVariable { get; }
    }
}
