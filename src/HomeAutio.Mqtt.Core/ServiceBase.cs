using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Diagnostics;
using MQTTnet.Extensions.ManagedClient;

namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Service base class for HomeAutio services.
    /// </summary>
    public abstract class ServiceBase : IHostedService, IDisposable
    {
        private readonly ILogger<ServiceBase> _serviceLog;

        private readonly BrokerSettings _brokerSettings;

        private bool _disposed = false;
        private bool _stopping;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBase"/> class.
        /// </summary>
        /// <param name="logger">Logging instance.</param>
        /// <param name="brokerSettings">MQTT broker settings.</param>
        /// <param name="topicRoot">MQTT topic root.</param>
        public ServiceBase(
            ILogger<ServiceBase> logger,
            BrokerSettings brokerSettings,
            string topicRoot)
        {
            _serviceLog = logger;
            _brokerSettings = brokerSettings;
            TopicRoot = topicRoot;

            // Setup mqtt client
            SetupMqttLogging();
            MqttClient.UseApplicationMessageReceivedHandler(e => Mqtt_MqttMsgPublishReceived(e));
        }

        /// <summary>
        /// MQTT client.
        /// </summary>
        protected IManagedMqttClient MqttClient { get; private set; } = new MqttFactory().CreateManagedMqttClient();

        /// <summary>
        /// MQTT client id.
        /// </summary>
        protected Guid MqttClientId { get; private set; } = Guid.NewGuid();

        /// <summary>
        /// Holds list of active MQTT subscriptions.
        /// </summary>
        protected IList<string> SubscribedTopics { get; private set; } = new List<string>();

        /// <summary>
        /// MQTT Topic root.
        /// </summary>
        protected string TopicRoot { get; private set; }

        /// <summary>
        /// Setup logging for the MqttClient.
        /// </summary>
        private void SetupMqttLogging()
        {
            // Log trace messages
            MqttNetGlobalLogger.LogMessagePublished += (sender, e) =>
            {
                switch (e.TraceMessage.Level)
                {
                    case MqttNetLogLevel.Error:
                        _serviceLog.LogError(e.TraceMessage.Message, e.TraceMessage.Exception);
                        break;
                    case MqttNetLogLevel.Warning:
                        _serviceLog.LogWarning(e.TraceMessage.Message);
                        break;
                    case MqttNetLogLevel.Info:
                        _serviceLog.LogInformation(e.TraceMessage.Message);
                        break;
                    case MqttNetLogLevel.Verbose:
                    default:
                        _serviceLog.LogTrace(e.TraceMessage.Message);
                        break;
                }
            };

            // Log connect and disconnect events
            MqttClient.UseConnectedHandler(async e =>
            {
                // Publish connected announcement, due to structure of these apps, it is always assumed the device is connected
                await MqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"{TopicRoot}/connected")
                    .WithPayload(((int)ConnectionStatus.ConnectedMqttAndDevice).ToString())
                    .WithAtLeastOnceQoS()
                    .WithRetainFlag()
                    .Build()).ConfigureAwait(false);

                _serviceLog.LogInformation("MQTT Connection established");
            });

            MqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(e => _serviceLog.LogWarning("MQTT Connection failed, retrying..."));
            MqttClient.UseDisconnectedHandler(e =>
            {
                if (!_stopping)
                {
                    _serviceLog.LogInformation("MQTT Connection closed unexpectedly, reconnecting...");
                }
                else
                {
                    _serviceLog.LogInformation("MQTT Connection closed");
                }
            });
        }

        #region Service implementation

        /// <summary>
        /// Service Start action. Do not call this directly.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _serviceLog.LogInformation("Service start initiated");
            _stopping = false;

            // MQTT will message
            var willMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"{TopicRoot}/connected")
                .WithPayload(((int)ConnectionStatus.Disconnected).ToString())
                .WithAtLeastOnceQoS()
                .WithRetainFlag()
                .Build();

            // MQTT client options
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerSettings.BrokerIp, _brokerSettings.BrokerPort)
                .WithClientId(MqttClientId.ToString())
                .WithCleanSession()
                .WithWillMessage(willMessage);

            // MQTT TLS support
            if (_brokerSettings.BrokerUseTls)
            {
                if (_brokerSettings.BrokerTlsSettings == null)
                    throw new ArgumentNullException(nameof(_brokerSettings.BrokerTlsSettings));

                var tlsOptions = new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = _brokerSettings.BrokerUseTls,
                    AllowUntrustedCertificates = _brokerSettings.BrokerTlsSettings.AllowUntrustedCertificates,
                    IgnoreCertificateChainErrors = _brokerSettings.BrokerTlsSettings.IgnoreCertificateChainErrors,
                    IgnoreCertificateRevocationErrors = _brokerSettings.BrokerTlsSettings.IgnoreCertificateRevocationErrors,
                    SslProtocol = _brokerSettings.BrokerTlsSettings.SslProtocol,
                    Certificates = _brokerSettings.BrokerTlsSettings.Certificates?.Select(x => x.Export(System.Security.Cryptography.X509Certificates.X509ContentType.SerializedCert))
                };

                optionsBuilder.WithTls(tlsOptions);
            }

            // MQTT credentials
            if (!string.IsNullOrEmpty(_brokerSettings.BrokerUsername) && !string.IsNullOrEmpty(_brokerSettings.BrokerPassword))
                optionsBuilder.WithCredentials(_brokerSettings.BrokerUsername, _brokerSettings.BrokerPassword);

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(_brokerSettings.BrokerReconnectDelay))
                .WithClientOptions(optionsBuilder.Build())
                .Build();

            // Subscribe to MQTT messages
            await SubscribeAsync(cancellationToken)
                .ConfigureAwait(false);

            // Connect to MQTT
            await MqttClient.StartAsync(managedOptions)
                .ConfigureAwait(false);

            // Call startup on inheriting service class
            await StartServiceAsync(cancellationToken)
                .ConfigureAwait(false);

            _serviceLog.LogInformation("Service started successfully");
        }

        /// <summary>
        /// Service Stop action. Do not call this directly.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _serviceLog.LogInformation("Service stop initiated");

            _stopping = true;

            try
            {
                // Stop inheriting service class
                await StopServiceAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Graceful MQTT disconnect
                if (MqttClient.IsConnected)
                {
                    // Publish disconnected announcement
                    await MqttClient.PublishAsync(
                        new MqttApplicationMessageBuilder()
                            .WithTopic($"{TopicRoot}/connected")
                            .WithPayload(((int)ConnectionStatus.Disconnected).ToString())
                            .WithAtLeastOnceQoS()
                            .WithRetainFlag()
                            .Build(),
                        cancellationToken).ConfigureAwait(false);

                    await UnsubscribeAsync(cancellationToken)
                        .ConfigureAwait(false);
                    await MqttClient.StopAsync()
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Log error, but do not rethrow, Dispose should not throw exceptions.
                _serviceLog.LogError(ex, ex.Message);
            }

            _serviceLog.LogInformation("Service stopped successfully");
        }

        /// <summary>
        /// HomeAutio service start.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected abstract Task StartServiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// HomeAutio service stop.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected abstract Task StopServiceAsync(CancellationToken cancellationToken);

        #endregion

        #region MQTT Implementation

        /// <summary>
        /// Handles subscribed commands published to MQTT.
        /// </summary>
        /// <param name="e">Event args.</param>
        protected abstract void Mqtt_MqttMsgPublishReceived(MqttApplicationMessageReceivedEventArgs e);

        /// <summary>
        /// Subscribes to the MQTT topics in SubscribedTopics.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task SubscribeAsync(CancellationToken cancellationToken = default)
        {
            _serviceLog.LogInformation("MQTT subscribing to the following topics: " + string.Join(", ", SubscribedTopics));
            await MqttClient.SubscribeAsync(SubscribedTopics
                .Select(topic => new TopicFilterBuilder()
                    .WithTopic(topic)
                    .WithAtLeastOnceQoS()
                    .Build()))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Unsubscribes from the MQTT topics in SubscribedTopics.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
        {
            _serviceLog.LogInformation("MQTT unsubscribing to the following topics: " + string.Join(", ", SubscribedTopics));
            await MqttClient.UnsubscribeAsync(SubscribedTopics)
                .ConfigureAwait(false);
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Disposable implementation.
        /// </summary>
        /// <param name="disposing">Indicates if called as part of Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed state (managed objects).
                if (MqttClient != null)
                    MqttClient.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Disposable implementation.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
