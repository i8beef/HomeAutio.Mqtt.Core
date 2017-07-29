using System.Collections.Generic;

namespace HomeAutio.Mqtt.Core.Entities
{
    /// <summary>
    /// Selector control.
    /// </summary>
    public class SelectorControl : StatefulControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SelectorControl"/> class.
        /// </summary>
        public SelectorControl()
            : this(new Dictionary<string, string>())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectorControl"/> class.
        /// </summary>
        /// <param name="selectionLabels">Selection labels.</param>
        public SelectorControl(IDictionary<string, string> selectionLabels)
        {
            SelectionLabels = selectionLabels;
        }

        /// <summary>
        /// Selection labels.
        /// </summary>
        public IDictionary<string, string> SelectionLabels { get; set; }
    }
}
