using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace HomeAutio.Mqtt.Core
{
    public abstract class ServiceBase
    {
        private ILogger _serviceLog = LogManager.GetCurrentClassLogger();
        private bool _stopping;

        protected MqttClient _mqttClient;
        protected string _brokerUsername;
        protected string _brokerPassword;

        protected Guid _mqttClientId;
        protected string _topicRoot;

        /// <summary>
        /// Holds list of active MQTT subscriptions.
        /// </summary>
        protected IList<string> _subscribedTopics;

        public ServiceBase(string brokerIp, int brokerPort, string brokerUsername, string brokerPassword, string topicRoot)
        {
            _brokerUsername = brokerUsername;
            _brokerPassword = brokerPassword;

            // Setup MQTT client
            if (brokerPort != 1883)
                _mqttClient = new MqttClient(brokerIp, brokerPort, false, MqttSslProtocols.None, null, null);
            else
                _mqttClient = new MqttClient(brokerIp);

            _mqttClientId = Guid.NewGuid();
            _topicRoot = topicRoot;

            // Setup mqtt client
            _mqttClient.MqttMsgPublishReceived += Mqtt_MqttMsgPublishReceived;

            // MQTT client logging
            _mqttClient.MqttMsgPublished += (object sender, MqttMsgPublishedEventArgs e) => { _serviceLog.Debug("MQTT message id " + e.MessageId + " sent successfully"); };
            _mqttClient.MqttMsgSubscribed += (object sender, MqttMsgSubscribedEventArgs e) => { _serviceLog.Debug("MQTT subscribe successful with message id " + e.MessageId); };
            _mqttClient.MqttMsgUnsubscribed += (object sender, MqttMsgUnsubscribedEventArgs e) => { _serviceLog.Debug("MQTT unsubscribe successful with message id " + e.MessageId); };
            _mqttClient.ConnectionClosed += (object sender, EventArgs e) => {
                if (!_stopping)
                {
                    // Unexpected disconnect, restart service
                    _serviceLog.Error("MQTT Connection closed unexpectedly");
                    Stop();
                }
                else
                {
                    _serviceLog.Debug("MQTT Connection closed");
                }
            };
        }

        #region Service implementation

        /// <summary>
        /// HomeAutio service start.
        /// </summary>
        public abstract void StartService();

        /// <summary>
        /// HomeAutio service stop.
        /// </summary>
        public abstract void StopService();

        /// <summary>
        /// Service Start action. Do not call this directly.
        /// </summary>
        public void Start()
        {
            try
            {
                _serviceLog.Debug("Service start initiated");
                _stopping = false;

                // Connect to MQTT
                if (!string.IsNullOrEmpty(_brokerUsername) && !string.IsNullOrEmpty(_brokerPassword))
                    _mqttClient.Connect(_mqttClientId.ToString(), _brokerUsername, _brokerPassword);
                else
                    _mqttClient.Connect(_mqttClientId.ToString());

                _serviceLog.Debug("Service start initiated");

                // Subscribe to MQTT messages
                Subscribe();

                StartService();

                _serviceLog.Debug("Service started successfully");
            }
            catch (Exception ex)
            {
                _serviceLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Service Stop action. Do not call this directly.
        /// </summary>
        public void Stop()
        {
            try
            {
                _serviceLog.Debug("Service stop initiated");

                _stopping = true;
                Unsubscribe();

                StopService();

                if (_mqttClient.IsConnected)
                    _mqttClient.Disconnect();

                _serviceLog.Debug("Service stopped successfully");
            }
            catch (Exception ex)
            {
                _serviceLog.Error(ex);
                throw;
            }
        }

        #endregion

        #region MQTT Implementation

        /// <summary>
        /// Handles subscribed commands published to MQTT.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected abstract void Mqtt_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e);

        /// <summary>
        /// Subscribes to the MQTT topics in SubscribedTopics.
        /// </summary>
        protected virtual void Subscribe()
        {
            _serviceLog.Debug("MQTT subscribing to the following topics: " + string.Join(", ", _subscribedTopics));

            var qosLevels = _subscribedTopics.Select(x => MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE).ToArray();
            _mqttClient.Subscribe(_subscribedTopics.ToArray(), qosLevels);
        }

        /// <summary>
        /// Unsubscribes from the MQTT topics in SubscribedTopics.
        /// </summary>
        protected virtual void Unsubscribe()
        {
            // Wipe subscriptions
            if (_mqttClient.IsConnected)
            {
                _serviceLog.Debug("MQTT unsubscribing to the following topics: " + string.Join(", ", _subscribedTopics));
                _mqttClient.Unsubscribe(_subscribedTopics.ToArray());
            }
        }

        #endregion
    }
}
