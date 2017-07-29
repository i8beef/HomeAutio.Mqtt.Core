using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace HomeAutio.Mqtt.Core
{
    /// <summary>
    /// Service base class for HomeAutio services.
    /// </summary>
    public abstract class ServiceBase : IDisposable
    {
        private ILogger _serviceLog = LogManager.GetCurrentClassLogger();
        private bool _disposed = false;
        private bool _stopping;

        private string _brokerUsername;
        private string _brokerPassword;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBase"/> class.
        /// </summary>
        /// <param name="brokerIp">MQTT broker IP.</param>
        /// <param name="brokerPort">MQTT broker port.</param>
        /// <param name="brokerUsername">MQTT broker username.</param>
        /// <param name="brokerPassword">MQTT broker password.</param>
        /// <param name="topicRoot">MQTT topic root.</param>
        public ServiceBase(string brokerIp, int brokerPort, string brokerUsername, string brokerPassword, string topicRoot)
        {
            _brokerUsername = brokerUsername;
            _brokerPassword = brokerPassword;

            // Setup MQTT client
            if (brokerPort != 1883)
                MqttClient = new MqttClient(brokerIp, brokerPort, false, MqttSslProtocols.None, null, null);
            else
                MqttClient = new MqttClient(brokerIp);

            MqttClientId = Guid.NewGuid();
            TopicRoot = topicRoot;

            // Setup mqtt client
            MqttClient.MqttMsgPublishReceived += Mqtt_MqttMsgPublishReceived;

            // MQTT client logging
            MqttClient.MqttMsgPublished += (object sender, MqttMsgPublishedEventArgs e) => { _serviceLog.Debug("MQTT message id " + e.MessageId + " sent successfully"); };
            MqttClient.MqttMsgSubscribed += (object sender, MqttMsgSubscribedEventArgs e) => { _serviceLog.Debug("MQTT subscribe successful with message id " + e.MessageId); };
            MqttClient.MqttMsgUnsubscribed += (object sender, MqttMsgUnsubscribedEventArgs e) => { _serviceLog.Debug("MQTT unsubscribe successful with message id " + e.MessageId); };
            MqttClient.ConnectionClosed += (object sender, EventArgs e) =>
            {
                if (!_stopping)
                {
                    // Unexpected disconnect, restart service
                    throw new Exception("MQTT Connection closed unexpectedly");
                }
                else
                {
                    _serviceLog.Info("MQTT Connection closed");
                }
            };
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ServiceBase"/> class.
        /// Override a finalizer only if Dispose(bool disposing) has code to free unmanaged resources.
        /// </summary>
        ~ServiceBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// MQTT client.
        /// </summary>
        protected MqttClient MqttClient { get; private set; }

        /// <summary>
        /// MQTT client id.
        /// </summary>
        protected Guid MqttClientId { get; private set; }

        /// <summary>
        /// Holds list of active MQTT subscriptions.
        /// </summary>
        protected IList<string> SubscribedTopics { get; set; }

        /// <summary>
        /// MQTT Topic root.
        /// </summary>
        protected string TopicRoot { get; private set; }

        #region Service implementation

        /// <summary>
        /// Service Start action. Do not call this directly.
        /// </summary>
        public void Start()
        {
            _serviceLog.Info("Service start initiated");
            _stopping = false;

            // Connect to MQTT
            if (!string.IsNullOrEmpty(_brokerUsername) && !string.IsNullOrEmpty(_brokerPassword))
                MqttClient.Connect(MqttClientId.ToString(), _brokerUsername, _brokerPassword);
            else
                MqttClient.Connect(MqttClientId.ToString());

            // Subscribe to MQTT messages
            Subscribe();

            StartService();

            _serviceLog.Info("Service started successfully");
        }

        /// <summary>
        /// Service Stop action. Do not call this directly.
        /// </summary>
        public void Stop()
        {
            _serviceLog.Info("Service stop initiated");

            _stopping = true;
            StopService();

            Dispose();

            _serviceLog.Info("Service stopped successfully");
        }

        /// <summary>
        /// HomeAutio service start.
        /// </summary>
        protected abstract void StartService();

        /// <summary>
        /// HomeAutio service stop.
        /// </summary>
        protected abstract void StopService();

        #endregion

        #region MQTT Implementation

        /// <summary>
        /// Handles subscribed commands published to MQTT.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        protected abstract void Mqtt_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e);

        /// <summary>
        /// Subscribes to the MQTT topics in SubscribedTopics.
        /// </summary>
        protected virtual void Subscribe()
        {
            if (MqttClient.IsConnected)
            {
                _serviceLog.Debug("MQTT subscribing to the following topics: " + string.Join(", ", SubscribedTopics));

                var qosLevels = SubscribedTopics.Select(x => MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE).ToArray();
                MqttClient.Subscribe(SubscribedTopics.ToArray(), qosLevels);
            }
            else
            {
                _serviceLog.Warn("MQTT could not subscribe to topics on disconnected client");
            }
        }

        /// <summary>
        /// Unsubscribes from the MQTT topics in SubscribedTopics.
        /// </summary>
        protected virtual void Unsubscribe()
        {
            // Wipe subscriptions
            if (MqttClient.IsConnected)
            {
                _serviceLog.Debug("MQTT unsubscribing to the following topics: " + string.Join(", ", SubscribedTopics));
                MqttClient.Unsubscribe(SubscribedTopics.ToArray());
            }
            else
            {
                _serviceLog.Warn("MQTT could not unsubscribe from topics on disconnected client");
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
            }

            // Free unmanaged resources (unmanaged objects) and override a finalizer below.
            if (MqttClient != null && MqttClient.IsConnected)
            {
                try
                {
                    Unsubscribe();
                    MqttClient.Disconnect();
                }
                catch (Exception ex)
                {
                    // Log error, but do not rethrow, Dispose should not throw exceptions.
                    _serviceLog.Error(ex);
                }
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
