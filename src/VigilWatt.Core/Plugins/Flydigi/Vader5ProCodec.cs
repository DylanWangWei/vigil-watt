using VigilWatt.Core.Models;

namespace VigilWatt.Core.Plugins.Flydigi;

/// <summary>
/// 功 能：解析飞智黑武士 5 Pro 的设备信息帧。
/// 2026/11/15 10:53:17 root
/// </summary>
internal static class Vader5ProCodec
{
    private static readonly int[] Levels = [10, 25, 40, 55, 70, 85, 100];

    public static bool IsCandidate(ushort vid, ushort pid, ushort page, ushort usage,
        ushort inputLength, ushort outputLength, byte? outputReportId)
        => vid == 0x37D7 && pid == 0x2401 && page == 0xFFA0 && usage == 0x0001
           && inputLength >= 32 && outputLength >= 33
           && (outputReportId == null || outputReportId == 0);

    public static byte[] BuildQuery(int length)
    {
        if (length < 33) throw new ArgumentOutOfRangeException(nameof(length));
        var report = new byte[length];
        report[1] = 0x5A;
        report[2] = 0xA5;
        report[3] = 0x01;
        report[4] = 0x02;
        return report;
    }

    public static bool TryParse(ReadOnlySpan<byte> report, out int percent, out ChargingState charging)
    {
        percent = 0;
        charging = ChargingState.Unknown;
        int offset = report.Length > 0 && report[0] == 0x5A ? 0 : 1;
        if (report.Length < offset + 31 ||
            report[offset] != 0x5A || report[offset + 1] != 0xA5 || report[offset + 2] != 0x01 ||
            report[offset + 5] is not (0x82 or 0x90 or 0x91))
            return false;

        int firmware = (report[offset + 15] << 8) | report[offset + 16];
        if (firmware < 0x7141) return false;

        int state = report[offset + 11] >> 4;
        int level = report[offset + 11] & 0x0F;
        if (state > 2 || level >= Levels.Length) return false;

        charging = state switch
        {
            0 => ChargingState.Discharging,
            1 => ChargingState.Charging,
            _ => ChargingState.Full,
        };
        percent = state == 2 ? 100 : Levels[level];
        return true;
    }
}
