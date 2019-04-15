using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Broker TLS settings.
    /// </summary>
    public class BrokerTlsSettings
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
        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls12;

        /// <summary>
        /// Certificates for CS / client certificate auth.
        /// </summary>
        public IList<X509Certificate2> Certificates { get; set; }
    }
}
