using VigilWatt.Core.Models;
using VigilWatt.Core.Plugins.Flydigi;
using Xunit;

namespace VigilWatt.Core.Tests;

/// <summary>
/// 功 能：验证飞智黑武士 5 Pro 的查询及响应解析。
/// 2026/11/15 10:53:17 root
/// </summary>
public class Vader5ProCodecTests
{
    private static byte[] Reply(byte battery, bool prefixed = true)
    {
        var frame = new byte[prefixed ? 33 : 32];
        int i = prefixed ? 1 : 0;
        frame[i] = 0x5A;
        frame[i + 1] = 0xA5;
        frame[i + 2] = 0x01;
        frame[i + 5] = 0x82;
        frame[i + 6] = 2;
        frame[i + 11] = battery;
        frame[i + 15] = 0x71;
        frame[i + 16] = 0x41;
        return frame;
    }

    [Fact]
    public void QueryUsesUnnumberedOutputReport()
    {
        byte[] expected = new byte[33];
        expected[1] = 0x5A;
        expected[2] = 0xA5;
        expected[3] = 0x01;
        expected[4] = 0x02;
        Assert.Equal(expected, Vader5ProCodec.BuildQuery(33));
        Assert.Equal(expected.Concat(new byte[31]), Vader5ProCodec.BuildQuery(64));
        Assert.Throws<ArgumentOutOfRangeException>(() => Vader5ProCodec.BuildQuery(32));
    }

    [Theory]
    [InlineData(0x00, 10, ChargingState.Discharging)]
    [InlineData(0x03, 55, ChargingState.Discharging)]
    [InlineData(0x06, 100, ChargingState.Discharging)]
    [InlineData(0x14, 70, ChargingState.Charging)]
    [InlineData(0x20, 100, ChargingState.Full)]
    public void ParsesBatteryBands(byte raw, int expected, ChargingState state)
    {
        Assert.True(Vader5ProCodec.TryParse(Reply(raw), out int percent, out var charging));
        Assert.Equal(expected, percent);
        Assert.Equal(state, charging);
    }

    [Fact]
    public void SupportsUnprefixedAndSpecialEditionReplies()
    {
        var frame = Reply(0x05, false);
        frame[5] = 0x90;
        Assert.True(Vader5ProCodec.TryParse(frame, out int percent, out _));
        Assert.Equal(85, percent);
    }

    [Fact]
    public void IgnoresInputReportIdWhenReplyHasHidPrefix()
    {
        var frame = Reply(0x03);
        frame[0] = 0x7F;
        Assert.True(Vader5ProCodec.TryParse(frame, out int percent, out _));
        Assert.Equal(55, percent);
    }

    [Fact]
    public void RejectsUnrecognizedOrIncompleteReplies()
    {
        var frame = Reply(0x03);
        Assert.False(Vader5ProCodec.TryParse(frame.AsSpan(0, 20), out _, out _));
        frame[6] = 0x83;
        Assert.False(Vader5ProCodec.TryParse(frame, out _, out _));
        frame = Reply(0x03);
        frame[16] = 0x70;
        Assert.False(Vader5ProCodec.TryParse(frame, out _, out _));
        Assert.False(Vader5ProCodec.TryParse(Reply(0x07), out _, out _));
        Assert.False(Vader5ProCodec.TryParse(Reply(0x30), out _, out _));
        frame = Reply(0x03);
        frame[3] = 0x02;
        Assert.False(Vader5ProCodec.TryParse(frame, out _, out _));
    }

    [Fact]
    public void OnlyClaimsKnownVendorCollection()
    {
        Assert.True(Vader5ProCodec.IsCandidate(0x37D7, 0x2401, 0xFFA0, 1, 33, 33, 0));
        Assert.False(Vader5ProCodec.IsCandidate(0x37D7, 0x6001, 0xFFA0, 1, 33, 33, 0));
        Assert.False(Vader5ProCodec.IsCandidate(0x37D7, 0x2401, 0x0001, 1, 33, 33, 0));
        Assert.False(Vader5ProCodec.IsCandidate(0x37D7, 0x2401, 0xFFA0, 2, 33, 33, 0));
        Assert.False(Vader5ProCodec.IsCandidate(0x37D7, 0x2401, 0xFFA0, 1, 33, 32, 0));
        Assert.False(Vader5ProCodec.IsCandidate(0x37D7, 0x2401, 0xFFA0, 1, 33, 33, 3));
    }
}
