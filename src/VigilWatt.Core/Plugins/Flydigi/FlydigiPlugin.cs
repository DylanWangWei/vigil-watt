using VigilWatt.Core.Hid;
using VigilWatt.Core.Models;
using VigilWatt.Core.Utils;

namespace VigilWatt.Core.Plugins.Flydigi;

/// <summary>
/// 功 能：读取飞智黑武士 5 Pro 手柄电量。
/// 2026/11/15 10:53:17 root
/// </summary>
public sealed class FlydigiPlugin : BatteryPluginBase
{
    private const string ProtocolName = "flydigi-vader5pro-get-info";

    public override string Name => "Flydigi";
    public override string DisplayName => "飞智";
    public override string DisplayNameEn => "FLYDIGI";
    public override DeviceKind Kind => DeviceKind.Gamepad;
    public override bool SupportsFastDiscovery => false;
    protected override TimeSpan ProbeCooldown => TimeSpan.FromSeconds(30);
    protected override bool ProbeWrites => true;
    protected override OfflinePolicy Offline { get; } = new()
    {
        Silence = TimeSpan.Zero,
        CountPollFailures = true,
    };

    protected override bool IsCandidate(HidDeviceInfo info)
        => Vader5ProCodec.IsCandidate(info.VendorId, info.ProductId, info.UsagePage, info.Usage,
            info.InputReportLength, info.OutputReportLength, info.OutputReportId);

    protected override string BuildDisplayName(HidDeviceInfo info) => "飞智 黑武士 5 Pro";

    protected override (string Protocol, int? Percent, ChargingState Charging)? Probe(HidDeviceHandle handle)
        => ProbeAsync(handle).GetAwaiter().GetResult();

    protected override async Task<(string Protocol, int? Percent, ChargingState Charging)?> ProbeAsync(
        HidDeviceHandle handle)
    {
        var result = await QueryAsync(handle).ConfigureAwait(false);
        if (!result.WriteSucceeded || result.Percent is not { } percent) return null;
        return (ProtocolName, percent, result.Charging);
    }

    protected override (int? Percent, ChargingState Charging)? PollDevice(TrackedDevice dev)
    {
        if (!CanWrite(dev)) return null;
        var result = QueryAsync(dev.Handle).GetAwaiter().GetResult();
        if (!result.WriteSucceeded)
        {
            MarkHandleStale(dev);
            return null;
        }
        if (result.Percent is not { } percent) return null;

        ReportPollAnswered(dev);
        return (percent, result.Charging);
    }

    private readonly record struct QueryResult(bool WriteSucceeded, int? Percent, ChargingState Charging);

    private static async Task<QueryResult> QueryAsync(HidDeviceHandle handle)
    {
        if (!handle.WriteOutput(Vader5ProCodec.BuildQuery(handle.Info.OutputReportLength), TimeSpan.FromMilliseconds(500)))
        {
            Log.Warn($"飞智 GET_INFO 写入失败: {handle.Info.DevicePath}");
            return new QueryResult(false, null, ChargingState.Unknown);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        int frames = 0;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            var input = await handle.ReadInputAsync(remaining).ConfigureAwait(false);
            if (input == null) continue;
            frames++;
            if (Vader5ProCodec.TryParse(input, out int percent, out var charging))
                return new QueryResult(true, percent, charging);
        }

        Log.Info($"飞智 GET_INFO 未收到有效响应（收到 {frames} 帧）: {handle.Info.DevicePath}");
        return new QueryResult(true, null, ChargingState.Unknown);
    }
}
