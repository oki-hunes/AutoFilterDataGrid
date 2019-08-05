using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BetterDataGrid
{
    public class FilterValue : IEquatable<FilterValue>
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

        public override bool Equals(object obj)
        {
            return Equals(obj as FilterValue);
        }

        public bool Equals(FilterValue other)
        {
            bool isEqual = true;
            if (other != null)
            {
                foreach (string thisValue in FilteredValues)
                {
                    if (!other.FilteredValues.Contains(thisValue))
                        isEqual = false;
                }
                isEqual = isEqual && FilteredValues.Count == other.FilteredValues.Count;
            }
            return other != null &&
                   isEqual &&
                   PropertyName == other.PropertyName;
        }

        public override int GetHashCode()
        {
            var hashCode = 271077783;
            hashCode = hashCode * -1521134295 + EqualityComparer<List<string>>.Default.GetHashCode(FilteredValues);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(PropertyName);
            return hashCode;
        }

        public static bool operator ==(FilterValue value1, FilterValue value2)
        {
            return EqualityComparer<FilterValue>.Default.Equals(value1, value2);
        }

        public static bool operator !=(FilterValue value1, FilterValue value2)
        {
            return !(value1 == value2);
        }
    }
}