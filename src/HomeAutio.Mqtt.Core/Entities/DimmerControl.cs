namespace HomeAutio.Mqtt.Core.Entities
{
    public class DimmerControl : StatefulControl
    {
        public DimmerControl() : this(0, 100) { }

        public DimmerControl(int lowerBound, int upperBound)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        public int LowerBound { get; set; }
        public int UpperBound { get; set; }
    }
}
