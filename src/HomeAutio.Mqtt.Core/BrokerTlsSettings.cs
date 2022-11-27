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
        public bool AllowUntrustedCertificates { get; init; }

        /// <summary>
        /// Ignore certificate chain errors.
        /// </summary>
        public bool IgnoreCertificateChainErrors { get; init; }

        /// <summary>
        /// Ignore certificate revocation errors.
        /// </summary>
        public bool IgnoreCertificateRevocationErrors { get; init; }

        /// <summary>
        /// SSL protocol.
        /// </summary>
        public SslProtocols SslProtocol { get; init; } = SslProtocols.Tls12;

        /// <summary>
        /// Certificates for CS / client certificate auth.
        /// </summary>
        public IList<X509Certificate2> Certificates { get; init; } = new List<X509Certificate2>();
    }
}
