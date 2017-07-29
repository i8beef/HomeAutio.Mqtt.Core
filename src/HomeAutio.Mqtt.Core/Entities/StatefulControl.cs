namespace HomeAutio.Mqtt.Core.Entities
{
    /// <summary>
    /// Base class for controls that expose state.
    /// </summary>
    public abstract class StatefulControl : Control
    {
        /// <summary>
        /// MQTT topic that state updates are published on.
        /// </summary>
        public string ValueTopic { get; set; }
    }
}
