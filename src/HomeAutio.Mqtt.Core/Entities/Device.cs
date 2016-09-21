using System.Collections.Generic;

namespace HomeAutio.Mqtt.Core.Entities
{
    public class Device
    {
        public Device()
        {
            Controls = new List<Control>();
        }

        public string Name { get; set; }
        public IList<Control> Controls { get; set; }
    }
}
