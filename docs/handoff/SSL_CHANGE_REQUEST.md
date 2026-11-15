# Bohnster SimSeatLock: first-revision change request (0.2.5)

Started Nov 15, 2026 (MT) by Steven, kept by Apex. Collect only: nothing here is approved to build yet. Updated the same day with Nobel (research).

Version for this revision is a last-segment bump from the current Pose release, which is **v0.2.4** (`Program.Version` on `main` at `5b7c81c`). That makes this revision **0.2.5**. Items are numbered R1, R2, and so on (R = this revision). On Bohnster Torque the same kind of list used an N prefix, which only meant "next cycle"; the idea is the same.

A chart of the One-Euro idea in R5 is at [`docs/handoff/img/one_euro_demo.png`](img/one_euro_demo.png). Nobel's full research note is at [`docs/handoff/research/NOBEL_motion_comp_research_20261115.md`](research/NOBEL_motion_comp_research_20261115.md). Claim tags in that note, and in the research-backed lines below, are **[code]** (read in the repo), **[source]** (stated on a page Nobel opened), and **[inference]** (reasoning or arithmetic, verify before acting).

## Ground rules

- Start from current `main` (`5b7c81c` or later). Do not change product code until Steven approves this request.
- Bump the last version segment only: 0.2.4 becomes 0.2.5. Put that string in every place a version is shown (see R11).
- New settings need on-screen controls. Steven should not have to edit JSON or use a terminal for day-to-day use.
- Defaults for the new smoother and for logging are **pending Steven's decision** (on or off). Until he decides, treat them as undecided, not as a silent "on".
- Rebrand is display names, the window title, and the theme only. Keep exe, dll, OpenXR layer manifest, registry value, and `openvr_api.dll` proxy names unchanged (R7).
- The Pose app stays C# / .NET 8. The OpenXR layer and OpenVR proxies stay C++. Python is for offline analysis and plots, not for the live pose process (see the language section).
- Injectors must not write files from a frame callback. Pose must not publish a half-written block. Safety fallback is in R6.
- Credit design review notes as **Byte (design review)** and research notes as **Nobel (research)**.
- Log first, then change math. See the suggested build order below.

## Nobel's ranked likely causes (research)

Steven's report: with compensation armed, VR feels a little less smooth and somewhat nauseating; with it disarmed, being moved through the cockpit is much worse. Nobel's ranked list, to be confirmed from the first Combined log (R1) before any math change:

1. **Timer quantization.** Serial loop polls `BytesToRead` then `WaitOne(4)`. Publish loop uses `Environment.TickCount64` plus `Thread.Sleep`. Neither calls `timeBeginPeriod` or uses a high-resolution timer [code]. Processes that do not call `timeBeginPeriod` get the default ~15.6 ms tick [source]. [inference] The "250 Hz" publish is most likely about 64 Hz in steps. Sample age sawtooths from 0 to about 16 ms, sometimes 30 ms, and beats against the 90/120 Hz frame rate. At 20 deg/s, 15.6 ms of age variation is about 0.31 deg (19 arcmin) of frame-to-frame error, above Meta's 0.77 arcmin jitter threshold. Fix is in R8.
2. **No time alignment.** The layer applies the latest rig sample to an HMD pose already predicted to display time, and ignores `viewLocateInfo->displayTime`. Gyro packets (0x52) are parsed but discarded [code]. Residual world motion equals rig rate times lag, and shows up as "swim" when the platform changes direction [inference, with [source] on OpenXR `xrWaitFrame` and LaValle prediction]. Gyro prediction, capped at 40 ms, is in R5.
3. **Probable CoR error.** `imu_to_eye_m` = (0, 1.15, 0.27) m is used as an absolute pivot in the app's reference space [code]. [inference] The true pivot in that space is (neutral eye position) minus (imu_to_eye), about 1.15 m below the eyes, not 1.15 m above the space origin, so the pivot may be about 1.15 m too high unless the space origin already sits at the IMU. Rotation can still cancel (horizon planted, acceptance tests pass) while a translation residual remains: about 5 cm of view slide for a 1 m error at 3 deg of pitch [inference]. That is a classic visual-vestibular conflict. **Do not change geometry until the sine-pitch test in R4 has been run.**
4. **OpenXR layer does not restore the uncompensated pose in `xrEndFrame`** (OXRMC does) [code, source]. [inference] Late reprojection can fight the compensation, worse on ACE/LMU than AMS2/ACC. New item: R12. Runtime behavior on PSVR2 is not documented in sources Nobel could open; see Gaps.
5. **Witmotion defaults.** Bandwidth defaults to 20 Hz (lag and repeated samples when the output rate is higher). Output rate is capped at 200 Hz. The Kalman tilt estimate uses the accelerometer, which platform linear acceleration corrupts. Dynamic accuracy is about 1 deg [source: WT901C materials; see Gaps]. Reconfig is in R9.

