using System.Collections.Generic;

namespace HomeAutio.Mqtt.Core.Entities
{
    /// <summary>
    /// Device controlled by a hub.
    /// </summary>
    public class Device
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Device"/> class.
        /// </summary>
        public Device()
        {
            Controls = new List<Control>();
        }

        /// <summary>
        ///  Name of the device.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Controls exposed by the device.
        /// </summary>
        public IList<Control> Controls { get; set; }
    }
}
