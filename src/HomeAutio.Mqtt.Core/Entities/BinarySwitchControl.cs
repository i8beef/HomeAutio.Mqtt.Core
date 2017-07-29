namespace HomeAutio.Mqtt.Core.Entities
{
    /// <summary>
    /// Binary switch control.
    /// </summary>
    public class BinarySwitchControl : StatefulControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinarySwitchControl"/> class.
        /// </summary>
        public BinarySwitchControl()
            : this("ON", "OFF")
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinarySwitchControl"/> class.
        /// </summary>
        /// <param name="onStateLabel">On state label.</param>
        /// <param name="offStateLabel">Off state label.</param>
        public BinarySwitchControl(string onStateLabel, string offStateLabel)
        {
            OnStateLabel = onStateLabel;
            OffStateLabel = offStateLabel;
        }

        /// <summary>
        /// Label to display for "on" state.
        /// </summary>
        public string OnStateLabel { get; set; }

        /// <summary>
        /// Label to display for "off" state.
        /// </summary>
        public string OffStateLabel { get; set; }

        /// <summary>
        /// Argument to send for an "on" state.
        /// </summary>
        public string OnStateArgument { get; set; }

        /// <summary>
        /// Argument to send for an "off" state.
        /// </summary>
        public string OffStateArgument { get; set; }
    }
}
