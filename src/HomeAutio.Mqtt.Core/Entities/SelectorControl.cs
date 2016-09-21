using System.Collections.Generic;

namespace HomeAutio.Mqtt.Core.Entities
{
    public class SelectorControl : StatefulControl
    {
        public SelectorControl() : this(new Dictionary<string, string>()) { }
        public SelectorControl(IDictionary<string, string> selectionLabels)
        {
            SelectionLabels = selectionLabels;
        }

        public IDictionary<string, string> SelectionLabels { get; set; }
    }
}
