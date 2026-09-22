using Avalonia.Controls;
using Avalonia.Threading;
using Voyage.EarthquakeWarning.Models;
using Voyage.EarthquakeWarning.UI;

namespace Voyage.EarthquakeWarning.Services;

public sealed class WarningWindowService
{
    private EarthquakeWarningWindow? _window;
    private bool _userClosed;

    // 用户点击预警界面关闭按钮时触发
    public event EventHandler? UserClosed;

    public void ShowOrUpdate(
        WarningState state,
        bool topMost,
        bool allowReopen)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (allowReopen)
                _userClosed = false;

            if (_userClosed)
                return;

            if (_window is null)
            {
                _window = new EarthquakeWarningWindow();
                _window.Closed += WindowClosed;
            }

            _window.Topmost = topMost;

            if (!_window.IsVisible)
            {
                var screen = _window.Screens.Primary;

                if (screen is not null)
                {
                    _window.Width =
                        Math.Max(
                            1100,
                            screen.Bounds.Width / 2.0);

                    _window.Height =
                        Math.Max(
                            620,
                            screen.Bounds.Height / 2.0);
                }

                _window.WindowStartupLocation =
                    WindowStartupLocation.CenterScreen;

                _window.Show();
            }

            _window.UpdateState(state);
        });
    }

    public void Close()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _window?.Close();
        });
    }

    private void WindowClosed(
        object? sender,
        EventArgs e)
    {
        var userRequested =
            sender is EarthquakeWarningWindow window &&
            window.UserRequestedClose;

        if (userRequested)
            _userClosed = true;

        if (ReferenceEquals(sender, _window))
            _window = null;

        if (userRequested)
            UserClosed?.Invoke(this, EventArgs.Empty);
    }
}
