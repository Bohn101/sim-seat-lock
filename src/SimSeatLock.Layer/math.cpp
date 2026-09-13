#include "math.h"

#include <math.h>
#include <stdio.h>
#include <string.h>
#include <windows.h>

static void QuatMul(float ax, float ay, float az, float aw,
                    float bx, float by, float bz, float bw,
                    float* x, float* y, float* z, float* w) {
    *w = aw * bw - ax * bx - ay * by - az * bz;
    *x = aw * bx + ax * bw + ay * bz - az * by;
    *y = aw * by - ax * bz + ay * bw + az * bx;
    *z = aw * bz + ax * by - ay * bx + az * bw;
}

static void QuatConj(float x, float y, float z, float w, float* cx, float* cy, float* cz, float* cw) {
    *cx = -x;
    *cy = -y;
    *cz = -z;
    *cw = w;
}

static void QuatNorm(float* x, float* y, float* z, float* w) {
    const float n = sqrtf((*x) * (*x) + (*y) * (*y) + (*z) * (*z) + (*w) * (*w));
    if (n < 1e-12f) {
        *x = 0;
        *y = 0;
        *z = 0;
        *w = 1;
        return;
    }
    *x /= n;
    *y /= n;
    *z /= n;
    *w /= n;
}

static void Rotate(float qx, float qy, float qz, float qw, float vx, float vy, float vz,
                   float* ox, float* oy, float* oz) {
    float ix, iy, iz, iw;
    QuatMul(qx, qy, qz, qw, vx, vy, vz, 0, &ix, &iy, &iz, &iw);
    float cx, cy, cz, cw;
    QuatConj(qx, qy, qz, qw, &cx, &cy, &cz, &cw);
    float dummy;
    QuatMul(ix, iy, iz, iw, cx, cy, cz, cw, ox, oy, oz, &dummy);
}

static void EulerDegToQuat(float rollDeg, float pitchDeg, float yawDeg,
                           float* qx, float* qy, float* qz, float* qw) {
    const float r = rollDeg * 3.14159265358979323846f / 180.0f;
    const float p = pitchDeg * 3.14159265358979323846f / 180.0f;
    const float y = yawDeg * 3.14159265358979323846f / 180.0f;
    const float cr = cosf(r * 0.5f), sr = sinf(r * 0.5f);
    const float cp = cosf(p * 0.5f), sp = sinf(p * 0.5f);
    const float cy = cosf(y * 0.5f), sy = sinf(y * 0.5f);
    *qw = cr * cp * cy + sr * sp * sy;
    *qx = sr * cp * cy - cr * sp * sy;
    *qy = cr * sp * cy + sr * cp * sy;
    *qz = cr * cp * sy - sr * sp * cy;
    QuatNorm(qx, qy, qz, qw);
}

Rigid IdentityRigid() {
    return Rigid{0, 0, 0, 0, 0, 0, 1};
}

Rigid FromRig(const RigBlock& rig) {
    Rigid t{};
    EulerDegToQuat(rig.RollDeg, rig.PitchDeg, rig.YawDeg, &t.qx, &t.qy, &t.qz, &t.qw);
    t.px = rig.SwayM;
    t.py = rig.HeaveM;
    t.pz = -rig.SurgeM;
    return t;
}

Rigid Inverse(const Rigid& t) {
    Rigid o{};
    QuatConj(t.qx, t.qy, t.qz, t.qw, &o.qx, &o.qy, &o.qz, &o.qw);
    float px, py, pz;
    Rotate(o.qx, o.qy, o.qz, o.qw, t.px, t.py, t.pz, &px, &py, &pz);
    o.px = -px;
    o.py = -py;
    o.pz = -pz;
    return o;
}

