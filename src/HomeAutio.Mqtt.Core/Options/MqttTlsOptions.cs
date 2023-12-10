using System.Collections.Generic;

namespace HomeAutio.Mqtt.Core.Options
{
    /// <summary>
    /// Broker TLS settings.
    /// </summary>
    public class MqttTlsOptions
    {
        /// <summary>
        /// Allow untrusted certificates.
        /// </summary>
        public bool AllowUntrustedCertificates { get; set; }

        /// <summary>
        /// Ignore certificate chain errors.
        /// </summary>
        public bool IgnoreCertificateChainErrors { get; set; }

        /// <summary>
        /// Ignore certificate revocation errors.
        /// </summary>
        public bool IgnoreCertificateRevocationErrors { get; set; }

        /// <summary>
        /// SSL protocol.
        /// </summary>
        public string SslProtocol { get; set; } = "1.2";

        /// <summary>
        /// Certificates for CS / client certificate auth.
        /// </summary>
        public IList<MqttTlsCertificateInfo> Certificates { get; set; } = new List<MqttTlsCertificateInfo>();
    }
}
