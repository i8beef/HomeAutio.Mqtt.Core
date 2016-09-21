using System.Collections.Generic;

namespace HomeAutio.Mqtt.Core.Entities
{
    public class Hub
    {
        public Hub()
        {
            Devices = new List<Device>();
        }

        public string Name { get; set; }
        public IList<Device> Devices { get; set; }
    }
}