Rigid Compose(const Rigid& a, const Rigid& b) {
    Rigid o{};
    QuatMul(a.qx, a.qy, a.qz, a.qw, b.qx, b.qy, b.qz, b.qw, &o.qx, &o.qy, &o.qz, &o.qw);
    QuatNorm(&o.qx, &o.qy, &o.qz, &o.qw);
    float rx, ry, rz;
    Rotate(a.qx, a.qy, a.qz, a.qw, b.px, b.py, b.pz, &rx, &ry, &rz);
    o.px = a.px + rx;
    o.py = a.py + ry;
    o.pz = a.pz + rz;
    return o;
}

XrPosef ApplyCompensate(const XrPosef& hmd, const RigBlock& rig, float eyeX, float eyeY, float eyeZ) {
    Rigid cor{eyeX, eyeY, eyeZ, 0, 0, 0, 1};
    Rigid tHmd{hmd.position.x, hmd.position.y, hmd.position.z,
               hmd.orientation.x, hmd.orientation.y, hmd.orientation.z, hmd.orientation.w};
    if (tHmd.qx == 0 && tHmd.qy == 0 && tHmd.qz == 0 && tHmd.qw == 0) tHmd.qw = 1;
    const Rigid view = Compose(cor, Compose(Inverse(FromRig(rig)), Compose(Inverse(cor), tHmd)));
    XrPosef out = hmd;
    out.position.x = view.px;
    out.position.y = view.py;
    out.position.z = view.pz;
    out.orientation.x = view.qx;
    out.orientation.y = view.qy;
    out.orientation.z = view.qz;
    out.orientation.w = view.qw;
    return out;
}

static bool ParseFloatField(const char* json, const char* key, float* out) {
    const char* p = strstr(json, key);
    if (!p) return false;
    p = strchr(p, ':');
    if (!p) return false;
    *out = strtof(p + 1, nullptr);
    return true;
}

static bool TryLoadGeometryFile(const char* path, float* eyeX, float* eyeY, float* eyeZ) {
    FILE* f = nullptr;
    if (fopen_s(&f, path, "rb") != 0 || !f) return false;
    char buf[2048]{};
    const size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    fclose(f);
    if (n == 0) return false;

    float fwd = 0, right = 0, up = 1.10f;
    const bool gotLabeled = ParseFloatField(buf, "eye_forward_m", &fwd) |
                            ParseFloatField(buf, "eye_right_m", &right) |
                            ParseFloatField(buf, "eye_up_m", &up);

    float ix = right, iy = up, iz = -fwd;
    const char* imu = strstr(buf, "imu_to_eye_m");
    if (imu) {
        ParseFloatField(imu, "\"x\"", &ix);
        ParseFloatField(imu, "\"y\"", &iy);
        ParseFloatField(imu, "\"z\"", &iz);
    } else if (gotLabeled) {
        ix = right;
        iy = up;
        iz = -fwd;
    }

    *eyeX = ix;
    *eyeY = iy;
    *eyeZ = iz;
    return true;
}

void LoadGeometry(float* eyeX, float* eyeY, float* eyeZ) {
    *eyeX = 0.f;
    *eyeY = 1.10f;
    *eyeZ = 0.f;

    char dllDir[MAX_PATH]{};
    HMODULE self = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&LoadGeometry), &self);
    GetModuleFileNameA(self, dllDir, MAX_PATH);
    char* slash = strrchr(dllDir, '\\');
    if (slash) *slash = 0;

    char user[MAX_PATH]{};
    GetEnvironmentVariableA("USERPROFILE", user, MAX_PATH);

    char paths[8][MAX_PATH]{};
    sprintf_s(paths[0], "%s\\geometry.json", dllDir);
    sprintf_s(paths[1], "%s\\config\\geometry.json", dllDir);
    sprintf_s(paths[2], "%s\\..\\..\\config\\geometry.json", dllDir);
    sprintf_s(paths[3], "%s\\sim-seat-lock\\config\\geometry.json", user);
    sprintf_s(paths[4], "%s\\sim-seat-lock\\publish\\config\\geometry.json", user);

    for (int i = 0; i < 5; ++i) {
        if (TryLoadGeometryFile(paths[i], eyeX, eyeY, eyeZ)) return;
    }
}
