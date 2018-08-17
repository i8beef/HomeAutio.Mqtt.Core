namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Connection status.
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// Disconnected.
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Connected only to MQTT.
        /// </summary>
        ConnectedMqtt,

        /// <summary>
        /// Connected to MQTT and device.
        /// </summary>
        ConnectedMqttAndDevice
    }
}
