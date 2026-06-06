using System.Windows;
using System.Windows.Input;

namespace PowerMonitor.App.Views;

public partial class ExportDialog : Window
{
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool Confirmed { get; private set; }

    public ExportDialog()
    {
        InitializeComponent();
        StartPicker.SelectedDate = DateTime.Today.AddDays(-7);
        EndPicker.SelectedDate = DateTime.Today;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (StartPicker.SelectedDate == null || EndPicker.SelectedDate == null)
        {
            MessageBox.Show("请选择开始和结束日期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StartDate = StartPicker.SelectedDate.Value;
        EndDate = EndPicker.SelectedDate.Value.AddDays(1);
        if (StartDate >= EndDate)
        {
            MessageBox.Show("开始日期必须早于结束日期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Confirmed = true;
        Close();
    }
}
