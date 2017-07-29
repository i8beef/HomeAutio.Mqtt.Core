using System.Collections.Generic;

namespace HomeAutio.Mqtt.Core.Entities
{
    /// <summary>
    /// Root hub object for representing control options from a hub.
    /// </summary>
    public class Hub
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Hub"/> class.
        /// </summary>
        public Hub()
        {
            Devices = new List<Device>();
        }

        /// <summary>
        /// Name of the hub.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Devices controlled by the hub.
        /// </summary>
        public IList<Device> Devices { get; set; }
    }
}
