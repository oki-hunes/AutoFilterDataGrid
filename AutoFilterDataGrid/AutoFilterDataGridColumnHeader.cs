using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace BetterDataGrid
{
    [System.Windows.TemplatePart(Name = "PART_LeftHeaderGripper", Type = typeof(System.Windows.Controls.Primitives.Thumb))]
    [System.Windows.TemplatePart(Name = "PART_RightHeaderGripper", Type = typeof(System.Windows.Controls.Primitives.Thumb))]
    [System.Windows.TemplatePart(Name = "PART_FilterButton", Type = typeof(System.Windows.Controls.Primitives.ButtonBase))]
    [System.Windows.TemplatePart(Name = "PART_FilterPopup", Type = typeof(System.Windows.Controls.Primitives.Popup))]
    public class AutoFilterDataGridColumnHeader : DataGridColumnHeader
    {
        static AutoFilterDataGridColumnHeader()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AutoFilterDataGridColumnHeader), new FrameworkPropertyMetadata(typeof(AutoFilterDataGridColumnHeader)));
        }
        public AutoFilterDataGridColumnHeader()
        {
        }
        public override void OnApplyTemplate()
        {
            AutoFilterDataGrid parent = FindParent<AutoFilterDataGrid>(this);
            if(parent != null)
            {
                this.Click += parent.DataGridColumnHeader_Click;
                this.MouseDoubleClick += parent.DataGridColumnHeader_MouseDoubleClick;
                ButtonBase filterButton = this.GetTemplateChild("PART_FilterButton") as ButtonBase;
                if(filterButton != null)
                {
                    filterButton.Click += parent.FilterButton_Click;
                }
                Popup filterPopup = this.GetTemplateChild("PART_FilterPopup") as Popup;
                if (filterPopup != null)
                {
                    filterPopup.Closed += parent.FilterPopup_Closed;
                }
            }
            base.OnApplyTemplate();
        }
        private protected static T FindParent<T>(FrameworkElement element) where T : FrameworkElement
        {
            FrameworkElement parent = element.TemplatedParent as FrameworkElement;

            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }

                parent = parent.TemplatedParent as FrameworkElement;
            }

            return null;
        }
    }
}
