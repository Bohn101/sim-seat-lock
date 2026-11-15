# SimSeatLock motion compensation: discomfort causes, smoothing, runtime, logging

Prepared by Nobel for Apex and Steven, Sun Nov 15, 2026, about 3:45 AM MST.
Research and proposals only. Nothing was built, pushed, or changed.

Evidence tags used below:
- **[code]** read directly in `Bohn101/sim-seat-lock` (read-only clone, commit `5b7c81c`, committed Sat Sep 26, 2026, 1:01 PM MDT).
- **[source]** stated on a page I opened (URL in the source list at the end).
- **[inference]** my own reasoning or arithmetic, not stated by a source. Verify before acting.

---

## 0. Short answer: prioritized likely causes

| # | Likely cause | Evidence | Why it fits "less smooth, mildly queasy" |
|---|---|---|---|
| 1 | **Pose process runs on the default 15.6 ms Windows timer.** Serial loop polls `BytesToRead` then `WaitOne(4)`. Publish loop uses `Environment.TickCount64` plus `Thread.Sleep(<=8)`. Neither calls `timeBeginPeriod` or uses a high-resolution timer. | [code] `WitmotionSerialPoseSource.cs` Run(), `MainForm.Live.cs` PublishLoop(). [source] Microsoft: since Win10 2004, `timeBeginPeriod` is per process, and processes that do not call it get the default resolution. Dawson: default tick is 15.625 ms, so `Sleep(1)` wakes 1 to 16.6 ms later. | [inference] The "250 Hz" publish is most likely about 64 Hz in steps. Sample age sawtooths from 0 to about 16 ms, sometimes 30 ms, and beats against the 90/120 Hz frame rate. At 20 deg/s rig rate, 15.6 ms of age variation is 0.31 deg (about 19 arcmin) of frame-to-frame error. Meta's measured jitter threshold is 0.77 arcmin, and jitter below that threshold still caused discomfort over 90 minutes. |
| 2 | **No time alignment.** The layer applies the *latest* rig sample to an HMD pose the runtime has *predicted* to `displayTime`. The rig pose is late by IMU filtering, USB serial, polling, publishing, and the whole render-ahead interval. | [code] `layer.cpp` LayerLocateViews ignores `viewLocateInfo->displayTime`. Gyro packets (0x52) are parsed but discarded, and only angle (0x53) and accel are used. [source] OpenXR `xrWaitFrame`: poses should be located for the predicted display time. LaValle et al.: prediction with constant angular velocity or acceleration works up to about 50 ms. | Residual world motion equals rig angular rate times the lag. It shows up as "swim" every time the platform changes direction, which is exactly when latency is most visible (Ellis/Adelstein). |
| 3 | **Probable CoR placement error.** `T_cor` is built as a pure translation equal to `imu_to_eye_m` = (0, +1.15, +0.27) m and used as an absolute point in the app's reference space (LOCAL/STAGE for OpenXR, seated universe for OpenVR). The true pivot in that space is (neutral eye position) minus (imu_to_eye), i.e. about 1.15 m *below* the eyes, not 1.15 m above the space origin. | [code] `math.cpp` ApplyCompensateRigid: `Compose(cor, Compose(Inverse(rig), Compose(Inverse(cor), hmd)))` with `cor = {eyeX, eyeY, eyeZ}`. [inference] Unless the reference-space origin sits exactly at the IMU, the pivot is wrong. | Rotation is still fully cancelled, so the horizon stays planted and the existing acceptance tests pass. But a translation residual of about (I - R) times the CoR error remains. For example, a 1 m error at 3 deg of pitch gives about 5 cm of view slide; a 2.3 m error (LOCAL origin at the head) gives about 12 cm [inference]. Rotation correct but position wrong is a classic visual-vestibular conflict. **Verify first** (test in section 4.3). |
| 4 | **No `xrEndFrame` revert in the OpenXR layer (ACE, LMU).** The game submits projection views with the compensated pose, so the runtime's late reprojection compares a pose that never happened against the real head pose. | [code] The layer only hooks `xrLocateViews` and `xrDestroyInstance`. [source] OXRMC caches poses and *reverts* every projection-layer and quad-layer pose in `xrEndFrame` ("cache used for reverting the motion corrected pose on frame submission"). Its `use_eye_cache` option exists because the wrong choice causes jitter. | [inference] With any late-stage reprojection, part of the compensation gets undone or fought frame to frame, especially on late frames. Expected to be worse in ACE/LMU than AMS2/ACC. My reading of the OpenVR proxy is that it patches only the game's copy of the pose, so the compositor keeps the real pose, which is effectively the same as a revert. |
| 5 | **Witmotion configuration and fusion limits.** Bandwidth defaults to 20 Hz, which adds lag and causes repeated samples when the output rate is higher. Output rate is capped at 200 Hz. The Kalman tilt estimate uses the accelerometer, which platform linear acceleration corrupts. Dynamic accuracy is about 1 deg. | [source] WT901C RS232 manual and datasheet, WitMotion WT901 page. | Stale or duplicated samples add steps. Surge and sway make the IMU see a false tilt, so compensation adds a small wrong roll or pitch during braking and cornering [inference]. |
| 6 | **Translation is not compensated.** Surge, sway, and heave gains are 0, and the P5's translational motion passes straight into the inside-out tracked HMD. | [code] `pose.json` gain_trans_m all 0. [source] Golding et al.: nauseogenicity peaks around 0.2 Hz for both real and visual motion. | The low-frequency heave and surge sway that remains is in the most nauseogenic band [inference]. |
| 7 | **Torn shared-memory reads.** The C# writer locks, but the C++ reader does an unsynchronized `memcpy` of 88 bytes, so it can read half-old, half-new angles. | [code] `shm.cpp` TryReadRig, `PoseSharedMemory.cs` Write. | Rare single-frame pops [inference]. |
| 8 | **Euler order and axis-mapping mismatch** between Witmotion's angle convention and `EulerDegToQuat` in the layer and in C#. `swap_pitch_roll` and inverts are hand-tuned. | [code] `math.cpp` EulerDegToQuat. [inference] | Correct for pure pitch or pure roll, but slightly wrong for combined pitch plus roll, which is the most common motion in corners. Verify with a combined-axis test. |