Smaller, still worth logging: translation gains are all 0, so platform heave/surge/sway stay in the view [code]; torn shared-memory reads [code]; Euler order and axis-mapping may be slightly wrong for combined pitch plus roll [inference].

## Suggested build order

Logging first. Do not change compensation math until one session has been recorded.

1. **R1 logging**, with enough of **R8** (QPC stamps, event-driven or high-res timing) and **R6** (torn-read guard) that the first log is trustworthy.
2. One measured session, then **R2** / **R3** / **R4** analysis: does cause 1, 2, or 3 dominate?
3. Finish **R8** (event-driven serial, timer resolution) if the log shows the 15.6 ms step.
4. **R4 CoR sine-pitch test.** Only then consider a geometry change.
5. **R12** `xrEndFrame` restore, with an A/B toggle.
6. **R9** Witmotion output and bandwidth, after Steven confirms the IMU model (see Open questions).
7. **R5** gyro prediction (clamp 40 ms) and One-Euro, after the raw lag in R8 is reduced. Prediction should stay "a small amount" [source: Carmack, via Nobel].
8. **R7**, **R10**, and **R11** can proceed in parallel once the live path is stable.

Later, not this revision: predictive `T_rig` from the motion-command stream (Source B).

## R1. Combined-style per-frame logging

**Today.** There is no Combined-style log. Pose shows live text boxes. The OpenXR layer and OpenVR proxies append to `layer.log` / `simseatlock-openvr.log` with `fopen` on some early hook calls. Shared-memory stamps use `DateTime.UtcNow` / `GetSystemTimeAsFileTime`, not a common high-resolution clock. `Game.v1` stores only the post-compensation pose: no raw HMD pose, no predicted display time, no frame index.

**Want.** One timestamped row per frame (or per IMU sample, with injector rows joined by sequence / frame index), on a single QPC clock, in the same spirit as Bohnster Torque's Combined log. Readable column names, plus a sidecar JSON with version, geometry, gains, filter settings, title, and start time. Auto-start and auto-name like BT (session, car/track if known, otherwise title + local time). Optional coupling with game telemetry, starting with LMU shared memory, so a motion cue can be lined up with the correction that followed it.

**Columns (minimum).**

- Clock: QPC ticks and QPC frequency, UTC wall time for humans only.
- IMU raw: arrival QPC (`t_rx_qpc` when the read returns), device sample time if the IMU provides it, device sample counter, packet type, raw roll/pitch/yaw, gyro, accel, angle Hz, checksum-drop count, bytes per read, COM status, duplicate-sample flag (same angles in a row, which would confirm the 20 Hz bandwidth issue [source, Nobel]).
- Computed `T_rig`: roll, pitch, yaw (deg), surge, sway, heave (m), quaternion, home, gains, invert, armed flag, publish sequence, publish QPC, filtered and predicted outputs.
- SteamVR HMD pose: position, quaternion, space (seated/standing), valid/live, QPC of the read. For any OpenVR predicted pose, also log `fPredictedSecondsToPhotonsFromNow`.
- Injected / adjusted pose from the OpenXR layer and from the OpenVR proxy: raw `T_hmd` before compensation and `T_view` after, predicted display time (`xrWaitFrame` `predictedDisplayTime` on OpenXR, converted to QPC; compositor frame timing plus the prediction interval on OpenVR), frame index (so dropped or reused poses show), injector-read QPC, the Rig sequence that was applied, rig age (`t_display - t_sample`), and whether a torn read was rejected (Nobel (research), section 4.1).
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
3. Injector to predicted display: injector-apply QPC vs OpenXR `predictedDisplayTime`, or vs OpenVR `fPredictedSecondsToPhotonsFromNow` plus compositor frame timing. Also report rig age at consumption (`t_display - t_sample`); a 0 to 16 ms sawtooth would support cause 1 (Nobel (research)).

