using System.Windows;
using ProjectsSearchRAG.App.ViewModels;

namespace ProjectsSearchRAG.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
