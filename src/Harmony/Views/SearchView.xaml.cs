using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Harmony.ViewModels;

namespace Harmony.Views;

public partial class SearchView : UserControl
{
    public SearchView() => InitializeComponent();

    private void OnSuggestionClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string suggestion) return;
        if (DataContext is SearchViewModel vm)
            vm.PickSuggestionCommand.Execute(suggestion);
    }
}
