using SimSeatLock.Pose;
using Xunit;

namespace SimSeatLock.Pose.Tests;

public class PoseMathTests
{
    [Fact]
    public void PreviewOff_LeavesHeadsetUntouched()
    {
        var hmd = new RigidPose { Py = 1.1, Qw = 1, Valid = true, Live = true, Space = "test" };
        var rig = new PoseSample { PitchDeg = 8, Valid = true };
        var view = PoseMath.Compensate(hmd, rig, 0, 1.1, 0, armed: false);
        Assert.Equal(hmd.Py, view.Py);
        Assert.Equal(hmd.Qw, view.Qw);
    }

    [Fact]
    public void IdentityRig_LeavesHeadset()
    {
        var hmd = new RigidPose { Px = 0.03, Py = 1.1, Qw = 1, Valid = true, Live = true, Space = "test" };
        var view = PoseMath.Compensate(hmd, PoseSample.Zero, 0, 1.1, 0, armed: true);
        Assert.InRange(view.Px, 0.029, 0.031);
        Assert.InRange(view.Qw, 0.999, 1.001);
    }
}
