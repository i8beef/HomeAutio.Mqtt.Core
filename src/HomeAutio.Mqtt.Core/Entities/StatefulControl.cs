namespace HomeAutio.Mqtt.Core.Entities
{
    public abstract class StatefulControl : Control
    {
        public string ValueTopic { get; set; }
    }
}
