using SimSeatLock.Pose.Witmotion;
using Xunit;

namespace SimSeatLock.Pose.Tests;

public class WitmotionProtocolTests
{
    [Fact]
    public void AnglePacket_90DegRoll_MatchesPublicScale()
    {
        short raw = WitmotionProtocol.AngleDegToRaw(90);
        Assert.Equal(16384, raw);
        var pkt = WitmotionProtocol.BuildPacket(WitmotionProtocol.TypeAngle, raw, 0, 0, 0);
        Assert.Equal(0x55, pkt[0]);
        Assert.Equal(0x53, pkt[1]);
        Assert.True(WitmotionProtocol.TryParse(pkt, out var parsed));
        Assert.Equal(90.0, WitmotionProtocol.RawToAngleDeg(parsed.D0), 4);
    }

    [Fact]
    public void ChecksumMismatch_IsRejected()
    {
        var pkt = WitmotionProtocol.BuildPacket(WitmotionProtocol.TypeAngle, 1, 2, 3, 4);
        pkt[10] ^= 0xFF;
        Assert.False(WitmotionProtocol.TryParse(pkt, out _));
    }

    [Fact]
    public void StreamParser_SkipsNoiseAndReadsTwoPackets()
    {
        var a = WitmotionProtocol.BuildPacket(WitmotionProtocol.TypeAngle, 100, 200, 300, 0);
        var b = WitmotionProtocol.BuildPacket(WitmotionProtocol.TypeAccel, 1, 2, 3, 4);
        var noise = new byte[] { 0x00, 0x11, 0x55, 0x00 };
        var bytes = noise.Concat(a).Concat(b).ToArray();
        var parser = new WitmotionStreamParser();
        var sink = new List<WitPacket>();
        parser.Feed(bytes, sink);
        Assert.Equal(2, sink.Count);
        Assert.Equal(WitmotionProtocol.TypeAngle, sink[0].Type);
        Assert.Equal(0, parser.DroppedChecksum);
    }

    [Fact]
    public void StreamParser_CountsChecksumAsDrop_NotValidAccel()
    {
        var goodAccel = WitmotionProtocol.BuildPacket(WitmotionProtocol.TypeAccel, 1, 2, 3, 4);
        var bad = WitmotionProtocol.BuildPacket(WitmotionProtocol.TypeAngle, 1, 2, 3, 4);
        bad[10] ^= 0xFF;
        var parser = new WitmotionStreamParser();
        var sink = new List<WitPacket>();
        parser.Feed(goodAccel.Concat(bad).ToArray(), sink);
        Assert.Single(sink);
        Assert.Equal(WitmotionProtocol.TypeAccel, sink[0].Type);
        Assert.True(parser.DroppedChecksum >= 1);
    }
}
