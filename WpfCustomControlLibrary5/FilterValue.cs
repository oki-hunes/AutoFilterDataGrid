using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BetterDataGrid
{
    public class FilterValue
    {
        internal FilterValue()
        {
            FilteredValues = new List<string>();
            PropertyName = "";
        }

        public FilterValue(string propertyName, List<string> filteredValues)
        {
            FilteredValues = filteredValues;
            PropertyName = propertyName;
        }

        public List<string> FilteredValues { get; set; }
        public string PropertyName { get; set; }
    }
}