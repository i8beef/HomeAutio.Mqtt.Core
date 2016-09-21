namespace HomeAutio.Mqtt.Core.Entities
{
    public abstract class Control
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CommandTopic { get; set; }
    }
}
