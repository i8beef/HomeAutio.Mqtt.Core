namespace HomeAutio.Mqtt.Core.Entities
{
    /// <summary>
    /// Dimmer control.
    /// </summary>
    public class DimmerControl : StatefulControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DimmerControl"/> class.
        /// </summary>
        public DimmerControl()
            : this(0, 100)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DimmerControl"/> class.
        /// </summary>
        /// <param name="lowerBound">Lower bound.</param>
        /// <param name="upperBound">Upper bound.</param>
        public DimmerControl(int lowerBound, int upperBound)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        /// <summary>
        /// Lower bound for dimmer.
        /// </summary>
        public int LowerBound { get; set; }

        /// <summary>
        /// Upper bound for dimmer.
        /// </summary>
        public int UpperBound { get; set; }
    }
}
