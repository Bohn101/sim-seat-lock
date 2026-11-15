# Bohnster SimSeatLock: first-revision change request (0.2.5)

Started Nov 15, 2026 (MT) by Steven, kept by Apex. Collect only: nothing here is approved to build yet.

Version for this revision is a last-segment bump from the current Pose release, which is **v0.2.4** (`Program.Version` on `main` at `5b7c81c`). That makes this revision **0.2.5**. Items are numbered R1, R2, and so on (R = this revision). On Bohnster Torque the same kind of list used an N prefix, which only meant "next cycle"; the idea is the same.

A chart of the One-Euro idea in R5 is at [`docs/handoff/img/one_euro_demo.png`](img/one_euro_demo.png).

## Ground rules

- Start from current `main` (`5b7c81c` or later). Do not change product code until Steven approves this request.
- Bump the last version segment only: 0.2.4 becomes 0.2.5. Put that string in every place a version is shown (see R11).
- New settings need on-screen controls. Steven should not have to edit JSON or use a terminal for day-to-day use.
- Defaults for the new smoother and for logging are **pending Steven's decision** (on or off). Until he decides, treat them as undecided, not as a silent "on".
- Rebrand is display names, the window title, and the theme only. Keep exe, dll, OpenXR layer manifest, registry value, and `openvr_api.dll` proxy names unchanged (R7).
- The Pose app stays C# / .NET 8. The OpenXR layer and OpenVR proxies stay C++. Python is for offline analysis and plots, not for the live pose process (see the language section).
- Injectors must not write files from a frame callback. Pose must not publish a half-written block. Safety fallback is in R6.
- Credit design review notes below as **Byte (design review)**. Nobel's research input is still pending; there is a placeholder at the end.

## R1. Combined-style per-frame logging

**Today.** There is no Combined-style log. Pose shows live text boxes. The OpenXR layer and OpenVR proxies append to `layer.log` / `simseatlock-openvr.log` with `fopen` on some early hook calls. Shared-memory stamps use `DateTime.UtcNow` / `GetSystemTimeAsFileTime`, not a common high-resolution clock. `Game.v1` stores only the post-compensation pose: no raw HMD pose, no predicted display time, no frame index.

**Want.** One timestamped row per frame (or per IMU sample, with injector rows joined by sequence / frame index), on a single QPC clock, in the same spirit as Bohnster Torque's Combined log. Readable column names, plus a sidecar JSON with version, geometry, gains, filter settings, title, and start time. Auto-start and auto-name like BT (session, car/track if known, otherwise title + local time). Optional coupling with game telemetry, starting with LMU shared memory, so a motion cue can be lined up with the correction that followed it.

**Columns (minimum).**

- Clock: QPC ticks and seconds, UTC wall time for humans only.
- IMU raw: arrival QPC, device sample time if the IMU provides it, device sample counter, angle Hz, accel, gyro if present, checksum-drop count, COM status.
- Computed `T_rig`: roll, pitch, yaw (deg), surge, sway, heave (m), quaternion, home, gains, invert, armed flag, publish sequence, publish QPC.
- SteamVR HMD pose: position, quaternion, space (seated/standing), valid/live, QPC of the read. For any OpenVR predicted pose, also log `fPredictedSecondsToPhotonsFromNow`.
- Injected / adjusted pose from the OpenXR layer and from the OpenVR proxy: raw `T_hmd` before compensation and `T_view` after, predicted display time (`xrWaitFrame` `predictedDisplayTime` on OpenXR; compositor frame timing plus the prediction interval on OpenVR), frame index (so dropped or reused poses show), injector-read QPC, and the Rig sequence that was applied.
- Filter: mode (bypass / One-Euro / other), live cutoff Hz, A/B variant, and a switch marker when the variant changes.
- Safety: clamp or fallback reason, IMU age (ms).
- Optional LMU (or other title) telemetry: speed, yaw, and whatever is needed to join a driving log later.

**How it is written (Byte, design review).** Injectors never write files in a frame callback. They push a small record into a lock-free shared-memory ring buffer. A separate writer thread in the Pose process drains that ring and the Pose-side samples, and writes one Combined CSV. Extend or version the shm layout so raw HMD, injected pose, predicted display time, and frame index all travel with the record. QPC is used everywhere, including IMU stamp-at-read (see also R8 and R9).

## R2. Latency measurement

**Today.** There is no measured latency budget. The publish loop aims at about 4 ms (`Environment.TickCount64` + `Thread.Sleep`), IMU samples are stamped with `DateTime.UtcNow` at parse time, and the desktop SteamVR reader calls `GetDeviceToAbsoluteTrackingPose` with a prediction of 0 s.

