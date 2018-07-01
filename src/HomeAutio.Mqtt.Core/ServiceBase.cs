using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;

namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Service base class for HomeAutio services.
    /// </summary>
    public abstract class ServiceBase : IHostedService, IDisposable
    {
        private readonly ILogger<ServiceBase> _serviceLog;

        private readonly string _brokerIp;
        private readonly int _brokerPort;
        private readonly string _brokerUsername;
        private readonly string _brokerPassword;

        private bool _disposed = false;
        private bool _stopping;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBase"/> class.
        /// </summary>
        /// <param name="applicationLifetime">Application lifetime instance.</param>
        /// <param name="logger">Logging instance.</param>
        /// <param name="brokerIp">MQTT broker IP.</param>
        /// <param name="brokerPort">MQTT broker port.</param>
        /// <param name="brokerUsername">MQTT broker username.</param>
        /// <param name="brokerPassword">MQTT broker password.</param>
        /// <param name="topicRoot">MQTT topic root.</param>
        public ServiceBase(
            IApplicationLifetime applicationLifetime,
            ILogger<ServiceBase> logger,
            string brokerIp,
            int brokerPort,
            string brokerUsername,
            string brokerPassword,
            string topicRoot)
        {
            ApplicationLifetime = applicationLifetime;
            _serviceLog = logger;
            _brokerIp = brokerIp;
            _brokerPort = brokerPort;
            _brokerUsername = brokerUsername;
            _brokerPassword = brokerPassword;
            TopicRoot = topicRoot;

            // Setup mqtt client
            SetupMqttLogging();
            MqttClient.ApplicationMessageReceived += Mqtt_MqttMsgPublishReceived;
        }

        /// <summary>
        /// Application lifetime for control and eventing.
        /// </summary>
        protected IApplicationLifetime ApplicationLifetime { get; private set; }

        /// <summary>
        /// MQTT client.
        /// </summary>
        protected IMqttClient MqttClient { get; private set; } = new MqttFactory().CreateMqttClient();

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
            MqttClient.Connected += (sender, e) => _serviceLog.LogInformation("MQTT Connection established");
            MqttClient.Disconnected += (sender, e) =>
            {
                if (!_stopping)
                {
                    _serviceLog.LogInformation("MQTT Connection closed unexpectedly");
                    ApplicationLifetime.StopApplication();
                }
                else
                {
                    _serviceLog.LogInformation("MQTT Connection closed");
                }
            };
        }

        #region Service implementation

        /// <summary>
        /// Service Start action. Do not call this directly.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _serviceLog.LogInformation("Service start initiated");
            _stopping = false;

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerIp, _brokerPort)
                .WithClientId(MqttClientId.ToString())
                .WithCleanSession();

            if (!string.IsNullOrEmpty(_brokerUsername) && !string.IsNullOrEmpty(_brokerPassword))
                optionsBuilder.WithCredentials(_brokerUsername, _brokerPassword);

            // Connect to MQTT
            await MqttClient.ConnectAsync(optionsBuilder.Build())
                .ConfigureAwait(false);

            // Subscribe to MQTT messages
            await SubscribeAsync(cancellationToken)
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
        public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
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
                    await Unsubscribe(cancellationToken)
                        .ConfigureAwait(false);
                    await MqttClient.DisconnectAsync()
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
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        protected abstract void Mqtt_MqttMsgPublishReceived(object sender, MqttApplicationMessageReceivedEventArgs e);

        /// <summary>
        /// Subscribes to the MQTT topics in SubscribedTopics.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task SubscribeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (MqttClient.IsConnected)
            {
                _serviceLog.LogDebug("MQTT subscribing to the following topics: " + string.Join(", ", SubscribedTopics));
                await MqttClient.SubscribeAsync(SubscribedTopics
                    .Select(topic => new TopicFilterBuilder()
                        .WithTopic(topic)
                        .WithAtLeastOnceQoS()
                        .Build()))
                    .ConfigureAwait(false);
            }
            else
            {
                _serviceLog.LogWarning("MQTT could not subscribe to topics on disconnected client");
            }
        }

        /// <summary>
        /// Unsubscribes from the MQTT topics in SubscribedTopics.
        /// </summary>
        /// <param name="cancellationToken">Cancelation token.</param>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task Unsubscribe(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Wipe subscriptions
            if (MqttClient.IsConnected)
            {
                _serviceLog.LogDebug("MQTT unsubscribing to the following topics: " + string.Join(", ", SubscribedTopics));
                await MqttClient.UnsubscribeAsync(SubscribedTopics)
                    .ConfigureAwait(false);
            }
            else
            {
                _serviceLog.LogWarning("MQTT could not unsubscribe from topics on disconnected client");
            }
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
