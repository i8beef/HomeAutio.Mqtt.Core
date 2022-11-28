namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Broker setings.
    /// </summary>
    public class BrokerSettings
    {
        /// <summary>
        /// Broker IP address.
        /// </summary>
        public required string BrokerIp { get; init; }

        /// <summary>
        /// Borker port, default 1883.
        /// </summary>
        public int BrokerPort { get; init; } = 1883;

        /// <summary>
        /// Broker username.
        /// </summary>
        public string? BrokerUsername { get; init; }

        /// <summary>
        /// Broker password.
        /// </summary>
        public string? BrokerPassword { get; init; }

        /// <summary>
        /// Broker reconnect delay in seconds, default 5.
        /// </summary>
        public int BrokerReconnectDelay { get; init; } = 5;

        /// <summary>
        /// Whether to use TLS for the connection or not. Defaults to false.
        /// </summary>
        public bool BrokerUseTls { get; init; }

        /// <summary>
        /// Broker TLS settings.
        /// </summary>
        public BrokerTlsSettings? BrokerTlsSettings { get; init; }
    }
}
