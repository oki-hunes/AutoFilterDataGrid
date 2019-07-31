using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.Specialized;
using System.Collections.ObjectModel;



namespace BetterDataGrid
{
    public class AutoFilterDataGrid : DataGrid, INotifyPropertyChanged
    {
        private List<FilterValue> filterList;

        public static System.Windows.Input.RoutedCommand SortColumnCommand = new RoutedCommand("SortColumn", typeof(AutoFilterDataGrid));

        public static System.Windows.Input.RoutedCommand SelectColumnCommand = new RoutedCommand("SelectColumn", typeof(AutoFilterDataGrid));

        public event PropertyChangedEventHandler PropertyChanged;

        public AutoFilterDataGrid() : base()
        {
            filterList = new List<FilterValue>();
            this.Loaded += AutoFilterDataGridLoaded;
            this.Items.Filter = new Predicate<object>(this.Contains);
        }
        
        public List<FilterValue> FilterList
        {
            get
            {
                return filterList;
            }
            set
            {
                filterList = value;
                NotifyPropertyChanged("FilterList");
            }
        }
        private void Columns_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (DataGridColumn thisColumn in e.NewItems)
                {
                    thisColumn.HeaderTemplate
                    thisColumn.HeaderStyle = ColumnHeaderStyle;
                }
            }
        }
        private void NotifyPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        internal void AllButton_Click(object sender, RoutedEventArgs e)
        {
            this.Focus();
        }
        protected override void OnCopyingRowClipboardContent(DataGridRowClipboardEventArgs args)
        {
            List<DataGridClipboardCellContent> originalContent = new List<DataGridClipboardCellContent>(args.ClipboardRowContent.AsEnumerable());
            args.ClipboardRowContent.Clear();
            for (int x = 0; x < originalContent.Count; x++)
            {
                args.ClipboardRowContent.Add(new DataGridClipboardCellContent(originalContent[x].Item, originalContent[x].Column, originalContent[x].Content.ToString().Replace('\0', ' ')));
            }
            base.OnCopyingRowClipboardContent(args);
        }
        private bool Contains(object testObject)
        {
            bool filtered = false;
            foreach (FilterValue thisFilter in filterList)
            {
                string testObjectValue = testObject.GetType().GetProperty(thisFilter.PropertyName).GetMethod.Invoke(testObject, new object[] { }).ToString();
                foreach (string filterValue in thisFilter.FilteredValues)
                {
                    if (filterValue == testObjectValue)
                    {
                        filtered = true;
                        return !filtered;
                    }
                }
            }
            return !filtered;
        }
        private void AutoFilterDataGridLoaded(object sender, RoutedEventArgs e)
        {
            GenerateFilterList();
        }
        private void GenerateFilterList()
        {
            filterList = new List<FilterValue>();
            foreach (DataGridBoundColumn thisColumn in this.Columns)
            {
                filterList.Add(new FilterValue(((Binding)thisColumn.Binding).Path.Path.ToString(), new List<string>()));
            }
        }
    }
}