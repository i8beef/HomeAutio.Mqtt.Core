namespace HomeAutio.Mqtt.Core.Entities
{
    /// <summary>
    /// Base class for controls.
    /// </summary>
    public abstract class Control
    {
        /// <summary>
        /// Control ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Control name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Control MQTT topic.
        /// </summary>
        public string CommandTopic { get; set; }
    }
}
