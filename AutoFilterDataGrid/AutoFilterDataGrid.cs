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
    [TemplatePart(Name = "PART_ColumnHeadersPresenter", Type = typeof(DataGridColumnHeadersPresenter))]
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
        public event FilterChangedEventHandler FilterChanged;
        public event CannotDeleteValueEventHandler CannotDeleteValue;
        private DataGridColumnHeadersPresenter headersPresenter;
        //private int testInt;
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
                this.Items.Filter = new Predicate<object>(this.Contains);
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
            SetBinding(ItemsControl.ItemsSourceProperty, new Binding()
            {
                Source = this,
                Path = new PropertyPath("ItemsSource"),
                Converter = new ListToListCollectionViewConverter(),
                Mode = BindingMode.TwoWay
            });
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, new ExecutedRoutedEventHandler(ExecutedCut), new CanExecuteRoutedEventHandler(CanExecuteCut)));
            this.Initialized += AutoFilterDataGrid_Initialized;
            //ItemsSource = new ArrayList();
        }

        private void AutoFilterDataGrid_Initialized(object sender, EventArgs e)
        {
            GenerateFilterList();
            if (CanUserFilterData)
                this.Items.Filter = new Predicate<object>(this.Contains);
        }

        private void CanExecuteCut(object sender, CanExecuteRoutedEventArgs e)
        {
            OnCanExecuteCut(e);
        }
        private void ExecutedCut(object sender, ExecutedRoutedEventArgs e)
        {
            OnExecutedCut(e);
        }
        protected virtual void OnCanExecuteCut(CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ApplicationCommands.Copy.CanExecute(null, this) && ApplicationCommands.Delete.CanExecute(null, this);
            e.Handled = true;
        }
        protected virtual void OnExecutedCut(ExecutedRoutedEventArgs e)
        {
            ApplicationCommands.Copy.Execute(null, this);
            ApplicationCommands.Delete.Execute(null, this);
            e.Handled = true;
        }
        protected override void OnCanExecuteDelete(CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !this.IsReadOnly && (this.SelectionUnit != DataGridSelectionUnit.FullRow || this.CanUserDeleteRows) && this.SelectedCells.Count > 0;
            e.Handled = true;
        }
        protected override void OnExecutedDelete(ExecutedRoutedEventArgs e)
        {
            List<object> fullRows = new List<object>();
            foreach(object thisItem in this.SelectedItems)
            {
                //this.ItemContainerGenerator.ContainerFromItem(thisItem) is DataGridRow row && row.IsSelected && 
                if (thisItem.GetType().ToString() != "MS.Internal.NamedObject")
                {
                    fullRows.Add(thisItem);
                }
            }
            foreach(object thisItem in fullRows)
            {
                if (this.SelectionUnit == DataGridSelectionUnit.FullRow)
                {
                    this.SelectedItems.Remove(thisItem);
                }
                else
                {
                    DataGridCellInfo[] tempCells = new DataGridCellInfo[this.SelectedCells.Count];
                    SelectedCells.CopyTo(tempCells, 0);
                    foreach (DataGridCellInfo thisCell in tempCells)
                    {
                        if (thisCell.Item == thisItem)
                            SelectedCells.Remove(thisCell);
                    }
                }
                if (this.ItemsSource != null)
                {
                    ((this as ItemsControl).ItemsSource as ListCollectionView).Remove(thisItem);
                }
                else
                {
                    (this.Items as IList).Remove(thisItem);
                }
            } 
            List<Exception> cannotDeleteExceptions = new List<Exception>();
            foreach (DataGridCellInfo thisCell in this.SelectedCells)
            {
                try
                {
                    if(thisCell.Item.GetType().ToString() != "MS.Internal.NamedObject")
                        DeleteCellValue(thisCell);
                }
                catch(NotSupportedException ex)
                {
                    cannotDeleteExceptions.Add(ex);
                }
            }
            if (cannotDeleteExceptions.Count > 0)
                throw new AggregateException(cannotDeleteExceptions);
        }
        private MethodInfo GetSetFieldMethod()
        {
            foreach(MethodInfo thisMethod in typeof(DataRowExtensions).GetMethods())
            {
                if(thisMethod.Name == "SetField")
                {
                    foreach(ParameterInfo parameter in thisMethod.GetParameters())
                    {
                        if (parameter.Name == "columnName" && parameter.ParameterType == typeof(string))
                            return thisMethod;
                    }
                }
            }
            return null;
        }
        private object[] GetInvokeParams(PropertyPath propertyPath, Type valueType, object item)
        {
            bool canBeNull = !valueType.IsValueType;
            if (item.GetType() == typeof(DataRow) || item.GetType().IsSubclassOf(typeof(DataRow)))
            {
                if (IsNumericType(valueType) && !canBeNull)
                    return new object[] { item, propertyPath.Path, 0 };
                else if (canBeNull)
                    return new object[] { item, propertyPath.Path, null };
                else if (Nullable.GetUnderlyingType(valueType) != null)
                    return new object[] { item, propertyPath.Path, valueType.GetConstructor(new Type[] { Nullable.GetUnderlyingType(valueType) }).Invoke(new object[] { null }) };
                else
                    return new object[] { item, propertyPath.Path, valueType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()) };
            }
            else if (item.GetType() == typeof(DataRowView) || item.GetType().IsSubclassOf(typeof(DataRowView)))
            {
                if (IsNumericType(valueType) && !canBeNull)
                    return new object[] { (item as DataRowView).Row, propertyPath.Path, 0 };
                else if (canBeNull)
                    return new object[] { (item as DataRowView).Row, propertyPath.Path, null };
                else if (Nullable.GetUnderlyingType(valueType) != null)
                    return new object[] { (item as DataRowView).Row, propertyPath.Path, valueType.GetConstructor(new Type[] { Nullable.GetUnderlyingType(valueType) }).Invoke(new object[] { null }) };
                else
                    return new object[] { (item as DataRowView).Row, propertyPath.Path, valueType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()) };
            }
            else
            {
                if (IsNumericType(valueType) && !canBeNull)
                    return new object[] { 0 };
                else if (canBeNull)
                    return new object[] { null };
                else if (Nullable.GetUnderlyingType(valueType) != null)
                    return new object[] { valueType.GetConstructor(new Type[] { Nullable.GetUnderlyingType(valueType) }).Invoke(new object[] { null }) };
                else
                    return new object[] { valueType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()) };
            }
        }
        protected void DeleteCellValue(DataGridCellInfo thisCell)
        {
            PropertyPath propertyPath = ((thisCell.Column as DataGridBoundColumn).Binding as Binding).Path;
            object[] invokeParams = Array.Empty<object>();
            MethodInfo setMethod;
            object invokeItem;
            Type valueType;
            if (thisCell.Item.GetType() == typeof(DataRow) || thisCell.Item.GetType().IsSubclassOf(typeof(DataRow)))
            {
                setMethod = GetSetFieldMethod().MakeGenericMethod(new Type[] { typeof(object) });
                valueType = ((DataRow)thisCell.Item).Field<object>(propertyPath.Path).GetType();
                invokeItem = null;
            }
            else if (thisCell.Item.GetType() == typeof(DataRowView) || thisCell.Item.GetType().IsSubclassOf(typeof(DataRowView)))
            {
                setMethod = GetSetFieldMethod().MakeGenericMethod(new Type[] { typeof(object) });
                valueType = ((DataRowView)thisCell.Item).Row.Field<object>(propertyPath.Path).GetType();
                invokeItem = null;
            }
            else
            {
                setMethod = thisCell.Item.GetType().GetProperty(propertyPath.Path).GetSetMethod();
                valueType = thisCell.Item.GetType().GetProperty(propertyPath.Path).PropertyType;
                invokeItem = thisCell.Item;
            }
            if (valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null && valueType.GetConstructor(Array.Empty<Type>()) == null && !IsNumericType(thisCell.Item.GetType().GetProperty(propertyPath.Path).PropertyType))
            {
                if (CannotDeleteValue != null)
                    this.CannotDeleteValue.Invoke(this, new CannotDeleteValueEventArgs(thisCell.Item, propertyPath, valueType));
                else
                    throw new NotSupportedException("Cannot delete value because it's type " + valueType.ToString() + " cannot be set to null and does not have a parameterless constructor");
            }
            else
            {
                setMethod.Invoke(invokeItem, GetInvokeParams(propertyPath, valueType, thisCell.Item));
            }
        }
        public bool IsNumericType(Type t)
        {
            if (t == null)
                return false;
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
        private static void OnItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                _ = new ListCollectionView(Array.Empty<string>());
                CollectionView tempView;
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
                        d.GetType().GetMethod("SetFilter", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(d, Array.Empty<object>());
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
                        d.GetType().GetMethod("SetFilter", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(d, Array.Empty<object>());
                    }
                }
            }
            else
                throw new ArgumentNullException("e.NewValue");
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
                if (thisHeader != null)
                {
                    try
                    {
                        thisHeader.Click -= DataGridColumnHeader_Click;
                        thisHeader.MouseDoubleClick -= DataGridColumnHeader_MouseDoubleClick;
                        if (thisHeader.Template.FindName("PART_FilterButton", thisHeader) is ButtonBase tempButton)
                        {
                            tempButton.Click -= FilterButton_Click;
                        }
                        if (thisHeader.Template.FindName("FilterPopup", thisHeader) is Popup tempPopup)
                        {
                            tempPopup.Closed -= FilterPopup_Closed;
                        }
                    }
                    finally
                    {
                        thisHeader.Click += DataGridColumnHeader_Click;
                        thisHeader.MouseDoubleClick += DataGridColumnHeader_MouseDoubleClick;
                        if (thisHeader.Template.FindName("PART_FilterButton", thisHeader) is ButtonBase tempButton)
                        {
                            tempButton.Click += FilterButton_Click;
                        }
                        if (thisHeader.Template.FindName("FilterPopup", thisHeader) is Popup tempPopup)
                        {
                            tempPopup.Closed += FilterPopup_Closed;
                        }
                    }
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
                    FilterValue thisColumnFilter = GetFilterValueFromColumn((DataGridBoundColumn)thisColumn);
                    if(thisColumnFilter != null)
                        filterList.Remove(thisColumnFilter);
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
                    object CellValue = ((DataRow)testObject).Field<object>(thisFilter.PropertyName);
                    if (CellValue != null)
                        testObjectValue = CellValue.ToString();
                }
                else if (testObject.GetType() == typeof(DataRowView) || testObject.GetType().IsSubclassOf(typeof(DataRowView)))
                {
                    object CellValue = ((DataRowView)testObject).Row.Field<object>(thisFilter.PropertyName);
                    if(CellValue != null)
                        testObjectValue = CellValue.ToString();
                }
                else
                {
                    object CellValue = testObject.GetType().GetProperty(thisFilter.PropertyName).GetMethod.Invoke(testObject, Array.Empty<object>());
                    if (CellValue != null)
                        testObjectValue = CellValue.ToString();
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
            this.Columns.CollectionChanged -= Columns_CollectionChanged;
            this.Columns.CollectionChanged += Columns_CollectionChanged;
            try
            {
                ((this.GetTemplateChild("DG_ScrollViewer") as ScrollViewer).Template.FindName("AllButton", this.GetTemplateChild("DG_ScrollViewer") as FrameworkElement) as Button).Click += AllButton_Click;
            }
            finally
            {
                UpdateEventHandlers();
            }
            if (headersPresenter == null)
            {
                headersPresenter = FindDataGridColumnHeadersPresenter(this);
                if (headersPresenter != null)
                {
                    headersPresenter.LayoutUpdated -= HeadersPresenter_LayoutUpdated;
                    headersPresenter.LayoutUpdated += HeadersPresenter_LayoutUpdated;
                }
            }
        }
        private void GenerateFilterList()
        {
            filterList = new List<FilterValue>();
            foreach (DataGridColumn thisColumn in this.Columns)
            {
                if (typeof(DataGridBoundColumn).IsAssignableFrom(thisColumn.GetType()) && (thisColumn as DataGridBoundColumn).Binding != null)
                {
                    PropertyPath propertyPath = ((Binding)(thisColumn as DataGridBoundColumn).Binding).Path;
                    if (propertyPath != null && propertyPath.Path != null)
                        filterList.Add(new FilterValue(propertyPath.Path.ToString(), new List<string>()));
                }
            }
        }
        public void ClearFilter()
        {
            List<FilterValue> oldFilter = (from FilterValue thisFilter in FilterList
                                           select new FilterValue(thisFilter.PropertyName, thisFilter.FilteredValues)).ToList();
            bool hadFilters = false;
            foreach(FilterValue thisFilter in FilterList)
            {
                if (thisFilter.FilteredValues.Count > 0)
                    hadFilters = true;
                thisFilter.FilteredValues = new List<string>();
            }
            if(hadFilters)
                FilterChanged?.Invoke(this, new FilterChangedEventArgs(oldFilter, FilterList));
        }
        internal void FilterPopup_Closed(object sender, EventArgs e)
        {
            List<FilterValue> oldFilter = (from FilterValue thisFilter in FilterList
                                           select new FilterValue(thisFilter.PropertyName, thisFilter.FilteredValues)).ToList();
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
            //thisColumnFilter.FilteredValues = new List<string>();
            FilterValue tempFilter = new FilterValue(thisColumnFilter.PropertyName, new List<string>());
            for (int x = 1; x < popupContent.Items.Count; x++)
            {
                CheckBox tempCheck = popupContent.Items[x] as CheckBox;
                if (!tempCheck.IsChecked.HasValue || !tempCheck.IsChecked.Value)
                    tempFilter.FilteredValues.Add(tempCheck.Content.ToString());
            }
            bool changed = tempFilter != thisColumnFilter;
            thisColumnFilter.FilteredValues = tempFilter.FilteredValues;
            this.Items.Filter = new Predicate<object>(this.Contains);
            if(changed)
                FilterChanged?.Invoke(this, new FilterChangedEventArgs(oldFilter, FilterList));
        }

        internal void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO rewrite to use the listview directly instead of the property to see if that fixes the disappearing all button
            Button filterButton = (Button)sender;
            int columnIndex = ((DataGridColumnHeader)filterButton.TemplatedParent).DisplayIndex;
            FilterValue thisColumnFilter = GetFilterValueFromColumn((DataGridBoundColumn)this.Columns[columnIndex]) ?? new FilterValue();
            List<object> columnValues = new List<object>();
            List<string> columnValueStrings = new List<string>();
            CheckBox allCheck = new CheckBox
            {
                Content = "All",
                FontWeight = FontWeights.Bold,
            };
            allCheck.Checked += AllCheckBox_Checked;
            allCheck.Unchecked += AllCheckBox_Unchecked;
            Popup filterPopup = (Popup)filterButton.Tag;
            filterPopup.Name = "FilterPopup";
            ListBox popupContent = (ListBox)filterPopup.FindName("FilterList");
            popupContent.Items.Clear();
            popupContent.Items.Add(allCheck);
            if (((this.ItemsSource ?? this.Items) as IList).Count > 0)
            {
                PropertyPath propertyPath = ((Binding)((DataGridBoundColumn)this.Columns[columnIndex]).Binding).Path;
                foreach (object item in this.ItemsSource ?? this.Items)
                {
                    if (item.GetType().ToString() != "MS.Internal.NamedObject")
                    {
                        object CellValue;
                        if (item.GetType() == typeof(DataRow) || item.GetType().IsSubclassOf(typeof(DataRow)))
                        {
                            CellValue = ((DataRow)item).Field<object>(propertyPath.Path);
                        }
                        else if (item.GetType() == typeof(DataRowView) || item.GetType().IsSubclassOf(typeof(DataRowView)))
                        {
                            CellValue = ((DataRowView)item).Row.Field<object>(propertyPath.Path);
                        }
                        else
                        {
                            CellValue = item.GetType().GetProperty(propertyPath.Path).GetMethod.Invoke(item, Array.Empty<object>());
                        }
                        if (CellValue != null && !columnValues.Contains(CellValue))
                            columnValues.Add(CellValue);
                    }
                }
                if(columnValues.Count > 0 && columnValues.All(thisValue => thisValue.GetType() == columnValues[0].GetType()) && columnValues[0].GetType().GetInterfaces().Contains(typeof(IComparable)))
                {
                    Type castType = columnValues[0].GetType();
                    columnValues.Sort(delegate (object x, object y)
                    {
                        dynamic dx = Convert.ChangeType(x, castType);
                        return dx.CompareTo(y);
                    });
                    columnValueStrings = columnValues.ConvertAll(convertValue => convertValue.ToString());
                }
                else
                {
                    columnValueStrings = columnValues.ConvertAll(convertValue => convertValue.ToString());
                    columnValueStrings.Sort();
                }
                foreach (string thisValue in columnValueStrings.Distinct())
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
        private static bool HasFilterPopupParent(FrameworkElement element)
        {
            if (element is null)
                return false;
            DependencyObject logicalParent = LogicalTreeHelper.GetParent(element);
            DependencyObject visualParent = VisualTreeHelper.GetParent(element);
            if (IsFilterPopup(logicalParent) || IsFilterPopup(visualParent))
                return true;
            else if (logicalParent is FrameworkElement && HasFilterPopupParent(logicalParent as FrameworkElement))
                return true;
            else if (visualParent is FrameworkElement && HasFilterPopupParent(visualParent as FrameworkElement))
                return true;
            else
                return false;
        }
        private static bool IsFilterPopup(DependencyObject element)
        {
            if (element is Popup && (element as Popup).Name == "FilterPopup")
                return true;
            else
                return false;
        }
        internal void DataGridColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            
            System.Windows.Controls.Primitives.DataGridColumnHeader columnHeader = (System.Windows.Controls.Primitives.DataGridColumnHeader)sender;
            if (this.CanSelectMultipleItems && (this.SelectionUnit == DataGridSelectionUnit.Cell || this.SelectionUnit == DataGridSelectionUnit.CellOrRowHeader) && (!(e.OriginalSource is ButtonBase) || (e.OriginalSource as ButtonBase).Name != "PART_FilterButton") && (!(e.OriginalSource is FrameworkElement) || !HasFilterPopupParent(e.OriginalSource as FrameworkElement)))
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
        private FilterValue GetFilterValueFromColumn(DataGridBoundColumn column)
        {
            foreach (FilterValue thisFilter in filterList)
            {
                if (thisFilter.PropertyName == ((Binding)column.Binding).Path.Path.ToString())
                    return thisFilter;
            }
            return null;
        }
        protected class ListToListCollectionViewConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null)
                    return null;
                _ = new ListCollectionView(Array.Empty<string>());
                CollectionView tempView;
                if (value is CollectionView || typeof(CollectionView).IsAssignableFrom(value.GetType()))
                {
                    tempView = value as CollectionView;
                }
                else
                {
                    tempView = new ListCollectionView((IList)value);
                }
                return tempView;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return value as IList;
            }
        }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (headersPresenter == null)
                UpdateEventHandlers();
        }
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            headersPresenter = FindDataGridColumnHeadersPresenter(this);
            if(headersPresenter != null)
                headersPresenter.LayoutUpdated += HeadersPresenter_LayoutUpdated;
        }

        private void HeadersPresenter_LayoutUpdated(object sender, EventArgs e)
        {
            UpdateEventHandlers();
        }

        private DataGridColumnHeadersPresenter FindDataGridColumnHeadersPresenter(FrameworkElement parent)
        {
            PropertyInfo visualChildCountProperty = typeof(FrameworkElement).GetProperty("VisualChildrenCount", BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo logicalChildrenProperty = typeof(FrameworkElement).GetProperty("LogicalChildren", BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo hasLogicalChildrenProperty = typeof(FrameworkElement).GetProperty("HasLogicalChildren", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo getVisualChildMethod = typeof(FrameworkElement).GetMethod("GetVisualChild", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
            for (int x = 0; x < (int)visualChildCountProperty.GetValue(parent); x++)
            {
                object child = getVisualChildMethod.Invoke(parent, new object[] { x });
                if(child is DataGridColumnHeadersPresenter && (child as DataGridColumnHeadersPresenter).Name == "PART_ColumnHeadersPresenter")
                {
                    return child as DataGridColumnHeadersPresenter;
                }
                else if(child is FrameworkElement)
                {
                    DataGridColumnHeadersPresenter ret = FindDataGridColumnHeadersPresenter(child as FrameworkElement);
                    if (ret != null)
                        return ret;
                }
            }
            if((bool)hasLogicalChildrenProperty.GetValue(parent) == true)
            {
                IEnumerator childEnumerator = (logicalChildrenProperty.GetValue(parent) as IEnumerator);
                while(childEnumerator.MoveNext())
                {
                    if (childEnumerator.Current is DataGridColumnHeadersPresenter)
                    {
                        return childEnumerator.Current as DataGridColumnHeadersPresenter;
                    }
                    else if(childEnumerator.Current is FrameworkElement)
                    {
                        DataGridColumnHeadersPresenter ret = FindDataGridColumnHeadersPresenter(childEnumerator.Current as FrameworkElement);
                        if(ret != null)
                            return ret;
                    }

                }
            }
            return null;
        }
    }
    public class CannotDeleteValueEventArgs : EventArgs
    {
        private readonly object item;
        private readonly PropertyPath property;
        private readonly Type propertyType;

        public CannotDeleteValueEventArgs(object item, PropertyPath property, Type propertyType)
        {
            this.item = item;
            this.property = property;
            this.propertyType = propertyType;
        }

        public object Item { get => item; }
        public PropertyPath Property { get => property; }
        public Type PropertyType { get => propertyType; }
    }
    public delegate void CannotDeleteValueEventHandler(object sender, CannotDeleteValueEventArgs e);
    public class FilterChangedEventArgs : EventArgs
    {
        readonly List<FilterValue> oldValue;
        readonly List<FilterValue> newValue;

        public FilterChangedEventArgs(List<FilterValue> oldValue, List<FilterValue> newValue)
        {
            this.oldValue = oldValue;
            this.newValue = newValue;
        }

        public List<FilterValue> OldValue { get => oldValue; }
        public List<FilterValue> NewValue { get => newValue; }
    }
    public delegate void FilterChangedEventHandler(object sender, FilterChangedEventArgs e);
    internal class FilterButtonVisibilityConverter : IMultiValueConverter
    {
        public virtual object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            foreach(object thisObject in values)
            {
                if(thisObject.GetType() != typeof(bool) && !typeof(bool).IsAssignableFrom(thisObject.GetType()))
                {
                    throw new ArgumentException("All value objects must be type bool", nameof(values));
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
    internal class ColumnTypeFilterBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;
            if (typeof(DataGridBoundColumn).IsAssignableFrom(value.GetType()) && (value as DataGridBoundColumn).Binding != null && ((value as DataGridBoundColumn).Binding as Binding).Path != null)
                return true;
            else
                return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("This converter does not support converting back to the original value");
        }
    }
}