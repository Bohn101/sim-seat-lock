#pragma once

#include "openxr_min.h"
#include "shm.h"

// T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
// Matches SimSeatLock.Pose PoseMath.Compensate / FromRig.
// Named pose_math.h on purpose — a file called math.h on the include path
// shadows the CRT and makes <cmath> explode under MSVC.

struct Rigid {
    float px, py, pz;
    float qx, qy, qz, qw;
};

Rigid IdentityRigid();
Rigid FromRig(const RigBlock& rig);
Rigid Inverse(const Rigid& t);
Rigid Compose(const Rigid& a, const Rigid& b);
Rigid ApplyCompensateRigid(const Rigid& hmd, const RigBlock& rig, float eyeX, float eyeY, float eyeZ);
XrPosef ApplyCompensate(const XrPosef& hmd, const RigBlock& rig, float eyeX, float eyeY, float eyeZ);
void LoadGeometry(float* eyeX, float* eyeY, float* eyeZ);
