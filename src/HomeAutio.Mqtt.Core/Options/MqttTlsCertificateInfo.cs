namespace HomeAutio.Mqtt.Core.Options
{
    /// <summary>
    /// MQTT TLS certificate info.
    /// </summary>
    public class MqttTlsCertificateInfo
    {
        /// <summary>
        /// Certificate file path.
        /// </summary>
        public string File { get; set; } = string.Empty;

        /// <summary>
        /// Certificate pass phrase.
        /// </summary>
        public string PassPhrase { get; set; } = string.Empty;
    }
}
