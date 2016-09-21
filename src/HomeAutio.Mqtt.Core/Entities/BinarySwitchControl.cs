namespace HomeAutio.Mqtt.Core.Entities
{
    public class BinarySwitchControl : StatefulControl
    {
        public BinarySwitchControl() : this("ON", "OFF") { }

        public BinarySwitchControl(string onStateLabel, string offStateLabel)
        {
            OnStateLabel = onStateLabel;
            OffStateLabel = offStateLabel;
        }

        public string OnStateLabel { get; set; }
        public string OffStateLabel { get; set; }

        public string OnStateArgument { get; set; }
        public string OffStateArgument { get; set; }
    }
}
