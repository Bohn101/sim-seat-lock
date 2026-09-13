#include "shm.h"

#include <string.h>

namespace {
constexpr wchar_t kRigName[] = L"Local\\SimSeatLock.Rig.v1";
constexpr wchar_t kGameName[] = L"Local\\SimSeatLock.Game.v1";
}

int64_t FileTimeUtcNow() {
    FILETIME ft{};
    GetSystemTimeAsFileTime(&ft);
    ULARGE_INTEGER u{};
    u.LowPart = ft.dwLowDateTime;
    u.HighPart = ft.dwHighDateTime;
    return static_cast<int64_t>(u.QuadPart);
}

SharedMemory::SharedMemory() {
    gameMap_ = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0,
                                  static_cast<DWORD>(sizeof(GameBlock)), kGameName);
    if (gameMap_) {
        gameView_ = MapViewOfFile(gameMap_, FILE_MAP_WRITE, 0, 0, sizeof(GameBlock));
    }
}

SharedMemory::~SharedMemory() {
    if (gameView_) UnmapViewOfFile(gameView_);
    if (gameMap_) CloseHandle(gameMap_);
    if (rigView_) UnmapViewOfFile(rigView_);
    if (rigMap_) CloseHandle(rigMap_);
}

void SharedMemory::EnsureRig() {
    if (rigView_) return;
    if (rigMap_) {
        CloseHandle(rigMap_);
        rigMap_ = nullptr;
    }
    rigMap_ = OpenFileMappingW(FILE_MAP_READ, FALSE, kRigName);
    if (!rigMap_) return;
    rigView_ = MapViewOfFile(rigMap_, FILE_MAP_READ, 0, 0, sizeof(RigBlock));
}

bool SharedMemory::TryReadRig(RigBlock* out) {
    EnsureRig();
    if (!rigView_) return false;
    RigBlock block{};
    memcpy(&block, rigView_, sizeof(block));
    if (block.Magic != kSslMagic || block.Version != kSslVersion) return false;
    *out = block;
    return true;
}

void SharedMemory::WriteGame(const GameBlock& block) {
    if (!gameView_) return;
    GameBlock b = block;
    b.Magic = kSslMagic;
    b.Version = kSslVersion;
    b.Sequence = ++gameSeq_;
    b.FileTimeUtc = FileTimeUtcNow();
    memcpy(gameView_, &b, sizeof(b));
}
