# AutoFilterDataGrid
WPF Datagrid with Excel like auto filter

# XAML
add<br/>
   xmlns:ctrl="clr-namespace:BetterDataGrid;assembly=AutoFilterDataGrid" 
<br />
...
<br />
&lt;ctrl:AutoFilterDataGrid <br />
  x:Name="fooGrid" 
      ItemsSource="{Binding FilterdView}" 
      SelectionMode="Extended" 
      ScrollViewer.CanContentScroll="True" 
      ScrollViewer.VerticalScrollBarVisibility="Auto" 
      ScrollViewer.HorizontalScrollBarVisibility="Auto"
      CanUserAddRows="False" AutoGenerateColumns="True" IsReadOnly="True"  
      EnableColumnVirtualization="True"
      EnableRowVirtualization="True"&gt;
&lt;/ctrl:AutoFilterDataGrid&gt;
