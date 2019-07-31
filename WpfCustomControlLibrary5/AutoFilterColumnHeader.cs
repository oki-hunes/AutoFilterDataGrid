using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.ComponentModel;


namespace BetterDataGrid
{
    public class AutoFilterColumnHeader : DataGridColumnHeader
    {
        //public static RoutedCommand OpenPopup = new RoutedCommand("OpenPopup", typeof(AutoFilterColumnHeader));
        //public static RoutedCommand ClosePopup = new RoutedCommand("ClosePopup", typeof(AutoFilterColumnHeader));
        public static readonly DependencyProperty FilterPopupContentProperty = DependencyProperty.Register(
            "FilterPopupContent",
            typeof(ObservableCollection<CheckBox>),
            typeof(AutoFilterDataGrid),
            new FrameworkPropertyMetadata(new ObservableCollection<CheckBox>(), FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender)
            );
        public Popup FilterPopup;
        public AutoFilterColumnHeader() : base()
        {
            CheckBox tempCheck = new CheckBox
            {
                Content = "All",
                FontWeight = FontWeights.Bold,
                IsThreeState = true
            };
            tempCheck.Checked += AllCheckBox_Checked;
            tempCheck.Unchecked += AllCheckBox_Unchecked;
            FilterPopupContent.Add(tempCheck);
            //this.CommandBindings.Add(new CommandBinding(OpenPopup, OnOpenPopupExecuted, OnOpenPopupCanExecute));
            //this.CommandBindings.Add(new CommandBinding(ClosePopup, OnClosePopupExecuted));
            FilterPopup = new Popup
            {
                StaysOpen = false,
                Width = double.NaN
            };
            Grid tempGrid = new Grid
            {
                UseLayoutRounding = true,
                Width = double.NaN,
                Background = SystemColors.ControlBrush
            };
            ListBox tempListbox = new ListBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = double.NaN,
                UseLayoutRounding = true,
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = double.NaN,
                ItemsSource = FilterPopupContent
            };
            tempGrid.Children.Add(tempListbox);
            FilterPopup.Child = tempGrid;
            FilterPopup.Closed += ClosePopup;
            this.AddVisualChild(FilterPopup);
        }
        public ObservableCollection<CheckBox> FilterPopupContent
        {
            get
            {
                return (ObservableCollection<CheckBox>)GetValue(FilterPopupContentProperty);
            }
        }
        public RoutedEventHandler OpenPopupHandler
        {
            get
            {
                return new RoutedEventHandler(OpenPopup);
            }
        }
        internal void OpenPopup(object sender, RoutedEventArgs e)
        {
            GeneratePopupContent();
            FilterPopup.IsOpen = true;
        }
        private void ClosePopup(object sender, EventArgs e)
        {
            AutoFilterDataGrid dataGrid = (AutoFilterDataGrid)this.Column.GetType().GetProperty("DataGridOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this.Column, null);
            int columnIndex = this.DisplayIndex;
            FilterValue thisColumnFilter = new FilterValue();
            foreach (FilterValue thisFilter in dataGrid.FilterList)
            {
                if (thisFilter.PropertyName == ((Binding)((DataGridBoundColumn)dataGrid.Columns[columnIndex]).Binding).Path.Path.ToString())
                    thisColumnFilter = thisFilter;
            }
            thisColumnFilter.FilteredValues = new List<string>();
            for (int x = 1; x < FilterPopupContent.Count; x++)
            {
                CheckBox tempCheck = FilterPopupContent[x];
                if (!tempCheck.IsChecked.HasValue || !tempCheck.IsChecked.Value)
                    thisColumnFilter.FilteredValues.Add(tempCheck.Content.ToString());

            }
            (dataGrid.ItemsSource as CollectionView).Refresh();
        }

        public void GeneratePopupContent()
        {
            AutoFilterDataGrid dataGrid = (AutoFilterDataGrid)this.Column.GetType().GetProperty("DataGridOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this.Column, null);
            int columnIndex = this.DisplayIndex;
            FilterValue thisColumnFilter = new FilterValue();
            foreach (FilterValue thisFilter in dataGrid.FilterList)
            {
                if (thisFilter.PropertyName == ((Binding)((DataGridBoundColumn)dataGrid.Columns[columnIndex]).Binding).Path.Path.ToString())
                    thisColumnFilter = thisFilter;
            }
            List<string> columnValues = new List<string>();
            for (int x = 1; x < FilterPopupContent.Count; x = 1)
            {
                FilterPopupContent.RemoveAt(x);
            }
            if (dataGrid.HasItems)
            {
                PropertyPath propertyPath = ((Binding)((DataGridBoundColumn)dataGrid.Columns[columnIndex]).Binding).Path;
                Type itemsType = dataGrid.Items[0].GetType();
                foreach (object item in dataGrid.Items.SourceCollection)
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
            
            AutoFilterDataGrid dataGrid = (AutoFilterDataGrid)this.Column.GetType().GetProperty("DataGridOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this.Column, null);
            if ((bool)dataGrid.GetType().GetProperty("CanSelectMultipleItems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dataGrid, null))
            {
                if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl) && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                    dataGrid.SelectedCells.Clear();
                int startIndex = this.DisplayIndex;
                int numCols = 1;
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) && dataGrid.SelectedCells.Count > 0)
                {
                    if (dataGrid.CurrentCell == null || dataGrid.CurrentCell.Column == null)
                    {
                        ConcurrentBag<DataGridColumn> tempSelectionColumns = new ConcurrentBag<DataGridColumn>();
                        List<DataGridCellInfo> selectedCells = dataGrid.SelectedCells.ToList();
                        Parallel.For(0, selectedCells.Count, (i) =>
                        {
                            if (!tempSelectionColumns.Contains(selectedCells[(int)i].Column))
                                tempSelectionColumns.Add(selectedCells[(int)i].Column);
                        }
                        );
                        List<DataGridColumn> selectionColumns = tempSelectionColumns.GroupBy(i => i.DisplayIndex).Select(group => group.First()).OrderBy(i => i.DisplayIndex).ToList();
                        if (this.DisplayIndex - selectionColumns[0].DisplayIndex == 0)
                            numCols = 1;
                        else if (this.DisplayIndex - selectionColumns[0].DisplayIndex < 0)
                            numCols = selectionColumns[0].DisplayIndex - this.DisplayIndex;
                        else
                        {
                            startIndex = selectionColumns[0].DisplayIndex + 1;
                            numCols = this.DisplayIndex - selectionColumns[0].DisplayIndex;
                        }
                    }
                    else
                    {
                        if (this.DisplayIndex - dataGrid.CurrentCell.Column.DisplayIndex == 0)
                            numCols = 1;
                        else if (this.DisplayIndex - dataGrid.CurrentCell.Column.DisplayIndex < 0)
                            numCols = dataGrid.CurrentCell.Column.DisplayIndex - this.DisplayIndex;
                        else
                        {
                            startIndex = dataGrid.CurrentCell.Column.DisplayIndex + 1;
                            numCols = this.DisplayIndex - dataGrid.CurrentCell.Column.DisplayIndex;
                        }
                    }
                }
                for (int x = startIndex; x < startIndex + numCols; x++)
                {
                    SelectColumn(dataGrid.Columns[x]);
                }
                this.Focus();
            }
        }
        private void SelectColumn(DataGridColumn column)
        {
            AutoFilterDataGrid dataGrid = (AutoFilterDataGrid)this.Column.GetType().GetProperty("DataGridOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this.Column, null);
            foreach (object item in dataGrid.Items)
            {
                DataGridCellInfo dataGridCellInfo = new DataGridCellInfo(item, column);
                if (!dataGrid.SelectedCells.Contains(dataGridCellInfo))
                    dataGrid.SelectedCells.Add(dataGridCellInfo);
            }
        }
        internal void DataGridColumnHeader_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AutoFilterDataGrid dataGrid = (AutoFilterDataGrid)this.Column.GetType().GetProperty("DataGridOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this.Column, null);
            if (dataGrid.CanUserSortColumns)
            {
                this.Column.SortDirection = this.Column.SortDirection == null || this.Column.SortDirection == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
                foreach (DataGridColumn thisColumn in dataGrid.Columns)
                {
                    if (thisColumn != this.Column)
                        thisColumn.SortDirection = null;
                }
                dataGrid.Items.SortDescriptions.Clear();
                dataGrid.Items.SortDescriptions.Add(new SortDescription(this.Column.SortMemberPath, this.Column.SortDirection.Value));
                dataGrid.Focus();
            }
        }
    }
}