using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Models.Notification;
using Voyage.EarthquakeWarning.Models;

namespace Voyage.EarthquakeWarning.Services;

[NotificationProviderInfo(
    "D39B2CB3-81A1-44E5-8CE8-7D8F6415E32A",
    "地震预警",
    "在收到预警信息时，根据用户设置弹出地震预警")]
public sealed class EewNotificationProvider : NotificationProviderBase
{
    public static EewNotificationProvider? Instance { get; private set; }

    private const double BannerSidePadding = 32;

    private TextBlock? _countdownText;

    public EewNotificationProvider()
    {
        Instance = this;
    }

    public static void Push(WarningState state, bool firstReport, bool overrideSettings)
    {
        Instance?.PushInternal(state, firstReport, overrideSettings);
    }

    public static void UpdateCountdown(WarningState state)
    {
        Instance?.UpdateCountdownInternal(state);
    }

    private void UpdateCountdownInternal(WarningState state)
    {
        var text = state.Arrived
            ? "地震横波已到达"
            : $"倒计时：{state.CountdownText}";

        Dispatcher.UIThread.Post(() =>
        {
            if (_countdownText is not null)
                _countdownText.Text = text;
        });
    }

    private void PushInternal(WarningState state, bool firstReport, bool overrideSettings)
    {
        var color = Color.Parse(state.BackgroundHex);
        var foreground = new SolidColorBrush(Color.Parse(state.ForegroundHex));
        var accent = new SolidColorBrush(Color.Parse(TierAccentHex(state.Tier)));

        var mask = new NotificationContent
        {
            Content = new Border
            {
                Padding = new Avalonia.Thickness(BannerSidePadding, 0, BannerSidePadding, 0),
                Child = new TextBlock
                {
                    Text = "地震预警",
                    FontSize = 38,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    Foreground = accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            },
            Duration = TimeSpan.FromSeconds(1.2),
            Color = new SolidColorBrush(color)
        };

        var line = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Avalonia.Thickness(BannerSidePadding, 0, BannerSidePadding, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        void AddPlain(string text, IBrush brush)
        {
            line.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 24,
                Foreground = brush,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        AddPlain("地震预警", accent);
        AddPlain($"发震时间：{state.ShockTimeText}", foreground);
        AddPlain($"震中：{state.PlaceName}", foreground);
        AddPlain($"震源深度：{state.DepthKm:0.#}km", foreground);
        AddPlain("预估本地烈度：", accent);

        line.Children.Add(new TextBlock
        {
            Text = state.LocalIntensityText,
            FontSize = 26,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center
        });

        _countdownText = new TextBlock
        {
            Text = state.Arrived
                ? "地震横波已到达"
                : $"倒计时：{state.CountdownText}",
            FontSize = 24,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center
        };

        line.Children.Add(_countdownText);

        var durationSeconds = Math.Max(
            8,
            Math.Max(0, state.CountdownSeconds) +
            Plugin.Current!.Settings.ReleaseSeconds +
            2);

        var overlay = new NotificationContent
        {
            Content = line,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            Color = new SolidColorBrush(color)
        };

        var request = new ClassIsland.Core.Models.Notification.NotificationRequest
        {
            MaskContent = mask,
            OverlayContent = overlay,
            RequestNotificationSettings = new NotificationSettings
            {
                IsSettingsEnabled = false,
                IsNotificationEnabled = true,
                IsNotificationEffectEnabled =
                    overrideSettings &&
                    firstReport &&
                    state.LocalIntensity >= 3,
                IsNotificationSoundEnabled = false,
                IsNotificationTopmostEnabled =
                    overrideSettings &&
                    Plugin.Current!.Settings.TopMost,
                IsSpeechEnabled = false
            }
        };

        ShowNotification(request);
    }

    public static void ShowStrongEffect(WarningState state)
    {
        Instance?.ShowStrongEffectInternal(state);
    }

    private void ShowStrongEffectInternal(WarningState state)
    {
        var color = Color.Parse(state.BackgroundHex);
        var accent = new SolidColorBrush(Color.Parse(TierAccentHex(state.Tier)));

        var request = new ClassIsland.Core.Models.Notification.NotificationRequest
        {
            MaskContent = new NotificationContent
            {
                Content = new Border
                {
                    Padding = new Avalonia.Thickness(BannerSidePadding, 0, BannerSidePadding, 0),
                    Child = new TextBlock
                    {
                        Text = "地震预警",
                        FontSize = 38,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = accent,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                },
                Duration = TimeSpan.FromSeconds(1.2),
                Color = new SolidColorBrush(color)
            },
            OverlayContent = NotificationContent.Empty,
            RequestNotificationSettings = new NotificationSettings
            {
                IsSettingsEnabled = false,
                IsNotificationEnabled = true,
                IsNotificationEffectEnabled = true,
                IsNotificationSoundEnabled = false,
                IsNotificationTopmostEnabled =
                    Plugin.Current!.Settings.TopMost,
                IsSpeechEnabled = false
            }
        };

        ShowNotification(request);
    }

    private static string TierAccentHex(WarningTier tier) => tier switch
    {
        WarningTier.BlueNoFeel or WarningTier.BlueFeel => "#3764FF",
        WarningTier.Yellow => "#FAE600",
        WarningTier.Orange => "#F09614",
        _ => "#DC2828"
    };
}
