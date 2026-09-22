using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Voyage.EarthquakeWarning.Models;

namespace Voyage.EarthquakeWarning.Services;

public sealed partial class EewService : BackgroundService
{
    private const string ApiUrl = "wss://api.odysphere.tech/cea";
    private const string TokenMissingText = "未连接（未填写 API Token）";

    private readonly WarningEngine _engine;

    public EewService(WarningEngine engine)
    {
        _engine = engine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var token =
                Plugin.Current!.Settings.ApiToken?.Trim() ?? "";

            if (token.Length == 0)
            {
                Plugin.Current!.Settings.ApiConnectionTimeText =
                    TokenMissingText;

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(3),
                        stoppingToken);
                }
                catch
                {
                }

                continue;
            }

            try
            {
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(ApiUrl), stoppingToken);
                Plugin.Current!.Settings.ApiConnectionTimeText = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss");

                await ws.SendAsync(
                    Encoding.UTF8.GetBytes(token),
                    WebSocketMessageType.Text,
                    true,
                    stoppingToken);

                await ReceiveLoopAsync(ws, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Plugin.Current!.Settings.ApiConnectionTimeText = $"连接失败：{ex.Message}";
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch { }
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buffer = new byte[32 * 1024];
        using var ms = new MemoryStream();
        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(buffer, token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Plugin.Current!.Settings.ApiConnectionTimeText =
                    string.IsNullOrWhiteSpace(result.CloseStatusDescription)
                        ? $"连接已被服务端关闭（{result.CloseStatus}）"
                        : $"连接已被服务端关闭：{result.CloseStatusDescription}";

                break;
            }

            if (result.MessageType != WebSocketMessageType.Text) continue;

            ms.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var json = Encoding.UTF8.GetString(ms.ToArray());
            ms.SetLength(0);

            EewEnvelope? envelope;

            try
            {
                envelope = JsonSerializer.Deserialize<EewEnvelope>(json);
            }
            catch (JsonException ex)
            {
                Plugin.Current!.Settings.ApiRecentDataText =
                    $"数据解析失败：{ex.Message}";

                continue;
            }

            if (envelope?.Data is null)
                continue;

            var d = envelope.Data;
            var depth = d.Depth ?? 0;

            // 最近数据始终为服务端推送的最新一条预警
            Plugin.Current!.Settings.ApiRecentDataText =
                $"中国地震预警网第{d.Updates}报，{d.ShockTime}在{d.PlaceName}附近({d.Latitude:0.###},{d.Longitude:0.###})正在发生{d.Magnitude}级地震，震源深度{depth:0.#}km，预估最大烈度{d.EpiIntensity:0.#}";

            try
            {
                await _engine.ProcessAsync(d, false, token);
            }
            catch
            {
            }
        }
    }
}
