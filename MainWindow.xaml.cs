using System;
using System.Runtime.InteropServices;
using System.Windows;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Woobly.ViewModels;

namespace Woobly;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isExpanded;
    private int _currentPage = 0;
    private System.Windows.Point _startPoint;
    private bool _isDragging;
    private DispatcherTimer _idleTimer;
    private readonly UIElement[] _pages;

    // Win32 API for window positioning
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _pages = new UIElement[] { Page1, Page2, Page3, Page4, Page5, Page6 };

            // Set up idle timer (no longer used for auto-collapse)
            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            // Auto-collapse disabled - only collapse on focus loss (OnDeactivated)

            // Set up mouse interaction for swiping
            ContentGrid.MouseLeftButtonDown += ContentGrid_MouseLeftButtonDown;
            ContentGrid.MouseLeftButtonUp += ContentGrid_MouseLeftButtonUp;
            ContentGrid.MouseMove += ContentGrid_MouseMove;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Initialization Error: {ex.Message}\n\n{ex.StackTrace}", 
                          "Woobly Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
            throw;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position at top center of screen
        PositionWindow();
        
        // Ensure window stays on top
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }

    private void PositionWindow()
    {
        var screen = SystemParameters.PrimaryScreenWidth;
        Left = (screen - Width) / 2;
        Top = 20;
    }

    private void Window_MouseDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (e.LeftButton == WpfInput.MouseButtonState.Pressed && !_isExpanded)
        {
            ExpandIsland();
        }
        
        ResetIdleTimer();
    }

    private void ExpandIsland()
    {
        if (_isExpanded) return;
        
        _isExpanded = true;
        _viewModel.IsExpanded = true;
        
        // Get the expand animation from resources
        var expandAnim = (Storyboard)App.Current.Resources["ExpandAnimation"];
        var expandClone = expandAnim.Clone();
        
        // Calculate center position for expanded state
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var targetLeft = (screenWidth - 400) / 2;
        
        // Animate window position smoothly
        var leftAnim = new DoubleAnimation
        {
            To = targetLeft,
            Duration = TimeSpan.FromSeconds(0.5),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        
        // Start content fade transition
        CollapsedContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromSeconds(0.2),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
        
        // After a short delay, switch content and fade in
        var switchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.15) };
        switchTimer.Tick += (s, e) =>
        {
            switchTimer.Stop();
            CollapsedContent.Visibility = Visibility.Collapsed;
            ExpandedContent.Visibility = Visibility.Visible;
            ExpandedContent.Opacity = 0;
            
            ExpandedContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.35),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        };
        switchTimer.Start();
        
        // Start all animations
        this.BeginAnimation(Window.LeftProperty, leftAnim);
        expandClone.Begin(this);
    }

    private void CollapseIsland()
    {
        if (!_isExpanded) return;
        
        _isExpanded = false;
        _viewModel.IsExpanded = false;
        _idleTimer.Stop();
        
        // Get the collapse animation from resources
        var collapseAnim = (Storyboard)App.Current.Resources["CollapseAnimation"];
        var collapseClone = collapseAnim.Clone();
        
        // Calculate center position for collapsed state
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var targetLeft = (screenWidth - 150) / 2;
        
        // Animate window position smoothly
        var leftAnim = new DoubleAnimation
        {
            To = targetLeft,
            Duration = TimeSpan.FromSeconds(0.5),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        
        // Start content fade transition
        ExpandedContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromSeconds(0.2),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
        
        // After a short delay, switch content and fade in
        var switchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.15) };
        switchTimer.Tick += (s, e) =>
        {
            switchTimer.Stop();
            ExpandedContent.Visibility = Visibility.Collapsed;
            CollapsedContent.Visibility = Visibility.Visible;
            CollapsedContent.Opacity = 0;
            
            CollapsedContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.35),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        };
        switchTimer.Start();
        
        // Start all animations
        this.BeginAnimation(Window.LeftProperty, leftAnim);
        collapseClone.Begin(this);
    }

    private void ResetIdleTimer()
    {
        // Idle timer disabled - only collapse on focus loss
        // _idleTimer.Stop();
        // _idleTimer.Start();
    }

    private void ContentGrid_MouseLeftButtonDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (_isExpanded)
        {
            _startPoint = e.GetPosition(ContentGrid);
            _isDragging = true;
        }
    }

    private void ContentGrid_MouseLeftButtonUp(object sender, WpfInput.MouseButtonEventArgs e)
    {
        _isDragging = false;
    }

    private void ContentGrid_MouseMove(object sender, WpfInput.MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == WpfInput.MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(ContentGrid);
            var delta = currentPoint.X - _startPoint.X;
            
            if (Math.Abs(delta) > 50)
            {
                if (delta > 0 && _currentPage > 0)
                {
                    NavigateToPage(_currentPage - 1);
                }
                else if (delta < 0 && _currentPage < 5)
                {
                    NavigateToPage(_currentPage + 1);
                }
                _isDragging = false;
            }
        }
        
        ResetIdleTimer();
    }

    private void NavigateToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex > 5) return;
        
        // Fade out current page with slide animation
        var fadeOutAnim = (Storyboard)App.Current.Resources["PageFadeOut"];
        var fadeOutClone = fadeOutAnim.Clone();
        fadeOutClone.Completed += (s, e) =>
        {
            _pages[_currentPage].Visibility = Visibility.Collapsed;
            _currentPage = pageIndex;
            _pages[_currentPage].Visibility = Visibility.Visible;
            _viewModel.CurrentPageIndex = pageIndex;
            
            // Fade in new page with slide animation
            var fadeInAnim = (Storyboard)App.Current.Resources["PageFadeIn"];
            var fadeInClone = fadeInAnim.Clone();
            fadeInClone.Begin(_pages[_currentPage] as System.Windows.FrameworkElement);
        };
        fadeOutClone.Begin(_pages[_currentPage] as System.Windows.FrameworkElement);
        
        ResetIdleTimer();
    }

    private void PageDot_Click(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Shapes.Ellipse ellipse && ellipse.Tag is string tagStr && int.TryParse(tagStr, out int pageIndex))
        {
            NavigateToPage(pageIndex);
        }
    }

    private void PageDot_MouseEnter(object sender, WpfInput.MouseEventArgs e)
    {
        if (sender is System.Windows.Shapes.Ellipse ellipse)
        {
            var hoverInAnim = (Storyboard)App.Current.Resources["DotHoverIn"];
            var clone = hoverInAnim.Clone();
            clone.Begin(ellipse);
            
            // Enhance glow effect
            if (ellipse.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
            {
                var glowAnim = new DoubleAnimation
                {
                    To = 0.6,
                    Duration = TimeSpan.FromSeconds(0.2)
                };
                shadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, glowAnim);
            }
        }
    }

    private void PageDot_MouseLeave(object sender, WpfInput.MouseEventArgs e)
    {
        if (sender is System.Windows.Shapes.Ellipse ellipse)
        {
            var hoverOutAnim = (Storyboard)App.Current.Resources["DotHoverOut"];
            var clone = hoverOutAnim.Clone();
            clone.Begin(ellipse);
            
            // Reduce glow effect
            if (ellipse.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
            {
                var glowAnim = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.2)
                };
                shadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, glowAnim);
            }
        }
    }

    private void AIInputBox_KeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        if (e.Key == WpfInput.Key.Enter)
        {
            var message = AIInputBox.Text;
            if (!string.IsNullOrWhiteSpace(message))
            {
                _viewModel.SendAIMessage(message);
                AIInputBox.Clear();
            }
            ResetIdleTimer();
        }
    }

    private void TaskInputBox_KeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        if (e.Key == WpfInput.Key.Enter)
        {
            var content = TaskInputBox.Text;
            if (!string.IsNullOrWhiteSpace(content))
            {
                _viewModel.AddTask(content);
                TaskInputBox.Clear();
            }
            ResetIdleTimer();
        }
    }

    private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        // The binding already updated IsCompleted, we just need to save
        _viewModel.SaveTasks();
        ResetIdleTimer();
    }

    private void RemoveTask_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as WpfControls.Button;
        if (button?.Tag != null && Guid.TryParse(button.Tag.ToString(), out Guid taskId))
        {
            _viewModel.RemoveTask(taskId);
        }
        ResetIdleTimer();
    }

    private void ClipboardItem_MouseDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        var border = sender as WpfControls.Border;
        if (border?.Tag is string content)
        {
            _viewModel.RestoreClipboard(content);
        }
        ResetIdleTimer();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveSettings();
        ResetIdleTimer();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (_isExpanded)
        {
            CollapseIsland();
        }
    }
    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        // Prevent accidental window closing - user must close via Task Manager or Ctrl+C
        e.Cancel = true;
        CollapseIsland();
    }
}