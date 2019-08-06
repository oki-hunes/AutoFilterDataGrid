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
using System.Collections;
using System.Globalization;

namespace BetterDataGrid
{
    public class AutoFilterDataGrid : DataGrid, INotifyPropertyChanged
    {
        static AutoFilterDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoFilterDataGrid), new
            FrameworkPropertyMetadata(typeof(AutoFilterDataGrid)));
        }
        public static readonly DependencyProperty CanUserFilterDataProperty = DependencyProperty.Register(
            "CanUserFilterData",
            typeof(bool),
            typeof(AutoFilterDataGrid),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender)
            );
        public new static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            "ItemsSource",
            typeof(IList),
            typeof(AutoFilterDataGrid),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourcePropertyChanged)
            );
        private List<FilterValue> filterList;
        public event PropertyChangedEventHandler PropertyChanged;
        public new event DataGridSortingEventHandler Sorting;
        [Category("Columns")]
        public bool CanUserFilterData
        {
            get
            {
                return (bool)GetValue(CanUserFilterDataProperty);
            }
            set
            {
                SetValue(CanUserFilterDataProperty, value);
                this.Items.Filter = value ? new Predicate<object>(this.Contains) : null;
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
        public new IList ItemsSource
        {
            get
            {
                return (IList)GetValue(ItemsSourceProperty);
            }
            set
            {
                SetValue(ItemsSourceProperty, value);
            }
        }

        public AutoFilterDataGrid() : base()
        {
            filterList = new List<FilterValue>();
            this.Loaded += AutoFilterDataGridLoaded;
        }
        private static void OnItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                CollectionView tempView = new ListCollectionView(Array.Empty<string>());
                if (e.NewValue is CollectionView || typeof(CollectionView).IsAssignableFrom(e.NewValue.GetType()))
                {
                    tempView = e.NewValue as CollectionView;
                    if (!tempView.CanFilter && (bool)d.GetValue(CanUserFilterDataProperty))
                    {
                        d.SetValue(e.Property, e.OldValue);
                        throw new NotSupportedException("The ItemsSource must have CanFilter equal to true when CanUserFilterData is also true.");
                    }
                    else
                    {
                        d.SetValue(ItemsControl.ItemsSourceProperty, tempView);
                        d.GetType().GetMethod("SetFilter", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(d, new object[] { });
                    }
                }
                else
                {
                    tempView = new ListCollectionView((IList)e.NewValue);
                    if (!tempView.CanFilter && (bool)d.GetValue(CanUserFilterDataProperty))
                    {
                        d.SetValue(e.Property, e.OldValue);
                        throw new NotSupportedException("The ItemsSource must have CanFilter equal to true after being converted to a ListCollectionView when CanUserFilterData is also true.");
                    }
                    else
                    {
                        d.SetValue(ItemsControl.ItemsSourceProperty, tempView);
                        d.GetType().GetMethod("SetFilter", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(d, new object[] { });
                    }
                }
            }
            else
                throw new ArgumentNullException("value");
        }
        internal void SetFilter()
        {
            this.Items.Filter = CanUserFilterData ? new Predicate<object>(this.Contains) : null;
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

                if ((child is DataGridColumnHeader colHeader) && (colHeader.Column == column))
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
                string testObjectValue = "";
                if (testObject.GetType() == typeof(DataRow) || testObject.GetType().IsSubclassOf(typeof(DataRow)))
                {
                    testObjectValue = ((DataRow)testObject).Field<object>(thisFilter.PropertyName).ToString();
                }
                else if (testObject.GetType() == typeof(DataRowView) || testObject.GetType().IsSubclassOf(typeof(DataRowView)))
                {
                    testObjectValue = ((DataRowView)testObject).Row.Field<object>(thisFilter.PropertyName).ToString();
                }
                else
                {
                    testObjectValue = testObject.GetType().GetProperty(thisFilter.PropertyName).GetMethod.Invoke(testObject, new object[] { }).ToString();
                }
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
            if (CanUserFilterData)
                this.Items.Filter = new Predicate<object>(this.Contains);
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
            ListBox popupContent = (ListBox)filterPopup.FindName("FilterList");
            int columnIndex = ((DataGridColumnHeader)filterPopup.TemplatedParent).DisplayIndex;
            FilterValue thisColumnFilter = filterList.Find((thisFilter) =>
            {
                if (thisFilter != null && thisFilter.PropertyName == ((Binding)((DataGridBoundColumn)this.Columns[columnIndex]).Binding).Path.Path.ToString())
                    return true;
                else
                    return false;
            });
            thisColumnFilter.FilteredValues = new List<string>();
            for (int x = 1; x < popupContent.Items.Count; x++)
            {
                CheckBox tempCheck = popupContent.Items[x] as CheckBox;
                if (!tempCheck.IsChecked.HasValue || !tempCheck.IsChecked.Value)
                    thisColumnFilter.FilteredValues.Add(tempCheck.Content.ToString());
            }
            if (CanUserFilterData)
                this.Items.Filter = new Predicate<object>(this.Contains);
        }

        internal void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO rewrite to use the listview directly instead of the property to see if that fixes the disappearing all button
            Button filterButton = (Button)sender;
            int columnIndex = ((DataGridColumnHeader)filterButton.TemplatedParent).DisplayIndex;
            FilterValue thisColumnFilter = new FilterValue();
            foreach (FilterValue thisFilter in filterList)
            {
                if (thisFilter.PropertyName == ((Binding)((DataGridBoundColumn)this.Columns[columnIndex]).Binding).Path.Path.ToString())
                    thisColumnFilter = thisFilter;
            }
            List<string> columnValues = new List<string>();
            CheckBox allCheck = new CheckBox
            {
                Content = "All",
                FontWeight = FontWeights.Bold,
            };
            allCheck.Checked += AllCheckBox_Checked;
            allCheck.Unchecked += AllCheckBox_Unchecked;
            Popup filterPopup = (Popup)filterButton.Tag;
            ListBox popupContent = (ListBox)filterPopup.FindName("FilterList");
            popupContent.Items.Clear();
            popupContent.Items.Add(allCheck);
            if (this.HasItems)
            {
                PropertyPath propertyPath = ((Binding)((DataGridBoundColumn)this.Columns[columnIndex]).Binding).Path;
                Type itemsType = this.Items[0].GetType();
                foreach (object item in this.ItemsSource ?? this.Items)
                {
                    if (item.GetType().ToString() != "MS.Internal.NamedObject")
                    {
                        string thisValue = "";
                        if (itemsType == typeof(DataRow) || itemsType.IsSubclassOf(typeof(DataRow)))
                        {
                            thisValue = ((DataRow)item).Field<object>(propertyPath.Path).ToString();
                        }
                        else if (itemsType == typeof(DataRowView) || itemsType.IsSubclassOf(typeof(DataRowView)))
                        {
                            thisValue = ((DataRowView)item).Row.Field<object>(propertyPath.Path).ToString();
                        }
                        else
                        {
                            thisValue = itemsType.GetProperty(propertyPath.Path).GetMethod.Invoke(item, new object[] { }).ToString();
                        }
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
                    popupContent.Items.Add(tempCheck);
                }
            }
            filterPopup.IsOpen = true;
            if (popupContent.Items.Count > 1)
            {
                bool? all = true;
                for (int x = 1; x < popupContent.Items.Count; x++)
                {
                    CheckBox thisCheck = popupContent.Items[x] as CheckBox;
                    if (thisCheck.IsChecked == false && all.HasValue && all.Value == true)
                    {
                        if(x == 1)
                            all = false;
                        else
                            all = new bool?();
                    }
                    if (thisCheck.IsChecked == true && all.HasValue && all.Value == false)
                    {
                        all = new bool?();
                    }
                }
                (popupContent.Items[0] as CheckBox).IsChecked = all;
            }
            else
            {
                (popupContent.Items[0] as CheckBox).IsChecked = true;
            }
        }
        private void AllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ListBox popupContent = ((CheckBox)sender).Parent as ListBox;
            for (int x = 1; x < popupContent.Items.Count; x++)
            {
                CheckBox thisCheck = popupContent.Items[x] as CheckBox;
                thisCheck.IsChecked = true;
            }
        }

        private void AllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ListBox popupContent = ((CheckBox)sender).Parent as ListBox;
            for (int x = 1; x < popupContent.Items.Count; x++)
            {
                CheckBox thisCheck = popupContent.Items[x] as CheckBox;
                thisCheck.IsChecked = false;
            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ListBox popupContent = ((CheckBox)sender).Parent as ListBox;
            bool all = true;
            for (int x = 1; x < popupContent.Items.Count; x++)
            {
                CheckBox thisCheck = popupContent.Items[x] as CheckBox;
                if (thisCheck.IsChecked == false)
                {
                    all = false;
                }
            }
            (popupContent.Items[0] as CheckBox).IsChecked = all ? true : new bool?();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ListBox popupContent = ((CheckBox)sender).Parent as ListBox;
            bool all = true;
            for (int x = 1; x < popupContent.Items.Count; x++)
            {
                CheckBox thisCheck = popupContent.Items[x] as CheckBox;
                if (thisCheck.IsChecked != false)
                {
                    all = false;
                }
            }
             (popupContent.Items[0] as CheckBox).IsChecked = all ? false : new bool?();
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
    public class FilterButtonVisibilityConverter : IMultiValueConverter
    {
        public virtual object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null)
                throw new ArgumentNullException("values");
            foreach(object thisObject in values)
            {
                if(thisObject.GetType() != typeof(bool) && !typeof(bool).IsAssignableFrom(thisObject.GetType()))
                {
                    throw new ArgumentException("All value objects must be type bool", "values");
                }
            }
            IEnumerable boolValues = values.Cast<bool>();
            bool result = true;
            foreach(bool thisBool in boolValues)
            {
                if (!thisBool)
                    result = false;
            }
            BooleanToVisibilityConverter booleanToVisibility = new BooleanToVisibilityConverter();
            return booleanToVisibility.Convert(result, typeof(Visibility), null, CultureInfo.CurrentCulture);
        }

        public virtual object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("This converter does not support converting back to the original value");
        }
    }
}