**Recommended first changes (proposals for Steven's CR log, not built):**
1. **Fix timing in the pose process.** Use a dedicated reader thread doing blocking `BaseStream.Read` (no `BytesToRead` polling). Publish on every packet, immediately, with a `Stopwatch.GetTimestamp()` (QPC) timestamp per sample. If a periodic loop is kept, call `timeBeginPeriod(1)` or use `CreateWaitableTimerExW(CREATE_WAITABLE_TIMER_HIGH_RESOLUTION)`. Add a seqlock (odd/even sequence before and after the write) so the layer can reject torn reads.
2. **Log before changing any math** (section 4). One session shows whether #1, #2, or #3 dominates.
3. **Check and fix the CoR** (#3) with the sine-pitch test in 4.3. The pivot should be `eye_neutral_in_space - imu_to_eye`, captured when Home is pressed, or computed from the HMD pose at Home.
4. **Add an `xrEndFrame` hook** that restores the uncompensated pose on projection views (the OXRMC pattern), with an A/B toggle.
5. **Predict the rig pose forward** to `displayTime` using the gyro packets (section 2.3), with a clamp.
6. **Reconfigure the Witmotion** with its PC software: 200 Hz output, bandwidth at least 200 Hz (256 Hz) per the manual's "set bandwidth above output rate", output only angle plus gyro (plus quaternion if available), and 230400 baud if the converter allows it.
7. Later, under the plan's Source B (still off by GROK.md rules until approved): use the motion command stream as the predictor and the IMU as the correction.

---

## 1. Causes of discomfort with motion compensation

### 1.1 Latency and jitter thresholds
- **About 20 ms motion-to-photon** is the classic target. Carmack: "when absolute delays are below approximately 20 milliseconds they are generally imperceptible"; 50 ms "will feel responsive, but still subtly lagging"; perceived latency is "one of the primary causes of simulator sickness". He advises using extrapolation only for "a small amount of prediction" [source: danluu archive].
- **Latency JND 15 to 17 ms** for trained observers, regardless of scene complexity (Mania, Adelstein, Ellis, Hill 2004; Adelstein et al. 2003). Ellis et al. 2004: "perceptual stability ... will require latencies less than 16 ms". Observers detect latency through *image slip* and are most sensitive to *velocity* error [source: SAGE/ACM abstracts].
- **Jerald et al.:** mean latency JND 16.6 ms (SD 9.7, range 3.2 to 51 ms); for their conditions they suggest "acceptable maximum end-to-end system latency in the range of 5 ms". Sensitivity is highest when the head reverses direction [source: PMC3095496]. [inference] A motion rig reverses direction constantly, so compensation lag is unusually visible.
- **Jitter:** Levulis et al. (Meta, 2025): peak jitter sensitivity is **0.77 arcmin at 12 Hz** (0.23 mm at 1 m). Jitter *below* that threshold still produced measurable discomfort in a 90-minute Quest 3 study, picked up by MISC surveys every 10 minutes but not by pre/post SSQ [source: arXiv 2504.16295]. Lutwak et al.: jitter thresholds rise during self-motion [source]. [inference] Seated and relatively still relative to the bucket is close to the most jitter-sensitive case.
- **Frequency:** nauseogenicity peaks around **0.2 Hz** for both real translation and optokinetic (visual) motion (Golding et al., citing O'Hanlon and McCauley 1974) [source]. [inference] Slow residual heave or surge sway of the view is worse than small fast buzz.
- Microsoft's terms are useful for describing symptoms: **jitter** (high-frequency shaking), **judder** (uneven motion, double images), **swim** (sway with head motion when reprojection is incomplete) [source: hologram stability]. Ask Steven which word fits what he sees.

### 1.2 Sources specific to this pipeline
- **Rig-sensing lag vs HMD prediction.** The HMD pose is predicted to photon time. The rig pose is a past measurement. The mismatch is (IMU internal filter delay + USB/serial + poll wait + publish wait + game render-ahead to displayTime) [inference, built from the code and the OpenXR spec]. The compensated view therefore trails the platform, and the residual is rig rate times lag.
- **Witmotion specifics** [source: WT901C RS232 manual and datasheet, WT901 page]:
  - Output rate 0.2 to 200 Hz (default 10 Hz). If the content selected is too much for the baud rate, the module silently "automatically reduce[s] the frequency".
  - Bandwidth 5 to 256 Hz, **default 20 Hz**. With 20 Hz bandwidth and 100 Hz output "there will be 5 repeating data". Bandwidth must exceed the output rate.
  - Fusion is a "dynamic Kalman filter". 9-axis heading accuracy is 0.1 deg without magnetic interference. 6-axis heading has "dynamic integral cumulative error" (yaw drift). In vibration "there will be cumulative errors". Tilt accuracy is 0.2 deg static; "dynamic angle accuracy of 1 deg".
  - Gyro noise 0.028 to 0.07 deg/s rms; angle resolution 0.0055 deg.
  - [inference] Yaw gain is 0 in `pose.json`, so yaw drift does not matter today. Accelerometer-based tilt correction during sustained platform acceleration is the bigger accuracy risk. Motors and bass shakers near the IMU can disturb both the mag and the accelerometer.
- **USB-serial latency.** FTDI documents a 16 ms default latency timer (search excerpt only; ftdichip.com blocked every fetch, 520/Cloudflare, so treat as unverified). The WT901C converter chip is unknown, which is a gap. [inference] Check the COM8 driver's latency setting in Device Manager.
- **.NET SerialPort usage.** Ben Voigt (Sparx Engineering) calls `BytesToRead` "completely unreliable" and recommends `BaseStream` reads [source]. The current loop depends on `BytesToRead`.
- **Translation vs rotation.** Rotation-only compensation leaves platform translation in the view. SRS states its OXRMC integration compensates pitch, roll, sway (via roll) and surge, but **not** yaw, linear sway, or heave [source: SRS troubleshooting]. So rotation-only setups are common and translation residual is a known limit.
- **CoR errors.** OXRMC has a dedicated CoR-finding procedure: feed sine rotation, then move the CoR until the view stops shifting [source: OXRMC docs]. DR Sim Manager: "If the VR view tilts or shifts slightly when the platform moves, the tracker position doesn't match your eyepoint" [source]. See #3 above for the specific code concern.
- **Physical mounting and vibration.** OVRMC FAQ: bass shakers make the IMU drift and the mount must be "extremely sturdy ... every ever so slightly movement (sub-mm!) will transfer to your HMD" [source]. [inference] Check the IMU mount on the P5 for flex.
- **Community experience.** XSimulator thread "Motion Compensation sickness": a user felt sick within minutes with OVRMC, "the same kind of sick you feel when a game is running at a low framerate". A moderator replied that "speed and precision of motion [is] way more important than large axis movement" and that feeling off signals latency [source]. [inference] Reduced motion amplitude in VR is a valid parallel mitigation while timing is fixed.
- **Frame timing.** GROK.md already says SteamVR Motion Smoothing should be OFF while testing [code]. [inference] Any missed frame triggers reprojection, which interacts with #4.

---

## 2. Low-latency smoothing and prediction

### 2.1 What OXRMC actually does (read from its source and ini, commit `1416c2e`, Jul 19, 2025)
- `[translation_filter] strength = 0.50, order = 2` (single, double, or triple EMA) and `[rotation_filter] strength = 0.50, order = 2` (slerp). In code, alpha = 1 - strength per frame [source: `filter.cpp`].
  - [inference] At 90 fps and alpha 0.5, each stage has a time constant of about 11 ms (dt*(1-a)/a), so the default 2-stage filter adds roughly 20 ms. That is noticeable lag.
- `[input_stabilizer]` (experimental, off by default): samples the tracker at about 600 Hz and applies a Butterworth biquad low-pass before the per-frame filters. Cutoff = 12.5/(2s+0.1) - 12.5/2.1 Hz [source: `filter.cpp`], so s = 0.1 gives about 36 Hz, s = 0.2 about 19 Hz, s = 0.5 about 5.4 Hz [inference: arithmetic]. Per-DOF factors are available.
- Trackers: `controller`, `vive`, and virtual `srs` (Witmotion via SRS), `flypt`, `yaw`, `rotovr` [source].
- `[cache] use_eye_cache` and `tolerance`: how poses are reverted in `xrEndFrame`; either option "might work better with some games or hmds if you encounter jitter" [source].
- `connection_timeout = 3.0`, `connection_check = 1.0` [source].
- OVRMC defaults: LPF Beta 0.6 (slerp rotation), Samples 50 (DEMA translation). Typical ranges 0.3 to 1 and 1 to 150 [source: OVRMC FAQ].
- DR Sim Manager virtual tracker: 2 ms (500 Hz) output; smoothing 0 to 0.3 recommended; "Too much smoothing introduces latency ... noticeable swim" [source].

### 2.2 One-Euro filter (Casiez, Roussel, Vogel, CHI 2012)
- Low-pass with adaptive cutoff `fc = mincutoff + beta * |dx/dt|`; the derivative is filtered at `dcutoff`, which the authors fix at **1 Hz** [source: paper and project page].
- Tuning per the authors: set beta = 0 and mincutoff = 1 Hz. Hold still and lower mincutoff until jitter is gone. Then move fast and raise beta, by factors of 10 starting at about 0.001, until lag is acceptable [source].
- **Starting point for rig angles (degrees, deg/s)** [inference]: mincutoff 1.5 Hz, beta 0.05 (at 30 deg/s this gives fc of about 3 Hz; raise toward 0.3 if fast motion lags), dcutoff 1 Hz. Run it at the IMU rate on the pose-process side, with timestamps from QPC. Prefer filtering the **gyro rate used for prediction** over heavily filtering the angle itself; the Witmotion angle is already Kalman-filtered.

### 2.3 Prediction to the HMD's display time (highest-value change)
- LaValle et al. (Oculus, ICRA 2014): "constant rate" and "constant acceleration" prediction from gyro data, "predictive filters need only a few milliseconds of data and yield good performance up to 50ms". They smooth angular velocity (Savitzky-Golay) because prediction amplifies noise, and "shorten the prediction interval for slower rotations" [source].
- **Proposal [inference]:**
  1. Read gyro packets (0x52) and timestamp each with QPC when it arrives.
  2. Publish `{q, omega, t_sample_qpc}` with a seqlock.
  3. In the layer, convert `viewLocateInfo->displayTime` to QPC with `xrConvertTimeToWin32PerformanceCounterKHR` (the layer would have to enable `XR_KHR_win32_convert_performance_counter_time` itself) [source: Khronos manual page].
  4. Compute `dt = t_display - t_sample + tau_sensor`, where tau_sensor is the measured IMU plus serial lag. Clamp dt to 0 to 40 ms. Apply `q_pred = q * exp(0.5 * omega_filtered * dt)`, with dt scaled down when |omega| is below about 2 deg/s.
  5. For OpenVR, use the frame's predicted seconds-to-photons in the same way.
- Carmack's caveat applies: prediction should be "a small amount", so fix the raw lag first (#1) [source].

### 2.4 Kalman or complementary filtering
- LaValle's tracker is a complementary design: gyro integration with slow gravity (tilt) and magnetic (yaw) drift correction [source]. The Witmotion already does an internal Kalman fusion [source]. [inference] A second full Kalman on Euler angles adds little. A useful PC-side option is a small complementary filter, using integrated gyro (fast) blended with the Witmotion angle (slow, about 0.5 to 1 Hz crossover). That suppresses step artifacts from duplicate or late angle packets, and it reduces false tilt from platform acceleration if the Witmotion's own accelerometer weighting cannot be lowered.

### 2.5 Motion-platform command stream as predictor (Source B)
- FlyPT Mover's virtual tracker uses "the pose sent to the rig hardware" (commanded pose), written to shared memory `motionRigPose` as doubles, with per-DOF filters available [source: FlyPT OVRMC output page]. DR Sim Manager describes the virtual tracker as "Zero latency ... comes from the motion processing pipeline" [source]. ProSimu ships "Prosimu X SimTools V3 Motion Software" [source: ProSimu page].
- [inference] A commanded pose leads the real platform by actuator lag, so it is a natural predictor. But it is not the measured truth: it misses actuator lag, compliance, and saturation. Best fusion: `T_rig = T_cmd(t + tau_plant)` corrected by a slow IMU residual (complementary or Kalman with a delay model). This matches the repo's planned Source B. GROK.md currently forbids SimTools/FlyPT as a pose source in v0, so this stays a later CR.

---

## 3. C#/.NET 8 vs Python for a >=250 Hz pose pipeline

**Recommendation: stay on C#/.NET 8** and fix the timing pattern. Python adds risk with no benefit here.

- The real problem today is not the language. It is polling and sleeping on a 15.6 ms timer [code + Microsoft + Dawson].
- Witmotion tops out at 200 Hz [source], so "250 Hz" publish only re-sends samples. Event-driven publishing on each packet gives the lowest latency [inference].
- .NET specifics:
  - `GCSettings.LatencyMode = SustainedLowLatency` suppresses foreground gen-2 collections; it requires background GC (the default) [source: .NET latency modes].
  - Keep the hot loop allocation-free: preallocated buffers, structs, no LINQ or strings per packet [inference]. With that, gen-0 GCs are rare and short.
  - Thread: dedicated, `AboveNormal` or `Highest` priority, blocking `BaseStream.Read` [source: Voigt].
  - Timer, if needed: `timeBeginPeriod(1)` per process [source: Microsoft], or `CreateWaitableTimerExW` with `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` (Win10 1803+) "when short expiration delays on the order of a few milliseconds are unacceptable" [source]. Note that Windows 11 may drop the higher resolution when the window is minimized or occluded [source: timeBeginPeriod docs], which matters because the pose UI is usually hidden behind the game. That is one more reason to be event-driven.
  - IPC: keep the memory-mapped file but add a seqlock and QPC timestamps. The layer currently stamps with `GetSystemTimeAsFileTime`, which is coarse [inference]. Use QPC on both sides.
- Python: the GIL lets only one thread run Python code at a time. The switch interval is only an "ideal duration of the timeslices"; "the actual value can be higher", and the OS picks the next thread [source: Python sys docs]. Add GC pauses, the same Windows timer issue, and an extra interop layer for the shared memory. Fine for offline analysis of logs (pandas, numpy, scipy); not for the real-time loop [inference].

---

## 4. What to log and how to measure

### 4.1 Log schema (CSV or binary ring buffer, all times QPC ticks plus QPC frequency)
Pose process, one row per IMU packet:
- `t_rx_qpc` (read returned), packet type, Witmotion chip time if enabled, raw roll/pitch/yaw, gyro x/y/z, accel x/y/z, filtered and predicted outputs, `seq`, dropped count, bytes per read.
- Publish: `t_pub_qpc`, `seq`.

Layer, one row per `xrLocateViews` call (and `xrEndFrame` if hooked):
- `t_call_qpc`, `displayTime` (XrTime and its QPC conversion), `seq` and `t_sample_qpc` of the rig sample used, **rig age = t_display - t_sample**, raw HMD view pose (L/R), compensated pose, reference space type, whether a torn read was rejected.
- Frame-to-frame `displayTime` delta (90 vs 120 Hz, missed frames).

Derived plots:
- Histogram of rig sample age at consumption. Expect a 0 to 16 ms sawtooth today if #1 is right.
- Histogram of publish intervals.
- Duplicate-sample rate (same angle values in a row, which is the Witmotion bandwidth issue).
- Residual = compensated head pose in world (should be still when the head is still relative to the seat). Report its rms in arcmin and mm, and its spectrum, especially 0.1 to 0.5 Hz and 5 to 20 Hz.
- SteamVR frame timing (Advanced Frame Timing) for the same window.

### 4.2 Measuring motion-to-photon latency of the compensation
- **Cross-correlation, no extra hardware** [inference]: put the head on a headrest (or strap the HMD to the seat) so the HMD moves only with the rig. Drive a 0.5 to 2 Hz sine in pitch or roll. Record HMD raw orientation (from the runtime) and the IMU angle. The lag at peak cross-correlation is the rig-sensing latency relative to the HMD's own tracking. The compensated pose should then be about constant; any remaining sine amplitude and phase is the error budget. Repeat with prediction on and off.
- **Camera method** [source: Carmack]: "record high speed video that simultaneously captures the initiating physical motion and the eventual display update", and count frames. Point a 240 fps phone at the rig edge and at the lens (or the mirrored desktop view) while bumping the rig.
- **Photodiode** [inference]: render a high-contrast edge, tape a photodiode to the lens, and put a second sensor on the rig (or use an IMU tap on a scope). Measure the time from rig motion onset to the change in brightness.
- **Subjective** [source: Levulis et al.]: use MISC ratings every 10 minutes rather than pre/post SSQ only, A/B blind for each change.

### 4.3 CoR verification test (do before any filter tuning)
Adapted from OXRMC's documented procedure [source]:
1. Head on headrest, compensation armed, platform running a pure pitch sine (5 deg, 0.5 Hz) from the motion software's test or joystick mode.
2. Log the compensated head *position*. If it moves fore and aft in phase with pitch, the pivot height is wrong. Repeat with roll and watch lateral motion.
3. Expected pivot in the reference space: `p_eye_at_Home - imu_to_eye`. Compare with the value the code uses today, `(0, 1.15, 0.27)` [code].

---

## 5. Sources opened this session (Sun Nov 15, 2026, about 3:28 to 3:45 AM MST)

Project and motion-compensation tools
- SimSeatLock repo (read-only clone): https://github.com/Bohn101/sim-seat-lock (files cited above)
- OXRMC repo and source: https://github.com/BuzzteeBear/OpenXR-MotionCompensation (README `docs/README.md`, `configuration/OpenXR-MotionCompensation.ini`, `layer.cpp` xrEndFrame revert, `filter.cpp`)
- SRS OXRMC troubleshooting: https://simracingstudio.freshdesk.com/support/solutions/articles/35000219657-openxr-motion-compensation-troubleshooting
- OVRMC FAQ: https://ovrmc.dschadu.de/en/faq
- FlyPT Mover OVRMC output: https://www.flyptmover.com/mover-3-5/outputs/ovrmc
- DR Sim Manager virtual tracker: https://docs.departedreality.com/dr-sim-manager/general/outputs/virtual-tracker/
- ProSimu SimTools software: https://www.prosimu.com/en/prosimusimtoolsmotionsoftware.html
- XSimulator "Motion Compensation sickness": https://www.xsimulator.net/community/threads/motion-compensation-sickness.17926/

Witmotion
- WT901 product page: https://www.wit-motion.com/Module/84.html
- WT901C RS232 datasheet and manual (RobotShop CDN). The PDFs would not download to the box (HTML returned), so the figures come from the search tool's extracts of these two files: https://cdn.robotshop.com/rbm/f83835f4-5e29-4ee0-9cc2-e49300031503/6/681c0521-11c2-4e47-99b0-d336be43f142/4d2fa75a_wt901c-rs232-datasheet.pdf and .../3113655b_wt901c-rs232-manual.pdf

Perception and latency
- Carmack, Latency Mitigation Strategies (archive): http://danluu.com/latency-mitigation/
- Jerald et al., Relating Scene-Motion Thresholds to Latency Thresholds for HMDs: https://pmc.ncbi.nlm.nih.gov/articles/PMC3095496/
- Mania, Adelstein, Ellis, Hill 2004 (abstract): https://doi.org/10.1145/1012551.1012559 ; Ellis et al. 2004: https://journals.sagepub.com/doi/10.1177/154193120404802306 ; Adelstein et al. 2003: https://journals.sagepub.com/doi/10.1177/154193120304702001 (abstracts via search results)
- Levulis et al. 2025, Subthreshold Jitter in VR Can Induce Visual Discomfort: https://arxiv.org/abs/2504.16295 (PDF read)
- Lutwak, Murdison, Rio 2023, jitter and self-motion: https://kevinwrio.com/files/papers/Lutwak,%20Murdison,%20&%20Rio%20(2023)%20User%20self-motion%20modulates%20the%20perceptibility%20of%20jitter%20for%20world-locked%20objects%20in%20augmented%20reality.pdf
- Golding et al., frequency and tilt in optokinetic motion sickness: https://www.ieda.ust.hk/dfaculty/so/pdf/Pages77-88-VIMS2007.pdf
- Microsoft, Hologram stability: https://learn.microsoft.com/en-us/windows/mixed-reality/develop/advanced-concepts/hologram-stability

Filtering and prediction
- One-Euro filter page: https://gery.casiez.net/1euro/ and paper PDF: https://gery.casiez.net/publications/CHI2012-casiez.pdf
- LaValle et al., Head Tracking for the Oculus Rift: https://lavalle.pl/papers/LavYerKatAnt14.pdf

OpenXR, Windows, .NET, Python
- xrWaitFrame: https://registry.khronos.org/OpenXR/specs/1.0/man/html/xrWaitFrame.html
- XrCompositionLayerProjectionView: https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrCompositionLayerProjectionView.html
- xrConvertTimeToWin32PerformanceCounterKHR: https://registry.khronos.org/OpenXR/specs/1.0/man/html/xrConvertTimeToWin32PerformanceCounterKHR.html
- timeBeginPeriod: https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
- Bruce Dawson, Windows Timer Resolution: The Great Rule Change: https://randomascii.wordpress.com/2020/10/04/windows-timer-resolution-the-great-rule-change/
- CreateWaitableTimerExW: https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createwaitabletimerexw
- .NET GC latency modes: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/latency
- Ben Voigt, If you must use .NET SerialPort: https://www.sparxeng.com/blog/software/must-use-net-system-io-ports-serialport
- Python sys.setswitchinterval: https://docs.python.org/3/library/sys.html#sys.setswitchinterval

Gaps and blocks
- ftdichip.com returned 520 or a Cloudflare challenge on every attempt, so the FTDI 16 ms latency-timer figure is unverified (search excerpt only).
- The Witmotion PDFs would not download as PDFs to the box.
- OVRMC GitHub README raw returned 404 (the FAQ site was used instead).
- The FlyPT "VR motion compensation" basics page returned 404.
- No PSVR2-specific SteamVR reprojection documentation was found or opened, so claim #4's runtime behavior is inference backed by OXRMC's design.
- The exact Witmotion model on the P5 and its current output rate and bandwidth are not recorded in the repo.
