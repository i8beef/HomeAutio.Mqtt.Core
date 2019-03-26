﻿namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Broker setings.
    /// </summary>
    public class BrokerSettings
    {
        /// <summary>
        /// Broker IP address.
        /// </summary>
        public string BrokerIp { get; set; }

        /// <summary>
        /// Borker port, default 1883.
        /// </summary>
        public int BrokerPort { get; set; } = 1883;

        /// <summary>
        /// Broker username.
        /// </summary>
        public string BrokerUsername { get; set; }

        /// <summary>
        /// Broker password.
        /// </summary>
        public string BrokerPassword { get; set; }

        /// <summary>
        /// Broker reconnect delay in seconds, default 5.
        /// </summary>
        public int BrokerReconnectDelay { get; set; } = 5;

        /// <summary>
        /// Whether to use TLS for the connection or not. Defaults to false.
        /// </summary>
        public bool BrokerUseTls { get; set; } = false;
    }
}
