using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using HomeAutio.Mqtt.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
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

        private readonly MqttOptions _mqttOptions;

        private bool _disposed;
        private bool _stopping;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBase"/> class.
        /// </summary>
        /// <param name="logger">Logging instance.</param>
        /// <param name="mqttOptions">MQTT broker settings.</param>
        /// <param name="topicRoot">MQTT topic root.</param>
        public ServiceBase(
            ILogger<ServiceBase> logger,
            IOptions<MqttOptions> mqttOptions,
            string topicRoot)
        {
            _serviceLog = logger;
            _mqttOptions = mqttOptions.Value;
            TopicRoot = topicRoot;

            // Log trace messages
            var mqttEventLogger = new MqttNetEventLogger("HomeAutioLogger");
            mqttEventLogger.LogMessagePublished += (sender, e) =>
            {
                switch (e.LogMessage.Level)
                {
                    case MqttNetLogLevel.Error:
                        _serviceLog.LogError(e.LogMessage.Message, e.LogMessage.Exception);
                        break;
                    case MqttNetLogLevel.Warning:
                        _serviceLog.LogWarning(e.LogMessage.Message);
                        break;
                    case MqttNetLogLevel.Info:
                        _serviceLog.LogInformation(e.LogMessage.Message);
                        break;
                    case MqttNetLogLevel.Verbose:
                    default:
                        _serviceLog.LogTrace(e.LogMessage.Message);
                        break;
                }
            };

            var mqttFactory = new MqttFactory(mqttEventLogger);
            MqttClient = mqttFactory.CreateManagedMqttClient();

            // Log connect and disconnect events
            MqttClient.ConnectedAsync += async (e) =>
            {
                // Publish connected announcement, due to structure of these apps, it is always assumed the device is connected
                await MqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"{TopicRoot}/connected")
                    .WithPayload(((int)ConnectionStatus.ConnectedMqttAndDevice).ToString())
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag()
                    .Build());

                _serviceLog.LogInformation("MQTT Connection established");
            };

            MqttClient.ConnectingFailedAsync += (e) =>
            {
                _serviceLog.LogWarning("MQTT Connection failed, retrying...");

                return Task.CompletedTask;
            };

            MqttClient.DisconnectedAsync += (e) =>
            {
                if (!_stopping)
                {
                    _serviceLog.LogInformation("MQTT Connection closed unexpectedly, reconnecting...");
                }
                else
                {
                    _serviceLog.LogInformation("MQTT Connection closed");
                }

                return Task.CompletedTask;
            };

            MqttClient.ApplicationMessageReceivedAsync += MqttMsgPublishReceived;
        }

        /// <summary>
        /// MQTT client.
        /// </summary>
        protected IManagedMqttClient MqttClient { get; init; }

        /// <summary>
        /// MQTT client id.
        /// </summary>
        protected Guid MqttClientId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Holds list of active MQTT subscriptions.
        /// </summary>
        protected IList<string> SubscribedTopics { get; init; } = new List<string>();

        /// <summary>
        /// MQTT Topic root.
        /// </summary>
        protected string TopicRoot { get; init; }

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

            // MQTT client options
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttOptions.BrokerIp, _mqttOptions.BrokerPort)
                .WithClientId(MqttClientId.ToString())
                .WithCleanSession()
                .WithWillTopic($"{TopicRoot}/connected")
                .WithWillPayload(((int)ConnectionStatus.Disconnected).ToString())
                .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithWillRetain();

            // MQTT TLS support
            if (_mqttOptions.BrokerUseTls)
            {
                if (_mqttOptions.BrokerTlsSettings == null)
                {
                    throw new ArgumentException($"BrokerSettings {nameof(_mqttOptions.BrokerTlsSettings)} cannot be null when {nameof(_mqttOptions.BrokerUseTls)} is true");
                }

                // Temporary fix for https://github.com/dotnet/MQTTnet/issues/1547
                var certificateValidationHandler = MqttClientDefaultCertificateValidationHandler.Handle;

                // SSL protocol
                var sslProtocol = _mqttOptions.BrokerTlsSettings.SslProtocol switch
                {
                    "1.2" => System.Security.Authentication.SslProtocols.Tls12,
                    "1.3" => System.Security.Authentication.SslProtocols.Tls13,
                    _ => throw new NotSupportedException($"Only TLS 1.2 and 1.3 are supported")
                };

                // Certificates
                var brokerTlsCertificates = _mqttOptions.BrokerTlsSettings.Certificates
                    .Select(x =>
                    {
                        if (!File.Exists(x.File))
                        {
                            throw new FileNotFoundException($"Broker Certificate '{x.File}' is missing!");
                        }

                        return !string.IsNullOrEmpty(x.PassPhrase) ?
                            new X509Certificate2(x.File, x.PassPhrase) :
                            new X509Certificate2(x.File);
                    }).ToList();

                var tlsOptions = new MqttClientTlsOptions
                {
                    UseTls = _mqttOptions.BrokerUseTls,
                    AllowUntrustedCertificates = _mqttOptions.BrokerTlsSettings.AllowUntrustedCertificates,
                    IgnoreCertificateChainErrors = _mqttOptions.BrokerTlsSettings.IgnoreCertificateChainErrors,
                    IgnoreCertificateRevocationErrors = _mqttOptions.BrokerTlsSettings.IgnoreCertificateRevocationErrors,
                    SslProtocol = sslProtocol,
                    ClientCertificatesProvider = new DefaultMqttCertificatesProvider(brokerTlsCertificates),
                    CertificateValidationHandler = certificateValidationHandler
                };

                optionsBuilder.WithTlsOptions(tlsOptions);
            }

            // MQTT credentials
            if (!string.IsNullOrEmpty(_mqttOptions.BrokerUsername) && !string.IsNullOrEmpty(_mqttOptions.BrokerPassword))
            {
                optionsBuilder.WithCredentials(_mqttOptions.BrokerUsername, _mqttOptions.BrokerPassword);
            }

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(_mqttOptions.BrokerReconnectDelay))
                .WithClientOptions(optionsBuilder.Build())
                .Build();

            // Subscribe to MQTT messages
            await SubscribeAsync(cancellationToken);

            // Connect to MQTT
            await MqttClient.StartAsync(managedOptions);

            // Call startup on inheriting service class
            await StartServiceAsync(cancellationToken);

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
                await StopServiceAsync(cancellationToken);

                // Graceful MQTT disconnect
                if (MqttClient.IsConnected)
                {
                    // Publish disconnected announcement
                    await MqttClient.EnqueueAsync(
                        new MqttApplicationMessageBuilder()
                            .WithTopic($"{TopicRoot}/connected")
                            .WithPayload(((int)ConnectionStatus.Disconnected).ToString())
                            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                            .WithRetainFlag()
                            .Build());

                    await UnsubscribeAsync(cancellationToken);

                    await MqttClient.StopAsync();
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
        protected abstract Task MqttMsgPublishReceived(MqttApplicationMessageReceivedEventArgs e);

        /// <summary>
        /// Subscribes to the MQTT topics in SubscribedTopics.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task SubscribeAsync(CancellationToken cancellationToken = default)
        {
            _serviceLog.LogInformation("MQTT subscribing to the following topics: " + string.Join(", ", SubscribedTopics));
            await MqttClient.SubscribeAsync(SubscribedTopics
                .Select(topic => new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithAtLeastOnceQoS()
                    .Build()).ToList());
        }

        /// <summary>
        /// Unsubscribes from the MQTT topics in SubscribedTopics.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
        {
            _serviceLog.LogInformation("MQTT unsubscribing to the following topics: " + string.Join(", ", SubscribedTopics));
            await MqttClient.UnsubscribeAsync(SubscribedTopics);
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
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed state (managed objects).
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
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