**Want.** From the Combined log, report each software stage, and a method for a total motion-to-photon estimate.

**Stages we can measure from the log (same QPC clock):**

1. IMU sample to Pose process: device sample time (if present) vs QPC at serial read, vs QPC when that sample becomes `T_rig` and is written to `Rig.v1`. This splits serial buffering delay from IMU rate jitter (Byte, design review).
2. Pose to injector read: `Rig.v1` write QPC / sequence vs the QPC when the layer or proxy reads that same sequence.
3. Injector to predicted display: injector-apply QPC vs OpenXR `predictedDisplayTime`, or vs OpenVR `fPredictedSecondsToPhotonsFromNow` plus compositor frame timing.

**Total motion-to-photon.** Sum of the software stages above, plus the compositor's own prediction, is the software estimate. It is not the full truth. The following need an external measurement, and should be called out in the report rather than guessed:

- IMU internal fusion delay beyond what the device clock shows.
- Platform mechanical lag (actuator command to actual motion). That gets easier once the planned command-feed source exists; it is not in this revision.
- Actual photon time vs the runtime's predicted display time (photodiode or high-speed camera on a flashed marker, synced to the same clock).

The first shipped analysis should print each stage's median / p95 / max, and show a gap clearly when a field could not be measured.

## R3. Smoothness metrics and analysis report

**Want.** An offline script (Python is the right tool here) and a short report over a Combined log:

- Correction velocity, acceleration, and jerk (deg/s, deg/s², deg/s³, and the translation equivalents).
- Jitter: frame-to-frame pose noise when the rig is still, and variation in IMU / injector inter-arrival times.
- Frame-to-frame mismatch: disagreement between the headset's predicted pose and the injected pose, including one-frame stutters and reused poses (frame index does not advance, or the same Rig sequence is applied twice).
- Dropped or late samples: checksum drops, IMU gaps, injector frames that never received a fresh Rig block, ring-buffer overruns.

Output: a one-page summary (medians and tails) plus plots. This is the instrument for R4, and for A/B of the smoother in R5.

## R4. Nausea-source investigation plan

Steven's report: with compensation armed, VR feels a little less smooth and somewhat nauseating; with it disarmed, being moved through the cockpit is much worse. Do not add a heavier filter until the logs say what is wrong.

**Hypotheses to test, in order:**

1. **IMU noise or uneven timing.** Angle noise, COM bursts, 4 ms "no bytes" waits, or publish-loop jitter that becomes visible in `T_view`. Look at still-stand correction jerk and IMU inter-arrival histograms.
2. **Prediction / frame mismatch.** Compensation applied to a pose predicted for a different photon time than the one just displayed (a one-frame stutter). Look at injected pose vs predicted display time, reused frame indices, and SteamVR / compositor timing.
3. **Yaw or translation leakage.** Yaw gain defaults to 0, and translation gains default to 0, but invert / swap / Euler packing can still leak a yaw or sway component into the view. Look at residual yaw and lateral translation in the delta channel during pure pitch/roll moves.
4. **CoR / geometry error.** Eyes are 270 mm aft and 1150 mm up of the IMU (`geometry.json`). A CoR error shows up as a translation of the eyepoint during a pure rotation, growing with angle. Log the drag-at-eye term already computed on the Live tab, and compare a garage pitch/roll jog against that prediction.

The first investigation is a measured session (compensation on vs off, same motion, Combined log + R3 report). Recommend a filter or a geometry fix only after one of the four is identified. If more than one is present, say so.

## R5. Adaptive low-latency smoother (One-Euro plus prediction)

**Want.** A live, low-lag smoother on `T_rig` (and therefore on the injected `T_view`), with on-screen tunables, a bypass, and a hot-switchable A/B that writes switch markers into the Combined log.

**One-Euro (Casiez et al., CHI 2012).** A 1-Euro filter is a first-order low-pass whose cutoff rises with how fast the signal is moving: `cutoff = min_cutoff + beta * |filtered velocity|`. `min_cutoff` is the cutoff when still (lower is smoother, and a bit more lag). `beta` is how quickly the cutoff climbs with speed. `d_cutoff` is the cutoff used when estimating that speed (typically 1 Hz). The demo at [`docs/handoff/img/one_euro_demo.png`](img/one_euro_demo.png) uses `min_cutoff = 1.0 Hz`, `beta = 0.07`, `d_cutoff = 1.0 Hz` on a 250 Hz noisy rotation signal. In that sim, a fixed 2 Hz low-pass and One-Euro both cut still-stand noise by about 85-86% (0.188 deg RMS down to 0.028 / 0.027 deg), but the fixed low-pass lagged the kerb jolt by 36 ms at the peak (56 ms at the 50% rise), while One-Euro lagged it by 12 ms (15 ms at the 50% rise).

