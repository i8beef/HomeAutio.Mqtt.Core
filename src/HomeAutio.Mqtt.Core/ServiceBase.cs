﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;

namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Service base class for HomeAutio services.
    /// </summary>
    public abstract class ServiceBase : IDisposable
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
        /// <param name="logger">Logging instance.</param>
        /// <param name="brokerIp">MQTT broker IP.</param>
        /// <param name="brokerPort">MQTT broker port.</param>
        /// <param name="brokerUsername">MQTT broker username.</param>
        /// <param name="brokerPassword">MQTT broker password.</param>
        /// <param name="topicRoot">MQTT topic root.</param>
        public ServiceBase(ILogger<ServiceBase> logger, string brokerIp, int brokerPort, string brokerUsername, string brokerPassword, string topicRoot)
        {
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
                    throw new Exception("MQTT Connection closed unexpectedly");
                else
                    _serviceLog.LogInformation("MQTT Connection closed");
            };
        }

        #region Service implementation

        /// <summary>
        /// Service Start action. Do not call this directly.
        /// </summary>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        public async Task StartAsync()
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
            await MqttClient.ConnectAsync(optionsBuilder.Build()).ConfigureAwait(false);

            // Subscribe to MQTT messages
            await SubscribeAsync().ConfigureAwait(false);

            // Call startup on inheriting service class
            await StartServiceAsync().ConfigureAwait(false);

            _serviceLog.LogInformation("Service started successfully");
        }

        /// <summary>
        /// Service Stop action. Do not call this directly.
        /// </summary>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        public async Task StopAsync()
        {
            _serviceLog.LogInformation("Service stop initiated");

            _stopping = true;

            try
            {
                // Stop inheriting service class
                await StopServiceAsync().ConfigureAwait(false);

                // Graceful MQTT disconnect
                await Unsubscribe().ConfigureAwait(false);
                await MqttClient.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log error, but do not rethrow, Dispose should not throw exceptions.
                _serviceLog.LogError(ex, ex.Message);
            }

            Dispose();

            _serviceLog.LogInformation("Service stopped successfully");
        }

        /// <summary>
        /// HomeAutio service start.
        /// </summary>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected abstract Task StartServiceAsync();

        /// <summary>
        /// HomeAutio service stop.
        /// </summary>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected abstract Task StopServiceAsync();

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
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task SubscribeAsync()
        {
            if (MqttClient.IsConnected)
            {
                _serviceLog.LogDebug("MQTT subscribing to the following topics: " + string.Join(", ", SubscribedTopics));
                await MqttClient.SubscribeAsync(SubscribedTopics
                    .Select(topic => new TopicFilterBuilder()
                        .WithTopic(topic)
                        .WithAtLeastOnceQoS()
                        .Build())).ConfigureAwait(false);
            }
            else
            {
                _serviceLog.LogWarning("MQTT could not subscribe to topics on disconnected client");
            }
        }

        /// <summary>
        /// Unsubscribes from the MQTT topics in SubscribedTopics.
        /// </summary>
        /// <returns>Awaitable <see cref="Task" />.</returns>
        protected virtual async Task Unsubscribe()
        {
            // Wipe subscriptions
            if (MqttClient.IsConnected)
            {
                _serviceLog.LogDebug("MQTT unsubscribing to the following topics: " + string.Join(", ", SubscribedTopics));
                await MqttClient.UnsubscribeAsync(SubscribedTopics).ConfigureAwait(false);
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
                    ((IDisposable)MqttClient).Dispose();
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
