using System;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace LLMR.Views;

public partial class MainWindow : Window
{
    private readonly ScrollViewer _scrollViewer;
    public MainWindow()
    {
        InitializeComponent();
        this.FindControl<ItemsControl>("ConsoleItemsControl").Items.CollectionChanged += ItemsOnCollectionChanged;
        _scrollViewer = this.FindControl<ScrollViewer>("ConsoleScrollViewer") ?? throw new InvalidOperationException("<MW error> ScrollViewer was not found.");
        
        this.Closed += MainWindow_Closed;
    }

    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _scrollViewer.ScrollToEnd();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    
    public void OnLinkClicked(object sender, PointerPressedEventArgs e)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "https://github.com/mmsbld/LLMR",
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ConsoleScrollViewer.ScrollToEnd();
        }, Avalonia.Threading.DispatcherPriority.Loaded); // change priority after loaded
    }

}