**Total motion-to-photon.** Sum of the software stages above, plus the compositor's own prediction, is the software estimate. It is not the full truth. Methods (Nobel (research), section 4.2):

- Cross-correlation, no extra hardware [inference]: head on a headrest so the HMD moves only with the rig, 0.5 to 2 Hz pitch or roll sine, lag between raw HMD orientation and IMU angle at peak correlation.
- Camera: high-speed video of the rig edge and the display update, count frames [source: Carmack, via Nobel].
- Photodiode on the lens vs a rig-motion onset [inference].
- Subjective: MISC ratings every 10 minutes, A/B blind, not only pre/post SSQ [source: Levulis et al., via Nobel].

Call out a gap clearly when a field could not be measured. The first shipped analysis should print each stage's median / p95 / max.

## R3. Smoothness metrics and analysis report

**Want.** An offline script (Python is the right tool here) and a short report over a Combined log:

- Correction velocity, acceleration, and jerk (deg/s, deg/s², deg/s³, and the translation equivalents).
- Jitter: frame-to-frame pose noise when the rig is still, and variation in IMU / injector inter-arrival times.
- Frame-to-frame mismatch: disagreement between the headset's predicted pose and the injected pose, including one-frame stutters and reused poses (frame index does not advance, or the same Rig sequence is applied twice).
- Dropped or late samples: checksum drops, IMU gaps, injector frames that never received a fresh Rig block, ring-buffer overruns.
- Residual of the compensated head pose when the head is still relative to the seat: rms in arcmin and mm, and its spectrum, especially 0.1 to 0.5 Hz and 5 to 20 Hz (Nobel (research)).
- Duplicate-sample rate, and a histogram of publish intervals and of rig sample age (expect a 0 to 16 ms sawtooth today if cause 1 is right).

Output: a one-page summary (medians and tails) plus plots. This is the instrument for R4, and for A/B of the smoother in R5 and the `xrEndFrame` toggle in R12.

## R4. Nausea-source investigation plan

Do not add a heavier filter, and do not change geometry, until the logs say what is wrong. Use Nobel's ranked list above as the test order. Microsoft's terms are useful when talking with Steven about what he sees: **jitter** (high-frequency shaking), **judder** (uneven motion, double images), **swim** (sway with head or rig motion) [source, via Nobel].

**Tests:**

1. **Timer / sampling (cause 1).** From the Combined log: publish-interval histogram, rig-age sawtooth, and still-stand correction jerk. If intervals cluster near 15.6 ms, R8 is the first fix.
2. **Time alignment / prediction (cause 2).** Injected pose vs predicted display time, reused frame indices, and residual vs rig rate when the platform changes direction.
3. **CoR / geometry (cause 3). Sine-pitch test, do this before any filter tuning or geometry change** (Nobel (research), section 4.3, adapted from OXRMC [source]). Head on a headrest, compensation armed, platform running a pure pitch sine (5 deg, 0.5 Hz) from the motion software's test or joystick mode. Log the compensated head position. If it moves fore and aft in phase with pitch, the pivot height is wrong. Repeat with roll and watch lateral motion. Expected pivot in the reference space: `p_eye_at_Home - imu_to_eye`. Compare with the value the code uses today, `(0, 1.15, 0.27)` [code]. Only then consider a fix: the pivot should be captured when Home is pressed, or computed from the HMD pose at Home.
4. **`xrEndFrame` / reprojection (cause 4).** A/B the R12 restore and look at frame-to-frame mismatch, especially on late frames in ACE or LMU.
5. **Witmotion fusion and config (cause 5).** Duplicate-sample rate, and false tilt during surge or sway (braking and cornering).
6. **Smaller checks.** Translation residual with gains at 0 (heave/surge in the 0.2 Hz nauseogenic band [source: Golding et al., via Nobel]). Torn-read rejects (R6). Combined pitch-plus-roll vs a pure-axis move, to test Euler order [inference]. IMU mount flex on the P5.

The first investigation is a measured session (compensation on vs off, same motion, Combined log plus the R3 report). Recommend a filter or a geometry fix only after the dominant cause is identified. If more than one is present, say so.