**Prediction (Byte, design review).** One-Euro alone still adds some lag. Pair it with a short prediction to the predicted display time: constant velocity, or a light constant-acceleration model with a clamp so a noise spike cannot throw the pose. Log the filter's internal cutoff every frame (already listed in R1).

**Controls.** Live knobs for `min_cutoff`, `beta`, `d_cutoff`, prediction on/off, and bypass (identity / raw `T_rig`). A/B can switch mid-session without restarting. Defaults pending Steven (ground rules).

**Alternatives to keep in mind, not required to ship together:** a complementary filter that uses the Witmotion gyro (R9), or a slightly heavier predictor if the logs in R4 say the remaining lag is the problem.

## R6. Safety: stale IMU, correction clamps, tear-free shared memory

**Today (from the code review).** The layer and the OpenVR proxy apply compensation whenever `Rig.v1` is readable and `Armed != 0`. They do not check IMU age or `Valid`. If the COM port drops, Pose marks the sample invalid, but a last pose can still be published while armed, and the injector will keep applying it. `Rig.v1` is a single packed block with a sequence field; the injector `memcpy`s the whole block, so a reader can in principle see a torn write. The Game map can also stay "live" after the title has stopped writing (R10).

**Want (Byte, design review):**

- Clamp correction size and rate so a glitch cannot throw the eyepoint.
- If the IMU sample is older than about 20 ms, or the COM port drops, fall back to passthrough or hold, and log the reason.
- Injectors must never apply a half-written pose: write the sequence counter last (with a barrier), or use a double buffer, and have the reader reject a torn or stale sequence.
- The same freshness rule applies on the Game heartbeat used for the Layer lamp (R10).

These are safety items, not tunables hidden in a file. Show fallback state on the UI.

## R7. Rebrand to Bohnster SimSeatLock, black and red glow theme

