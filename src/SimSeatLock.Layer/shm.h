#pragma once

#include <stdint.h>
#include <windows.h>

// Packed layout MUST match SimSeatLock.Pose Publish/PoseSharedMemory.cs
// [StructLayout(LayoutKind.Sequential, Pack = 1)]

#pragma pack(push, 1)
struct RigBlock {
    uint32_t Magic;
    uint32_t Version;
    uint32_t Sequence;
    uint32_t Flags;
    int64_t FileTimeUtc;
    float RigHz;
    uint32_t RigDropped;
    float RollDeg, PitchDeg, YawDeg;
    float SurgeM, SwayM, HeaveM;
    float Qx, Qy, Qz, Qw;
    float EyeX, EyeY, EyeZ;
    int32_t Armed;
};

struct GameBlock {
    uint32_t Magic;
    uint32_t Version;
    uint32_t Sequence;
    uint32_t Flags;
    int64_t FileTimeUtc;
    int32_t SpaceType;
    int32_t ViewCount;
    float Lpx, Lpy, Lpz, Lqx, Lqy, Lqz, Lqw;
    float Rpx, Rpy, Rpz, Rqx, Rqy, Rqz, Rqw;
};
#pragma pack(pop)

static_assert(sizeof(RigBlock) == 88, "RigBlock must stay 88 bytes (C# Pack=1)");
static_assert(sizeof(GameBlock) == 88, "GameBlock must stay 88 bytes (C# Pack=1)");

enum RigFlags : uint32_t {
    RigFlags_None = 0,
    RigFlags_RigLive = 1,
    RigFlags_SteamVrLive = 2,
    RigFlags_GameLive = 4,
    RigFlags_Armed = 8
};

constexpr uint32_t kSslMagic = 0x314C5353; // SSL1
constexpr uint32_t kSslVersion = 1;

class SharedMemory {
public:
    SharedMemory();
    ~SharedMemory();

    bool TryReadRig(RigBlock* out);
    void WriteGame(const GameBlock& block);

private:
    HANDLE gameMap_ = nullptr;
    void* gameView_ = nullptr;
    HANDLE rigMap_ = nullptr;
    void* rigView_ = nullptr;
    uint32_t gameSeq_ = 0;
    void EnsureRig();
};

int64_t FileTimeUtcNow();