## R5. Adaptive low-latency smoother (One-Euro plus prediction)

**Want.** A live, low-lag smoother on `T_rig` (and therefore on the injected `T_view`), with on-screen tunables, a bypass, and a hot-switchable A/B that writes switch markers into the Combined log. Build this after R8 has reduced the raw lag (build order step 7).

**One-Euro (Casiez et al., CHI 2012).** A 1-Euro filter is a first-order low-pass whose cutoff rises with how fast the signal is moving: `cutoff = min_cutoff + beta * |filtered velocity|`. `min_cutoff` is the cutoff when still (lower is smoother, and a bit more lag). `beta` is how quickly the cutoff climbs with speed. `d_cutoff` is the cutoff used when estimating that speed (typically 1 Hz [source]).

**Starting values for rig angles (Nobel (research), [inference]):** `min_cutoff = 1.5 Hz`, `beta = 0.05` (at 30 deg/s this gives a cutoff of about 3 Hz; raise toward 0.3 if fast motion lags), `d_cutoff = 1 Hz`. Run it at the IMU rate on the pose-process side, with QPC timestamps. Prefer filtering the **gyro rate used for prediction** over heavily filtering the angle itself; the Witmotion angle is already Kalman-filtered [inference]. The explainer chart at [`docs/handoff/img/one_euro_demo.png`](img/one_euro_demo.png) used 1.0 Hz / 0.07 / 1.0 Hz to show the idea on a 250 Hz noisy signal. In that sim, a fixed 2 Hz low-pass and One-Euro both cut still-stand noise by about 85-86% (0.188 deg RMS down to 0.028 / 0.027 deg), but the fixed low-pass lagged the kerb jolt by 36 ms at the peak (56 ms at the 50% rise), while One-Euro lagged it by 12 ms (15 ms at the 50% rise). Product defaults should start from Nobel's 1.5 / 0.05 / 1.0, not from the chart's demo values, and remain subject to Steven's on/off default decision.

**Prediction to display time (Byte, design review; detail from Nobel (research)).** One-Euro alone still adds some lag. Pair it with a short prediction to the predicted display time, clamped so a noise spike cannot throw the pose:

1. Read gyro packets (0x52) and timestamp each with QPC when it arrives (R9).
2. Publish `{q, omega, t_sample_qpc}` with a seqlock (R6).
3. In the OpenXR layer, convert `viewLocateInfo->displayTime` to QPC with `xrConvertTimeToWin32PerformanceCounterKHR` (enable `XR_KHR_win32_convert_performance_counter_time`) [source].
4. `dt = t_display - t_sample + tau_sensor`, where `tau_sensor` is the measured IMU plus serial lag. **Clamp dt to 0 to 40 ms.** Apply `q_pred = q * exp(0.5 * omega_filtered * dt)`, and scale dt down when `|omega|` is below about 2 deg/s [inference, Nobel].
5. For OpenVR, use the frame's predicted seconds-to-photons the same way.

Fix the raw lag first (R8). Prediction should be "a small amount" [source: Carmack, via Nobel]. Log the filter's internal cutoff every frame (R1).

**Controls.** Live knobs for `min_cutoff`, `beta`, `d_cutoff`, prediction on/off, and bypass (identity / raw `T_rig`). A/B can switch mid-session without restarting. Defaults pending Steven (ground rules).

**Alternatives to keep in mind, not required to ship together:** a small complementary filter that blends integrated gyro (fast) with the Witmotion angle (slow, about 0.5 to 1 Hz crossover), which can suppress duplicate-sample steps and some false tilt from platform acceleration (Nobel (research), section 2.4).

## R6. Safety: stale IMU, correction clamps, tear-free shared memory

**Today (from the code review).** The layer and the OpenVR proxy apply compensation whenever `Rig.v1` is readable and `Armed != 0`. They do not check IMU age or `Valid`. If the COM port drops, Pose marks the sample invalid, but a last pose can still be published while armed, and the injector will keep applying it. `Rig.v1` is a single packed block with a sequence field; the injector `memcpy`s the whole 88-byte block unsynchronized, so it can read half-old, half-new angles [code, Nobel (research)]. The Game map can also stay "live" after the title has stopped writing (R10).

**Want (Byte, design review; seqlock detail from Nobel (research)):**