**Want.** Rename the product **Bohnster SimSeatLock**, by Bohnster Innovations. Match the black-plus-red-glow look Byte is designing for Bohnster Torque (BT's new UI is PySide6 / Qt). The Pose app should feel like the same family: same palette, card styling, and glow on active nav and primary buttons only (Byte, design review).

**Implementation.** Pose is WinForms today (`SystemColors.Control`, Segoe UI). Do not port it to Python to reuse the Qt code. Restyle natively in **WPF or Avalonia** so the look matches. Window title, Live / Config / Help chrome, lamps, and help text all use the new name.

**Do not rename (Byte, design review).** `SimSeatLock.Pose.exe`, the layer dll, `XR_APILAYER_NOVENDOR_sim_seat_lock` and its registry value, and the `openvr_api.dll` proxy name. Renaming those breaks existing installs. Any later file-name migration is a separate, planned step with a shimed install path.

## R8. Pose hot-path hardening

**Today.** The publish loop is a UI-owned thread sleeping on `Environment.TickCount64` toward a 4 ms period. IMU, SteamVR, and Game each run `Task.Run` loops with 4-8 ms waits. The SteamVR poll `AllocHGlobal`s every read. Stamps are `DateTime.UtcNow`. There is no `GCSettings.LatencyMode` setting, and no high-resolution wait.

**Want (Byte, design review):**

- QPC for every latency-relevant stamp (R1, R2).
- No allocations on the publish / IMU / injector-facing path (structs, `Span`, `ArrayPool`).
- `GCSettings.LatencyMode = SustainedLowLatency`.
- A dedicated high-priority publish thread, with `timeBeginPeriod(1)` or a waitable high-resolution timer, still targeting ≥250 Hz into `Rig.v1`.
- Keep using `MemoryMappedFile` for the injector path.

This is what keeps a UI redraw or a GC pause from becoming visible judder in the headset.

## R9. Use unused Witmotion time, gyro, and quaternion packets

**Today.** The parser already accepts the standard 0x55 types, including time (0x50), gyro (0x52), and quaternion (0x59). Only angle (0x53) updates `T_rig`, and only accel is stored on the side. `DroppedChecksum` is correct (checksum/sync only). Angle samples are stamped with `DateTime.UtcNow`, not a device clock. There is no device sample counter in the log, so serial buffering and IMU rate jitter cannot be separated (Byte, design review).

**Want.** Parse and log TypeTime and a monotonic device sample counter alongside the QPC arrival time. Log gyro (and use it in the R5 predictor / complementary option). Optionally prefer the device quaternion when it is present, instead of packing Euler angles. Do not integrate accelerometer packets into position (existing rule).

## R10. Engine detection and Game heartbeat freshness

**Today, from the review:**

- Assetto Corsa Competizione is a supported OpenVR path (`IVRCompositor_022`), but `ACC` is not in `pose.json` `game_processes`, so the Engine lamp will not light for it.
- `GamePoseSource` treats any readable `Game.v1` block as live. After the title exits, the last snapshot can sit in the map and the Layer lamp can stay on, the same class of bug as Bohnster Torque's auto-stop-on-close issue.
- iRacing is listed and classified as `openvr`, but is still unproven. AC2 is also not classified yet (README "Next").

**Want.** Add ACC (and any other already-supported exe names) to the default process list. Treat the Game heartbeat as live only while its sequence advances inside a short freshness window, and show a clear "stale" state otherwise. Keep unproven titles labelled as unproven, including iRacing, until a measured inject path exists.

## R11. Unify version strings

**Today.** `Program.Version` is `0.2.4`, `SimSeatLock.Pose.csproj` and `app.manifest` still say `0.2.1`, and `HelpText.cs` says `v0.2.2`. README and `docs/RESEARCH.md` already say v0.2.4.

**Want.** This revision ships as 0.2.5 in all of those places, plus the window title, help text, and changelog. No leftover 0.2.1 or 0.2.2 strings.

## Language: C# / .NET 8 vs Python for the Pose app

**Recommendation (Byte, design review): keep the Pose app in C# / .NET 8. Do not port it to Python.**

Reasons:

- The live path is a 250+ Hz serial read, pose math, and shared-memory write, concurrent with UI and logging. CPython's GIL shares that work, so a UI redraw or a GC pause becomes injector jitter. In VR, a pose error is visible judder. Bohnster Torque can tolerate a small wobble because the wheel path is filtered; a headset is not.
- .NET rules for this codebase: no hot-path allocations (structs, `Span`, `ArrayPool`), `GCSettings.LatencyMode = SustainedLowLatency`, a dedicated high-priority thread with `timeBeginPeriod(1)` or a waitable high-resolution timer, and `MemoryMappedFile` to the injectors (R8).
- Python loses mainly on interop quality (`ctypes` vs P/Invoke), PyInstaller antivirus false positives, and slower startup versus a single-file .NET publish.
- The OpenXR layer and OpenVR proxies stay C++ either way, so a Python Pose app would not remove the C++ work.
- UI: match BT's new black-and-red glow visually in WPF or Avalonia (R7). There is no need to move Pose to PySide6 to do that.
- Testability is fine in C#: xUnit, plus a recorded-IMU replay harness. Python is not a win here.
- Port effort is about 1-2 weeks, plus latency risk, with no user-visible gain.

Python is the right tool for the offline analysis script, plots, and the latency / smoothness report in R3 (and for the One-Euro explainer chart). That is the split.

### Advantages of staying on C# / .NET 8

Native Win32 and OpenVR P/Invoke, low-latency GC modes, single-file `dotnet publish`, a UI that can match BT without a language change, and a codebase already in production at ≥250 Hz.

### Disadvantages / what Python would add or take away

Python would make it easier to share plot and analysis code with the research scripts, and to iterate on UI if BT's PySide6 widgets were copied. It would not add native capabilities the Pose app is missing, and it would reduce control over pause-free timing and interop. Injector capability is unchanged, because those stay C++.

**Nobel (research): pending.** Nobel's view on the language split, and any research notes on the nausea / smoothness questions, will be added here when they arrive.

## Open questions (Steven)

- Should the new smoother and the new logging default on, or default off, when 0.2.5 is first installed?
- Are you happy with the product name **Bohnster SimSeatLock** (by Bohnster Innovations)?
- WPF vs Avalonia for the restyle: team can recommend with a short mock, unless you already have a preference.

## Already on the roadmap, not this revision

Predictive `T_rig` from the motion-command stream, with an IMU residual (`docs/ARCHITECTURE.md` v1, `IPoseSource` "Source B"). That remains the next big pose-source step after this revision's logging, measurement, smoother, safety, and branding work. `SimSeatLock.Calib` (extent and plant ID) stays with that later step.
