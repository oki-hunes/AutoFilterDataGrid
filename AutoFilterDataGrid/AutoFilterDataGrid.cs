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
using System.Reflection;
using System.Windows.Media;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Windows.Controls.Primitives;



namespace BetterDataGrid
{
    public class AutoFilterDataGrid : DataGrid, INotifyPropertyChanged
    {
        static AutoFilterDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoFilterDataGrid), new
            FrameworkPropertyMetadata(typeof(AutoFilterDataGrid)));
        }
        public static readonly DependencyProperty FilterPopupContentProperty = DependencyProperty.Register(
            "FilterPopupContent",
            typeof(ObservableCollection<CheckBox>),
            typeof(AutoFilterDataGrid),
            new FrameworkPropertyMetadata(new ObservableCollection<CheckBox>(), FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender)
            );
        private List<FilterValue> filterList;

        public event PropertyChangedEventHandler PropertyChanged;
        public new event DataGridSortingEventHandler Sorting;

        public ObservableCollection<CheckBox> FilterPopupContent
        {
            get
            {
                return (ObservableCollection<CheckBox>)GetValue(FilterPopupContentProperty);
            }
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

        public AutoFilterDataGrid() : base()
        {
            CheckBox tempCheck = new CheckBox
            {
                Content = "All",
                FontWeight = FontWeights.Bold,
            };
            tempCheck.Checked += AllCheckBox_Checked;
            tempCheck.Unchecked += AllCheckBox_Unchecked;
            FilterPopupContent.Add(tempCheck);
            filterList = new List<FilterValue>();
            this.Loaded += AutoFilterDataGridLoaded;
            this.Items.Filter = new Predicate<object>(this.Contains);
        }
        private void UpdateEventHandlers()
        {
            foreach (DataGridColumn thisColumn in this.Columns)
            {
                DataGridColumnHeader thisHeader = GetHeader(thisColumn, this);
                try
                {
                    thisHeader.Click -= DataGridColumnHeader_Click;
                    thisHeader.MouseDoubleClick -= DataGridColumnHeader_MouseDoubleClick;
                    (thisHeader.Template.FindName("FilterButton", thisHeader) as Button).Click -= FilterButton_Click;
                    (thisHeader.Template.FindName("FilterPopup", thisHeader) as Popup).Closed -= FilterPopup_Closed;
                }
                finally
                {
                    thisHeader.Click += DataGridColumnHeader_Click;
                    thisHeader.MouseDoubleClick += DataGridColumnHeader_MouseDoubleClick;
                    (thisHeader.Template.FindName("FilterButton", thisHeader) as Button).Click += FilterButton_Click;
                    (thisHeader.Template.FindName("FilterPopup", thisHeader) as Popup).Closed += FilterPopup_Closed;
                }
            }
        }
        private DataGridColumnHeader GetHeader(DataGridColumn column, DependencyObject reference)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(reference); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(reference, i);

                DataGridColumnHeader colHeader = child as DataGridColumnHeader;
                if ((colHeader != null) && (colHeader.Column == column))
                {
                    return colHeader;
                }

                colHeader = GetHeader(column, child);
                if (colHeader != null)
                {
                    return colHeader;
                }
            }

            return null;
        }
        private void Columns_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (DataGridBoundColumn thisColumn in e.NewItems)
                {
                    if(thisColumn.DisplayIndex == filterList.Count)
                        filterList.Add(new FilterValue(((Binding)thisColumn.Binding).Path.Path.ToString(), new List<string>()));
                    else
                        filterList.Insert(thisColumn.DisplayIndex, new FilterValue(((Binding)thisColumn.Binding).Path.Path.ToString(), new List<string>()));
                }
                UpdateEventHandlers();
                
            }
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (DataGridBoundColumn thisColumn in e.OldItems)
                {
                    filterList.RemoveAt(thisColumn.DisplayIndex);
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                foreach (DataGridBoundColumn thisColumn in e.OldItems)
                {
                    filterList.RemoveAt(thisColumn.DisplayIndex);
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
            this.Columns.CollectionChanged += Columns_CollectionChanged;
            try
            {
                ((this.GetTemplateChild("DG_ScrollViewer") as ScrollViewer).Template.FindName("AllButton", this.GetTemplateChild("DG_ScrollViewer") as FrameworkElement) as Button).Click += AllButton_Click;
            }
            finally
            {
                UpdateEventHandlers();
            }
        }
        private void GenerateFilterList()
        {
            filterList = new List<FilterValue>();
            foreach (DataGridBoundColumn thisColumn in this.Columns)
            {
                filterList.Add(new FilterValue(((Binding)thisColumn.Binding).Path.Path.ToString(), new List<string>()));
            }
        }
        internal void FilterPopup_Closed(object sender, EventArgs e)
        {
            Popup filterPopup = (Popup)sender;
            int columnIndex = ((DataGridColumnHeader)filterPopup.TemplatedParent).DisplayIndex;
            FilterValue thisColumnFilter = new FilterValue();
            foreach (FilterValue thisFilter in filterList)
            {
                if (thisFilter.PropertyName == ((Binding)((DataGridBoundColumn)this.Columns[columnIndex]).Binding).Path.Path.ToString())
                    thisColumnFilter = thisFilter;
            }
            thisColumnFilter.FilteredValues = new List<string>();
            for (int x = 1; x < FilterPopupContent.Count; x++)
            {
                CheckBox tempCheck = FilterPopupContent[x];
                if (!tempCheck.IsChecked.HasValue || !tempCheck.IsChecked.Value)
                    thisColumnFilter.FilteredValues.Add(tempCheck.Content.ToString());

            }
            this.Items.Filter = new Predicate<object>(this.Contains);
        }

        internal void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            Button filterButton = (Button)sender;
            int columnIndex = ((DataGridColumnHeader)filterButton.TemplatedParent).DisplayIndex;
            FilterValue thisColumnFilter = new FilterValue();
            foreach (FilterValue thisFilter in filterList)
            {
                if (thisFilter.PropertyName == ((Binding)((DataGridBoundColumn)this.Columns[columnIndex]).Binding).Path.Path.ToString())
                    thisColumnFilter = thisFilter;
            }
            List<string> columnValues = new List<string>();
            Popup filterPopup = (Popup)filterButton.Tag;
            for (int x = 1; x < FilterPopupContent.Count; x = 1)
            {
                FilterPopupContent.RemoveAt(x);
            }
            if (this.HasItems)
            {
                PropertyPath propertyPath = ((Binding)((DataGridBoundColumn)this.Columns[columnIndex]).Binding).Path;
                Type itemsType = this.Items[0].GetType();
                foreach (object item in this.Items.SourceCollection)
                {
                    if (item.GetType().ToString() != "MS.Internal.NamedObject")
                    {
                        string thisValue = itemsType.GetProperty(propertyPath.Path).GetMethod.Invoke(item, new object[] { }).ToString();
                        if (!columnValues.Contains(thisValue))
                            columnValues.Add(thisValue);
                    }
                }
                columnValues.Sort();
                foreach (string thisValue in columnValues)
                {
                    CheckBox tempCheck = new CheckBox
                    {
                        Content = thisValue
                    };
                    if (!thisColumnFilter.FilteredValues.Contains(thisValue))
                    {
                        tempCheck.IsChecked = true;
                    }
                    tempCheck.Checked += CheckBox_Checked;
                    tempCheck.Unchecked += CheckBox_Unchecked;
                    FilterPopupContent.Add(tempCheck);
                }
            }
            filterPopup.IsOpen = true;
            if (FilterPopupContent.Count > 1)
            {
                bool? all = true;
                for (int x = 1; x < FilterPopupContent.Count; x++)
                {
                    CheckBox thisCheck = FilterPopupContent[x];
                    if (thisCheck.IsChecked == false && all.HasValue && all.Value == true)
                    {
                        all = false;
                    }
                    if (thisCheck.IsChecked == true && all.HasValue && all.Value == false)
                    {
                        all = new bool?();
                    }
                }
                FilterPopupContent[0].IsChecked = all;
            }
            else
            {
                FilterPopupContent[0].IsChecked = true;
            }
        }
        private void AllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            for (int x = 1; x < FilterPopupContent.Count; x++)
            {
                CheckBox thisCheck = FilterPopupContent[x];
                thisCheck.IsChecked = true;
            }
        }

        private void AllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            for (int x = 1; x < FilterPopupContent.Count; x++)
            {
                CheckBox thisCheck = FilterPopupContent[x];
                thisCheck.IsChecked = false;
            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            bool all = true;
            for (int x = 1; x < FilterPopupContent.Count; x++)
            {
                CheckBox thisCheck = FilterPopupContent[x];
                if (thisCheck.IsChecked == false)
                {
                    all = false;
                }
            }
            FilterPopupContent[0].IsChecked = all ? true : new bool?();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            bool all = true;
            for (int x = 1; x < FilterPopupContent.Count; x++)
            {
                CheckBox thisCheck = FilterPopupContent[x];
                if (thisCheck.IsChecked != false)
                {
                    all = false;
                }
            }
            FilterPopupContent[0].IsChecked = all ? false : new bool?();
        }
        internal void DataGridColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Primitives.DataGridColumnHeader columnHeader = (System.Windows.Controls.Primitives.DataGridColumnHeader)sender;
            if (this.CanSelectMultipleItems && (this.SelectionUnit == DataGridSelectionUnit.Cell || this.SelectionUnit == DataGridSelectionUnit.CellOrRowHeader) && (e.OriginalSource.GetType() != typeof(Button) || (e.OriginalSource as Button).Name != "FilterButton"))
            {
                if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl) && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                    this.SelectedCells.Clear();
                int startIndex = columnHeader.DisplayIndex;
                int numCols = 1;
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) && this.SelectedCells.Count > 0)
                {
                    if (this.CurrentCell == null || this.CurrentCell.Column == null)
                    {
                        ConcurrentBag<DataGridColumn> tempSelectionColumns = new ConcurrentBag<DataGridColumn>();
                        List<DataGridCellInfo> selectedCells = this.SelectedCells.ToList();
                        Parallel.For(0, selectedCells.Count, (i) =>
                        {
                            if (!tempSelectionColumns.Contains(selectedCells[(int)i].Column))
                                tempSelectionColumns.Add(selectedCells[(int)i].Column);
                        }
                        );
                        List<DataGridColumn> selectionColumns = tempSelectionColumns.GroupBy(i => i.DisplayIndex).Select(group => group.First()).OrderBy(i => i.DisplayIndex).ToList();
                        if (columnHeader.DisplayIndex - selectionColumns[0].DisplayIndex == 0)
                            numCols = 1;
                        else if (columnHeader.DisplayIndex - selectionColumns[0].DisplayIndex < 0)
                            numCols = selectionColumns[0].DisplayIndex - columnHeader.DisplayIndex;
                        else
                        {
                            startIndex = selectionColumns[0].DisplayIndex + 1;
                            numCols = columnHeader.DisplayIndex - selectionColumns[0].DisplayIndex;
                        }
                    }
                    else
                    {
                        if (columnHeader.DisplayIndex - this.CurrentCell.Column.DisplayIndex == 0)
                            numCols = 1;
                        else if (columnHeader.DisplayIndex - this.CurrentCell.Column.DisplayIndex < 0)
                            numCols = this.CurrentCell.Column.DisplayIndex - columnHeader.DisplayIndex;
                        else
                        {
                            startIndex = this.CurrentCell.Column.DisplayIndex + 1;
                            numCols = columnHeader.DisplayIndex - this.CurrentCell.Column.DisplayIndex;
                        }
                    }
                }
                for (int x = startIndex; x < startIndex + numCols; x++)
                {
                    SelectColumn(this.Columns[x]);
                }
                this.Focus();
            }
        }
        private void SelectColumn(DataGridColumn column)
        {
            foreach (object item in this.Items)
            {
                DataGridCellInfo dataGridCellInfo = new DataGridCellInfo(item, column);
                if (!this.SelectedCells.Contains(dataGridCellInfo))
                    this.SelectedCells.Add(dataGridCellInfo);
            }
        }
        internal void DataGridColumnHeader_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Controls.Primitives.DataGridColumnHeader columnHeader = (System.Windows.Controls.Primitives.DataGridColumnHeader)sender;
            if (this.CanUserSortColumns)
            {
                columnHeader.Column.SortDirection = columnHeader.Column.SortDirection == null || columnHeader.Column.SortDirection == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
                foreach (DataGridColumn thisColumn in this.Columns)
                {
                    if (thisColumn != columnHeader.Column)
                        thisColumn.SortDirection = null;
                }
                this.Items.SortDescriptions.Clear();
                this.Items.SortDescriptions.Add(new SortDescription(columnHeader.Column.SortMemberPath, columnHeader.Column.SortDirection.Value));
                this.Focus();
            }
        }
        protected override void OnSorting(DataGridSortingEventArgs eventArgs)
        {
            Sorting?.Invoke(this, eventArgs);
        }
    }
}