- Clamp correction size and rate so a glitch cannot throw the eyepoint.
- If the IMU sample is older than about 20 ms, or the COM port drops, fall back to passthrough or hold, and log the reason.
- Injectors must never apply a half-written pose: a seqlock (odd/even sequence written before and after the payload, with a barrier), or a double buffer, and the reader must reject a torn or stale sequence.
- The same freshness rule applies on the Game heartbeat used for the Layer lamp (R10).

These are safety items, not tunables hidden in a file. Show fallback state on the UI. The torn-read guard should ship with the first logging build (build order step 1).

## R7. Rebrand to Bohnster SimSeatLock, black and red glow theme

**Want.** Rename the product **Bohnster SimSeatLock**, by Bohnster Innovations. Match the black-plus-red-glow look Byte is designing for Bohnster Torque (BT's new UI is PySide6 / Qt). The Pose app should feel like the same family: same palette, card styling, and glow on active nav and primary buttons only (Byte, design review).

**Implementation.** Pose is WinForms today (`SystemColors.Control`, Segoe UI). Do not port it to Python to reuse the Qt code. Restyle natively in **WPF or Avalonia** so the look matches. Window title, Live / Config / Help chrome, lamps, and help text all use the new name.

**Do not rename (Byte, design review).** `SimSeatLock.Pose.exe`, the layer dll, `XR_APILAYER_NOVENDOR_sim_seat_lock` and its registry value, and the `openvr_api.dll` proxy name. Renaming those breaks existing installs. Any later file-name migration is a separate, planned step with a shimed install path.

## R8. Pose hot-path hardening (timer resolution and event-driven serial)

**Today.** The publish loop is a UI-owned thread sleeping on `Environment.TickCount64` toward a 4 ms period. The serial loop polls `BytesToRead`, then `WaitOne(4)` when no data is waiting. IMU, SteamVR, and Game each run `Task.Run` loops with 4-8 ms waits. The SteamVR poll `AllocHGlobal`s every read. Stamps are `DateTime.UtcNow`. There is no `timeBeginPeriod` call, no `GCSettings.LatencyMode` setting, and no high-resolution wait [code].

**Why this is first among the nausea causes (Nobel (research)).** Since Windows 10 2004, `timeBeginPeriod` is per process, and a process that does not call it gets the default resolution [source: Microsoft]. The default tick is 15.625 ms, so `Sleep(1)` can wake 1 to 16.6 ms later [source: Dawson]. [inference] A "250 Hz" publish is most likely about 64 Hz in steps, with sample age walking a 0 to 16 ms sawtooth. Witmotion output is also capped at 200 Hz [source], so a 250 Hz loop would only re-send samples even if the timer were fixed [inference]. `BytesToRead` is an unreliable way to wait on a .NET `SerialPort` [source: Voigt, via Nobel]. Windows 11 may also drop a raised timer resolution when the window is minimized or occluded [source: `timeBeginPeriod` docs], and the pose UI is usually hidden behind the game, which is another reason to be event-driven rather than sleep-based [inference].

**Want (Byte, design review, tightened with Nobel (research)):**

- Event-driven serial: a dedicated reader thread doing blocking `BaseStream.Read` (no `BytesToRead` polling). Stamp each sample with `Stopwatch.GetTimestamp()` (QPC) when the read returns. Publish on every packet, immediately.
- If a periodic loop is still needed, call `timeBeginPeriod(1)`, or use `CreateWaitableTimerExW` with `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION`, on a dedicated high-priority thread (`AboveNormal` or `Highest`).
- QPC for every latency-relevant stamp (R1, R2).
- No allocations on the publish / IMU / injector-facing path (structs, `Span`, `ArrayPool`; no LINQ or strings per packet).
- `GCSettings.LatencyMode = SustainedLowLatency`.
- Keep using `MemoryMappedFile` for the injector path, with the R6 seqlock.

This is what keeps a UI redraw, a 15.6 ms sleep, or a GC pause from becoming visible judder in the headset.

## R9. Witmotion packets and device configuration

**Today.** The parser already accepts the standard 0x55 types, including time (0x50), gyro (0x52), and quaternion (0x59). Only angle (0x53) updates `T_rig`, and only accel is stored on the side. `DroppedChecksum` is correct (checksum/sync only). Angle samples are stamped with `DateTime.UtcNow`, not a device clock. There is no device sample counter in the log, so serial buffering and IMU rate jitter cannot be separated (Byte, design review). Gyro is discarded, so the layer cannot predict to display time [code].

**Device-side defaults, from Nobel (research).** Bandwidth defaults to 20 Hz, which adds lag and causes repeated samples when the output rate is higher. Output rate is capped at 200 Hz. The Kalman tilt estimate uses the accelerometer, which platform linear acceleration corrupts. Dynamic accuracy is about 1 deg [source: WT901C RS232 manual, datasheet, and WT901 page]. Those figures come from search extracts; the PDFs would not download (see Gaps). The exact Witmotion model on the P5, and its current output rate and bandwidth, are not recorded in the repo: that is a question for Steven.

**Want.**

- Parse and log TypeTime and a monotonic device sample counter alongside the QPC arrival time. Log gyro (and use it in the R5 predictor / complementary option). Optionally prefer the device quaternion when it is present, instead of packing Euler angles. Do not integrate accelerometer packets into position (existing rule).
- After Steven confirms the model, reconfigure the IMU with its PC software: **200 Hz output**, bandwidth at least 200 Hz (**256 Hz**, so bandwidth stays above the output rate, per the manual [source]), output only angle plus gyro (plus quaternion if available), and 230400 baud if the converter allows it (Nobel (research)).
- Check the COM8 driver's latency-timer setting in Device Manager. An FTDI default of 16 ms was cited in search excerpts only and is unverified (see Gaps).
- A combined-axis garage test (pitch plus roll together) to check Euler order and the hand-tuned invert / swap flags [inference].

## R10. Engine detection and Game heartbeat freshness

**Today, from the review:**

- Assetto Corsa Competizione is a supported OpenVR path (`IVRCompositor_022`), but `ACC` is not in `pose.json` `game_processes`, so the Engine lamp will not light for it.
- `GamePoseSource` treats any readable `Game.v1` block as live. After the title exits, the last snapshot can sit in the map and the Layer lamp can stay on, the same class of bug as Bohnster Torque's auto-stop-on-close issue.
- iRacing is listed and classified as `openvr`, but is still unproven. AC2 is also not classified yet (README "Next").

**Want.** Add ACC (and any other already-supported exe names) to the default process list. Treat the Game heartbeat as live only while its sequence advances inside a short freshness window, and show a clear "stale" state otherwise. Keep unproven titles labelled as unproven, including iRacing, until a measured inject path exists.

## R11. Unify version strings

**Today.** `Program.Version` is `0.2.4`, `SimSeatLock.Pose.csproj` and `app.manifest` still say `0.2.1`, and `HelpText.cs` says `v0.2.2`. README and `docs/RESEARCH.md` already say v0.2.4.

**Want.** This revision ships as 0.2.5 in all of those places, plus the window title, help text, and changelog. No leftover 0.2.1 or 0.2.2 strings.

## R12. Restore uncompensated poses in OpenXR `xrEndFrame`

**Today.** The OpenXR layer (ACE, LMU) hooks only `xrLocateViews` and `xrDestroyInstance` [code]. The game therefore submits projection views that still carry the compensated pose. OXRMC caches poses and reverts every projection-layer and quad-layer pose in `xrEndFrame` ("cache used for reverting the motion corrected pose on frame submission"), and its `use_eye_cache` option exists because the wrong choice causes jitter [source, Nobel (research)].

**Why it matters.** [inference, Nobel] With any late-stage reprojection, part of the compensation gets undone or fought frame to frame, especially on late frames. This is expected to be worse in ACE and LMU than in AMS2 and ACC. Nobel's reading of the OpenVR proxy is that it patches only the game's copy of the pose, so the compositor keeps the real pose, which is effectively the same as a revert. No PSVR2-specific SteamVR reprojection documentation was found, so the runtime side of this claim is inference backed by OXRMC's design (see Gaps). GROK.md already says SteamVR Motion Smoothing should be off while testing [code].

**Want.** Hook `xrEndFrame` and restore the uncompensated pose on submitted projection (and quad) views, following the OXRMC pattern, with an A/B toggle and Combined-log markers so Steven can feel and we can measure the difference (R3).

## Language: C# / .NET 8 vs Python for the Pose app

**Recommendation (Byte, design review, and Nobel (research)): keep the Pose app in C# / .NET 8. Do not port it to Python.**

Byte's reasons:

- The live path is a 250+ Hz serial read, pose math, and shared-memory write, concurrent with UI and logging. CPython's GIL shares that work, so a UI redraw or a GC pause becomes injector jitter. In VR, a pose error is visible judder. Bohnster Torque can tolerate a small wobble because the wheel path is filtered; a headset is not.
- .NET rules for this codebase: no hot-path allocations (structs, `Span`, `ArrayPool`), `GCSettings.LatencyMode = SustainedLowLatency`, a dedicated high-priority thread with `timeBeginPeriod(1)` or a waitable high-resolution timer, and `MemoryMappedFile` to the injectors (R8).
- Python loses mainly on interop quality (`ctypes` vs P/Invoke), PyInstaller antivirus false positives, and slower startup versus a single-file .NET publish.
- The OpenXR layer and OpenVR proxies stay C++ either way, so a Python Pose app would not remove the C++ work.
- UI: match BT's new black-and-red glow visually in WPF or Avalonia (R7). There is no need to move Pose to PySide6 to do that.
- Testability is fine in C#: xUnit, plus a recorded-IMU replay harness. Python is not a win here.
- Port effort is about 1-2 weeks, plus latency risk, with no user-visible gain.

Nobel's reasons, in agreement:

- The real problem today is not the language. It is polling and sleeping on a 15.6 ms timer [code + source].
- Witmotion tops out at 200 Hz [source], so a "250 Hz" publish only re-sends samples. Event-driven publishing on each packet gives the lowest latency [inference].
- The GIL lets only one thread run Python code at a time, the switch interval is only an ideal, and the same Windows timer issue plus an extra interop layer would still be there [source + inference]. Python is fine for offline analysis of logs (pandas, numpy, scipy); not for the real-time loop.

Python is the right tool for the offline analysis script, plots, and the latency / smoothness report in R3 (and for the One-Euro explainer chart). That is the split.

### Advantages of staying on C# / .NET 8

Native Win32 and OpenVR P/Invoke, low-latency GC modes, single-file `dotnet publish`, a UI that can match BT without a language change, and a codebase already in production on this path. The timing fixes in R8 are all available in .NET 8.

### Disadvantages / what Python would add or take away

Python would make it easier to share plot and analysis code with the research scripts, and to iterate on UI if BT's PySide6 widgets were copied. It would not add native capabilities the Pose app is missing, and it would reduce control over pause-free timing and interop. Injector capability is unchanged, because those stay C++.

## Open questions (Steven)

- Should the new smoother and the new logging default on, or default off, when 0.2.5 is first installed?
- Are you happy with the product name **Bohnster SimSeatLock** (by Bohnster Innovations)?
- WPF vs Avalonia for the restyle: team can recommend with a short mock, unless you already have a preference.
- **What exact Witmotion model is on the P5, and what output rate and bandwidth is it set to today?** Those values are not in the repo, and they decide the R9 reconfig (Nobel (research)).

## Gaps in the research (Nobel)

These are limits on the evidence, not extra build items:

- The FTDI 16 ms latency-timer figure is unverified. ftdichip.com returned 520 or a Cloudflare challenge; the number is from a search excerpt only.
- Witmotion figures (20 Hz default bandwidth, 200 Hz max output, accelerometer in the tilt Kalman, 1 deg dynamic accuracy) are from search extracts. The Witmotion PDFs would not download to the box.
- No PSVR2-specific SteamVR reprojection documentation was found or opened, so the runtime side of cause 4 / R12 is inference backed by OXRMC's design.
- The exact Witmotion model on the P5, and its current output rate and bandwidth, are not recorded: question for Steven, above.

## Already on the roadmap, not this revision

Predictive `T_rig` from the motion-command stream, with an IMU residual (`docs/ARCHITECTURE.md` v1, `IPoseSource` "Source B"). A commanded pose leads the real platform by actuator lag, so it is a natural predictor, but it misses compliance and saturation; the planned fusion is `T_cmd(t + tau_plant)` plus a slow IMU residual (Nobel (research), section 2.5). GROK.md currently forbids SimTools / FlyPT as a pose source in v0, so this stays a later cycle. `SimSeatLock.Calib` (extent and plant ID) stays with that later step.
