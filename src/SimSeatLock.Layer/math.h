#pragma once

#include "openxr_min.h"
#include "shm.h"

// T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
// Matches SimSeatLock.Pose PoseMath.Compensate / FromRig.

struct Rigid {
    float px, py, pz;
    float qx, qy, qz, qw;
};

Rigid IdentityRigid();
Rigid FromRig(const RigBlock& rig);
Rigid Inverse(const Rigid& t);
Rigid Compose(const Rigid& a, const Rigid& b);
XrPosef ApplyCompensate(const XrPosef& hmd, const RigBlock& rig, float eyeX, float eyeY, float eyeZ);
void LoadGeometry(float* eyeX, float* eyeY, float* eyeZ);
