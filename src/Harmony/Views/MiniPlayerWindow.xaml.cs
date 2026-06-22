using System.Windows;
using System.Windows.Input;
using Harmony.ViewModels;

namespace Harmony.Views;

public partial class MiniPlayerWindow : Window
{
    public MiniPlayerWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }
}
