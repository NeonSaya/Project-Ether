# Project Ether (VR osu! Player)

![Unity](https://img.shields.io/badge/Made%20with-Unity%202022.3%20LTS-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-blue)
![Platform](https://img.shields.io/badge/Platform-PC%20VR%20%2F%20Standalone%20VR%20(OpenXR)-green)
![Status](https://img.shields.io/badge/Status-v0.7.7-brightgreen)

[中文](README.md) | **English**

## 📖 Overview

Today's VR ecosystem is overflowing with brilliant rhythm games — yet nearly all of them share one painful bottleneck: **a severe shortage of high-quality community-made beatmaps**. Meanwhile, the classic PC rhythm game `osu!` has spent over a decade cultivating a beatmap library that is massive, gorgeous, and endlessly challenging.

**Project Ether** was born to bridge these two worlds. Our ultimate goal is to build an **immersive VR osu! beatmap player** powered by the Unity engine.

Our ambition goes far beyond porting 2D notes into 3D space — we aim to **fuse the exhilarating hit feedback of Beat Saber with the top-tier audiovisual spectacle of MMD stages in VRChat.**

With virtual ray pointers resting in your hands, every high-quality osu! beatmap becomes an audiovisual feast of pure sound waves and dazzling light.

> 🟢 **Current Status: v0.7.7**

> The core loop (Launch -> Song Select -> Play -> Result) is fully connected, and Circle, Slider and Spinner all take part in play. Storyboard command parsing and GPU instanced rendering are live, with background / video / SB three-layer compositing and brightness plus opacity steered from the settings panel. Unity Jobs + Burst have taken over part of the timeline evaluation, matrix maths and particle colour updates, while the main thread keeps object management and draw submission. We are inching toward the audiovisual feast we picture, with the MMD-style dynamic stage next on the engineering list.
>
> **Platform Support**: Built for PC VR (Windows) and Standalone VR (Android). The six target devices — Pico Neo 3 / Pico 4 / Pico 4 Ultra / Quest 2 / Quest 3 / Quest 3S — receive per-device verification of input, video decoding and performance.

---

### 🎮 Core Concept

* **Relax**: Say goodbye to button mashing, mouse clicks, and full-body swinging. An innovative 3D spatial ray hover interaction mechanism delivers a smooth "point and hit" experience that keeps you comfortable even after hours of play.
* **Precision**: Casual on the surface, rigorous underneath. We follow `osu! Lazer` for millisecond-level timing windows and combo weight multipliers, preserving as much of that top-tier rhythm-game feel as we can; sliders, spinners and speed Mods settle according to this project's own implementation, growing ever closer to upstream with each release.
* **Flow**: The UI and environment blend minimalism with cyberpunk aesthetics. Every flashy, distracting element is stripped away, letting your consciousness dissolve entirely into pure music and rhythm.

---

## 💻 Tech Stack

This project is built on the latest Unity technology stack, laying a solid foundation for future cross-platform support and high-performance rendering:

* **Game Engine**: Unity 2022.3.22f1 LTS — providing long-term, stable architectural support.
* **Rendering**: Universal Render Pipeline (URP 14.0.10) — delivering excellent visual quality while ensuring high rendering efficiency and frame rate for mobile VR devices (such as Quest).
* **VR Interaction Layer**: XR Interaction Toolkit (XRI 3.3.1) — the official powerful XR wrapper library, stably handling headset spatial tracking, controller 6DoF movement, and complex ray interaction logic.
* **Underlying XR Plugin**: Uses the highly compatible OpenXR 1.10.0 protocol standard, with embedded Oculus XR Plugin 4.2.0.
* **Visual & Text Solutions**: TextMeshPro (TMP 3.0.6) ensures crisp font rendering even under close VR inspection; combined with Visual Effect Graph (VFX 14.0.10) driving GPU-level large-scale particle effects.
* **Audio Visualization Stack**: `Lasp` (Keijiro) provides PC-side system-level low-latency FFT audio capture (guarded by the `#if LASP` macro, which is only defined for Standalone, and it also needs an analyser component set up in the scene before it does anything), `AudioLink` provides DFT fine-grained frequency data through reflective integration (cross-platform compatible), and `AudioVisualizationManager` unifies three-band global Shader parameter injection and the spectrum analysis pipeline.
* **Multithreading Architecture**: Unity Jobs System + Burst Compiler — Storyboard timeline evaluation and matrix maths plus ambient particle colour updates are offloaded to Worker Threads; object management, draw submission and note spawn preparation still run on the main thread.
* **Architecture**: C# object-oriented design — strictly following modular architecture with data-view separation, providing an extremely friendly environment for open-source community secondary development and large-scale customization.

---

## ✨ Features

* **Native Parsing & Precision Judgement**: Built-in pure C# high-performance beatmap parser (`OsuParser`), directly reading `.osu` files without conversion; judgement follows `osu! Lazer`, with Slider Ticks, Repeat points and Spinner RPM all taking part in scoring. Hover hits can register 13ms early, shortening the gap between input and feedback.
* **Storyboard Full-Command Engine**: Complete parsing of `.osb` / `.osu` inline storyboards, supporting Sprite, Animation, Loop, and all Trigger command types. Sprites are drawn through GPU instancing without occupying scene hierarchy; referencing osu!lazer and storybrew's evaluation logic, timeline evaluation and matrix maths run in Burst jobs, restoring the original SB look as faithfully as we can. Actual cost scales gracefully with beatmap complexity and hardware.
* **Multithreading Architecture (Unity Jobs + Burst)**: Storyboard matrix computation and particle colour updates are offloaded to Worker Threads, with `IJobParallelFor` + `[BurstCompile]` handling the batch maths; note coordinates are still produced on the main thread at spawn time. Gains scale with beatmap and hardware.
* **Three-Layer Compositing**: Background image / video / SB three-layer independent compositing, SB Background layer can automatically replace the beatmap background, settings panel controls global brightness and opacity uniformly.
* **Immersive VR Interaction**: Ray hover interaction mechanism achieves "point and hit"; controller haptic feedback (`HapticProfile`) dynamically adjusts based on beatmap volume and judgement results; frequently used panels use `CurvedUIEffect` physical curvature and `HUDFollower` spring following to ease edge distortion and motion sickness, and whether to curve a panel depends on its distance and viewing angle.
* **Complete Game System**: Integrates AutoPlay / HR / FL and other classic Mods, built-in automatic localization system (`LocalizationManager`) supporting multilingual Unicode rendering, sound effects and haptics use `TimingPoint × SampleVolume × Settings` complete multiplication chain, precisely controllable.
* **Data-Driven Audiovisual Performance**: Integrates `AudioLink` and `Lasp` for an audio data closed loop, 128-bar spectrum rendering and 11-layer environment particles responding in real-time to BPM beats and Kiai sections; pure code particle engine (`CodeOnlyVFX`) provides smooth fallback for low-end devices.
* **Cross-Platform Build**: Supports PC VR (Windows OpenXR) and Standalone VR (Android / Pico / Quest) dual platforms. Vulkan Graphics API + IL2CPP + ARM64, Dummy Material anti-culling mechanism ensures Shaders are not stripped. PC and standalone headsets each have four quality presets, standalone headsets run unlocked, and the actual refresh rate is up to the device and workload.
* **Floor Performance Monitor**: Uses Graphy to provide FPS, memory, and audio monitoring with charts and an optional toggle.

---

## 📂 Project Structure

We place great emphasis on clean project directory organization and code standards. If you open `ProjectEther/Assets/` in Unity, you'll find the following clearly structured tree:

```text
Assets/
├── Scenes/         # Core game scenes (MainMenuScene, SongSelectScene, GameScene, ResultScene)
├── Prefabs/        # Prefab assets (various interactive UI panels, flying note entities, judgement effect spheres, etc.)
├── Shader/         # Custom URP shaders (SBInstanced GPU instancing, HolographicScreen, SBOverlay, FlashlightMask, etc.)
├── Materials/      # Static material library (glowing objects, skybox, base UI backgrounds)
├── Texture/        # 2D image assets and Sprite atlases
├── Effekseer/      # Third-party open-source particle effect resource library
├── Songs/          # Test beatmap directory
└── Scripts/        # The heart and brain of the game (all namespaces under OsuVR)
    ├── Core/           # Gameplay loop control (RhythmGameManager + Burst Jobs scheduling, NoteController/SliderController/SpinnerController, CoordinateMapper, NotePoolManager)
    ├── Data/           # Pure data model layer (OsuParser text parsing, Beatmap / HitObject entity classes, BeatmapImporter .osz import)
    ├── Storyboard/     # Storyboard full-command engine (parsing, evaluation, GPU instanced rendering, three-layer compositing)
    ├── Interaction/    # Player physical interaction layer (RayController ray logic, HapticManager haptic dispatch, AudioManager sound management, AutoPlayManager AI auto-play)
    ├── System/         # Global infrastructure (SettingsManager settings + PlayerPrefs persistence, LocalizationManager localization, ModEffectsApplier mod effects)
    ├── UI/             # UI interaction layer (SimpleMainMenu, SimpleSongSelection, VRSettingsMenu, PauseMenu)
    ├── Visuals/        # Visual magicians (CodeOnlyVFX pure code hit effects, JudgementVisualizer, EtherealEnvironment 128-bar spectrum, CodeDrivenAmbientParticles Burst particles)
    ├── Context/        # Cross-scene data courier (GameContext safely passes song selection data to gameplay scene, ResultData for results)
    ├── Rulesets/       # Impartial judge (ScoreManager handles timing windows, accuracy and Combo calculations)
    └── Editor/         # Editor extension tools (ShaderStrippingProtector Dummy material anti-culling, ShaderStripGuard shader forced inclusion)
```

---

## 💡 Developer Onboarding

Want to fork our project for deep customization or personal modifications? You're more than welcome! To prevent you from getting lost in the vast codebase, here's a "wayfinding guide" prepared for you:

### 1. How Does a Song Run on Screen? (Core Data Flow)
Understanding the data flow is absolutely key to understanding this project's architecture:
* **Parsing Stage**: When a player selects a song in the song selection screen (`SongSelectScene`), the cross-scene singleton `GameContext` silently records its path. After scene transition to `GameScene`, `OsuParser` instantly steps in, parsing the complex `.osu` text line by line and accurately translating it into a structured `Beatmap` data model in memory.
* **Mapping Stage**: Next, `CoordinateMapper` gets to work. It extracts each note's 2D coordinates and linearly maps the osu! 512×384 playfield onto a 1.5m × 1.1m vertical striking plane positioned about 2 meters in front of the player at eye level, faithfully recreating the classic flat osu! playfield experience.
* **Spawning Stage**: The engine conductor `RhythmGameManager` starts monitoring the extremely low-level hardware audio time (DSP Time). Based on the beatmap's approach rate (AR), it pre-calculates the advance amount and calls the logistics chief `NotePoolManager` to awaken sleeping notes one by one from the object pool (Spawn) in front of the player.
* **Judgement Stage**: When the player's ray touches a note, the iron-fisted judge `ScoreManager` calculates your operation error within a millisecond, determining whether it's a Great or Miss. It then immediately signals the visual department `JudgementVisualizer` to detonate dazzling hit text and glow effects at the corresponding 3D coordinates.

### 2. I Want to Change Something — Which File Should I Open?
* **I want to add a new game Mod (e.g., Hidden)**:
  1. First, go to `System/ModSystem.cs` and add a name to the `ModType` enum.
  2. Then go to `UI/ModSelectionUI.cs` and add your UI toggle switch.
  3. Finally, write your specific penalty/reward logic in `System/ModEffectsApplier.cs`, and read it during corresponding note generation (e.g., controlling MeshRenderer fade-out).
* **I think the current judgement is too strict**:
  Timing windows (scaled by OD) live in `Core/Judgement/JudgementConfig.cs`, while score tiers and Combo multipliers live in `Rulesets/ScoreManager.cs`; each note controller decides when to submit a judgement.
* **I want the hit effects to be insanely flashy**:
  Please browse `Visuals/JudgementVisualizer.cs`. To ensure maximum frame rate, current hit effects are entirely generated through pure code meshes. If you want to introduce screen-filling spark particles, it's recommended to call pre-made VFX Graph instances here through the event system.

### 3. Project Development Iron Rules (Untouchable Red Lines)
1. **Absolute Data Layer Purity**: All classes under the `Data/` directory, such as `Beatmap` and `HitObject`, are merely containers for holding parameters. It is **absolutely forbidden** to introduce Unity `GameObject` or `Transform` references within them, ensuring purity for future logic extraction.
2. **The Zero Garbage Collection (0 GC) Goal**: In the `Update` loop during music playback, never reach for `Instantiate` and `Destroy` on a whim! Whether it's flying notes or dissipating particles, go through `NotePoolManager` and reuse pooled objects, otherwise a momentary GC hitch will wreck the whole run. Existing pools cover notes and some sound effects, and coverage keeps growing — keep an eye on GC Alloc in the Profiler when you add code.
3. **VR UI Ergonomics**: When you add an interactive panel, decide whether to mount the custom `CurvedUIEffect` script and give it concave physical curvature based on its distance from the player and its viewing angle. A flat panel parked at the edge of the VR field of view causes obvious distortion and eye fatigue.

---

## 🚀 Getting Started

### 1. Hardware & Software Requirements
* **Operating System**: Windows 10/11.
* **Development Environment**: Strictly align with **Unity 2022.3.22f1 LTS** or higher versions in the 2022.3 series.
* **Hardware**: PC VR headsets supporting the OpenXR standard (e.g., Valve Index, Meta Quest via Link, Pico 4 via Streaming Assistant). If you don't have a headset on hand, you can also enable Unity's built-in `XR Device Simulator` in the project to simulate controller experience with keyboard and mouse.

### 2. Step-by-Step Project Setup
1. **Clone the Source Code**:
   Find a suitable location and open your terminal to execute:
   ```bash
   git clone https://github.com/NeonSaya/Project-Ether.git
   ```
2. **Import into Unity Hub**: Open Unity Hub, click the `Add` button, and select the just-cloned `Project-Ether/ProjectEther` subdirectory. When first opening the project, Unity will frantically download URP and XR related dependency packages and compile all project Shaders — grab a coffee and patiently wait a few minutes.
3. **Prepare Beatmap Resources**:

   > ⚠️ **Note**: The project scans not `Assets/Songs` (that directory is for testing only) at runtime, but rather the runtime folder in the system user directory.

   * Open your computer's `osu!` game root directory, enter the `Songs` folder, and pick a few of your favorite beatmap folders.
   * Find the `.osz` archive in each beatmap folder (if not present, you can right-click the beatmap on the osu! official download page and select "Download .osz").
   * Place the `.osz` files in the following path:
     - **PC**: `C:/Users/<YourUsername>/AppData/LocalLow/Nyaon/ProjectEther/Songs/`
     - **Android**: `Internal Storage/Android/data/com.Nyaon.ProjectEther/files/Songs/` (you can also tap "Import" in the settings panel and pick `.osz` files directly through the system file picker, with multi-select support)
   * The project will automatically scan and extract `.osz` files on startup, after which you'll see the corresponding beatmaps in the song selection screen. You can also directly open the Songs folder from the settings panel and drag in `.osz` files.

   > **Tip**: If the `.osz` is in folder form (already extracted beatmaps), it can also be placed directly in the above directory. Ensure each beatmap folder contains `.osu` files, audio files, and background images.
4. **Launch the Game**:
   * You must double-click to enter `Assets/Scenes/MainMenuScene.unity` from the Project panel.
   * Put on and wake up your VR headset.
   * Click the **Play (▶)** button centered at the top of the Unity editor!
   * In VR, click `Play` at the main interface, scroll through the list to select your imported masterpiece, and begin your fantastical journey!

---

Local Android builds use the development signing key by default. For releases, select your own keystore and alias in Player Settings → Publishing Settings; updates to an installed release must use its original signing key. Keep signing files, passwords, machine-specific paths, personal notes, and validation outputs out of version control.

## 🕹️ How to Play

To ensure complete data flow and initialization, **you must always start your journey from the MainMenuScene**, otherwise unpredictable null reference errors will occur.

The scene flow order is very clear:
1. `MainMenuScene` (Main Interface): Adjust language, screen brightness, and most importantly, fine-tune audio latency based on your VR streaming situation.
2. `SongSelectScene` (Song Selection): Ray scrolls through the list, right panel enables AutoPlay to watch pros play, or enables other high-difficulty Mods.
3. `GameScene` (Gameplay Core): Fully enjoy the audiovisual feast. Need a bathroom break? Press the `Menu` button on the left controller or `Options` on the right controller to bring up the immersive pause panel.
4. `ResultScene` (Results Lobby): Review your highlight moments, statistical charts will show you where you hit early and where you hit late, and claim your well-deserved S rating.

**Tips for the Innovative Relax Interaction Mechanism**:
* Throughout the entire gameplay, **you never need to press any physical buttons on the controllers** (trigger pulling is only needed for menu selection).
* **It's all about "spatial hover"**: When the continuously shrinking approach circle on the incoming note perfectly overlaps with the note body, as long as your red-blue ray is pointing at the note area, the system automatically triggers an extremely precise perfect judgement!
* **Handling Sliders**: After triggering the slider head with your ray, don't move away! Keep the ray tightly following the slider ball as it rolls all the way to the end.
* **Handling Spinners**: When a large spinner appears, frantically draw circles within the spinner area with your ray like stirring coffee to rack up points!

---

## ❓ FAQ

**Q1: Why did I fall into the void with no UI after clicking Play?**
A: Please confirm whether you directly opened the gameplay scene (`GameScene`). If you skipped the main menu, the game's core data manager `GameContext` won't know which song to load, causing it to crash. Always enter from `MainMenuScene`!

**Q2: The beatmap is clearly imported, background music is playing, but there's not a single note flying out?**
A: Press `Ctrl+Shift+C` to check the console. If there are red error messages, it's possible the audio filename with special characters wasn't successfully read.

**Q3: Why do I feel like I'm hitting accurately, but there's always a maddening delay in the sound?**
A: This blame usually falls on the VR streaming software. Whether it's Quest Link, Air Link, or Virtual Desktop, wireless network transmission inevitably introduces 20ms to 60ms of audio latency. Please go to `Settings` in the main menu and repeatedly adjust the `Audio Offset` based on your feel until the hit feedback perfectly aligns with the beat.

**Q4: Can I contribute code even without a VR device?**
A: Absolutely! Unity officially provides the `XR Device Simulator` plugin. After enabling it, you can simulate headset rotation and hand movement space on your computer screen using WASD and mouse — every contribution is welcome. Of course, if you want to debug millisecond-level feel, it's ultimately recommended to borrow a headset for real device testing.

**Q5: Which devices does the standalone headset version support?**
A: Since v0.7.1 there is a Standalone VR (Android) build, targeting six mainstream headsets: Pico Neo 3 / Pico 4 / Pico 4 Ultra / Meta Quest 2 / Quest 3 / Quest 3S. Android video playback verified working; input and frame times continue to receive device-by-device checks, and controller profiles on the Android OpenXR path are slated for a future update. Quality presets are split into four tiers per platform, standalone headsets run unlocked, and the default on first launch is medium quality, switchable in settings.

**Q6: Why doesn't the Storyboard effect look exactly the same as in osu!?**
A: Our SB engine references osu!lazer and storybrew's open-source implementations, striving to restore the original visual style and compositing logic as faithfully as possible. Due to architectural differences between Unity engine and osu!'s native rendering (such as floating-point precision, blending modes, texture sampling, etc.), subtle visual differences may appear in rare cases. We continue to optimize with each release, steadily narrowing the gap with the original.

---

## 🚀 To-Do List

The current UI, effects, and global backgrounds are still in a "raw concrete" stage. With the core gameplay mechanics now established, our future focus will shift entirely to **ultimate VR audiovisual performance** and **multi-platform adaptation**. To bring this grand vision to life, we've broken down the development plan into the following achievable milestones:

### Phase 1: Visual Effects Refactoring & Visual Impact Enhancement
- [x] **URP Post-Processing Pipeline Configuration**: Completed the URP High Fidelity configuration (HDR, MSAA 4x, 4096 shadow resolution) with Bloom and Vignette enabled; Tonemapping currently sits on Neutral, with ACES reserved as a future toggle.
- [x] **Object Fade-In Animation**: Playable objects (notes, sliders and friends) have fade-in and fade-out effects for visual fluidity and immersion; the scope is performance objects, not every object in the scene.
- [x] **Hit Feedback Overhaul**: Implemented a pure code-driven high-performance particle effects system (`CodeOnlyVFX`), supporting object pool reuse and HDR highlight burst effects.
- [x] **Refined Judgement Visualization**: Implemented `JudgementVisualizer` with independent color coding and pop-up fade animations for 300/100/50/Miss judgement results.
- [ ] **Advanced Post-Processing Customization**: `X-PostProcessing-Library` is already in the project, but its assembly is Editor-only and runs on the legacy PPv2 stack, so it cannot be treated as a shipped URP filter; the next step is picking the filters we actually want (radial blur, chromatic aberration, film grain) and porting them onto the URP path.

### Phase 2: Data-Driven Audio Visualization Stage (Audio $\rightarrow$ Visual) — 🟢 Audiovisual Loop Achieved
This is the project's killer feature. Core logic: `Audio digitization (FFT) -> Data stream fully drives visuals (Shader parameters & particle velocity)`.
- [x] **Precise Audio Band Capture**: Integrated Keijiro's `Lasp` for real-time, ultra-low-latency multi-band FFT audio data streaming. (The `#if LASP` macro is defined for Standalone, and system capture additionally needs an analyser component configured in the scene.)
- [x] **Established Global Visual Channel**: Integrated VRChat community's powerful `AudioLink`, establishing a fundamental channel for audio data to control global Shader material transitions and ambient lighting through reflective integration.
- [x] **128-Bar Spectrum Visualization**: `EtherealEnvironment` drives 128 spectrum bar rendering, supporting AudioLink DFT fine-grained frequency bands with automatic fallback to three-band (Bass/Mid/Treble) dual channel.
- [x] **BPM Precise Sync & Kiai Detection**: Implemented precise beat synchronization based on beatmap BPM (binary search TimingPoints), parsing and responding to Kiai sections for more impactful light and shadow bursts during Kiai.
- [x] **Code-Driven Environment Particles**: Implemented a pure code computation environment particle system (11 particle layers), providing a low-end fallback for future GPU particle solutions.
- [x] **URP Material Full Cleanup**: Replaced all `Shader.Find("Standard")` with `Universal Render Pipeline/Lit`, unified `_Color` → `_BaseColor` property names, configured floor material for a deep, ethereal mirror effect (high Metallic/Smoothness + subtle Emission).
- [ ] **Complete Scene Model Overhaul**: Deep application of `Effekseer`, combined with AudioLink data, to create the first MMD-style large-scale dynamic stage background that breathes and pulses with music frequency.

### Phase 3: osu! Classic Features VR Revamp — 🟢 Storyboard Engine Live
- [x] **Storyboard Full-Command Parsing**: Complete support for Sprite, Animation, Loop, Trigger and all Fade/Move/Scale/Rotate/Color/Parameter commands.
- [x] **GPU Instanced Rendering**: Sprites stay out of the scene hierarchy and are submitted as instanced draws, with Alpha Blend and Additive dual-channel rendering.
- [x] **Multithreaded Timeline Evaluation**: Referencing osu!lazer and storybrew's command evaluation logic, timeline evaluation and matrix computation run in Burst jobs; the main thread still organises batches and submits draws.
- [x] **Video Background Playback**: Supports `.mp4` / `.webm` / `.mov` background video, rendered to the holographic screen via `VideoPlayer` + `Graphics.Blit`; unsupported extensions such as `.avi` are skipped and fall back to the background image. Android video playback verified working; other codecs and devices continue to be validated in their own environments.
- [x] **Three-Layer Compositing**: Background image / video / SB three-layer independent compositing, SB Background layer can automatically replace the beatmap background, settings panel controls global brightness and opacity uniformly.
- [x] **Retry and Trigger State**: Retrying from the pause menu clears the trigger chains, pending triggers and video sync state without re-decoding textures, and revoked trigger records are reclaimed before rendering.
- [ ] **Long-Term Bound on Live Trigger History**: Long sessions can still accumulate live trigger records; the next step is establishing a lower bound on future trigger times so pruning becomes safe.
- [ ] **Effekseer Effect Performance**: Utilize `Effekseer` to create spectacular particle effects linked with Storyboard.

> **Regarding Storyboard Fidelity:** This engine references osu!lazer and storybrew's open-source implementations, restoring osu!'s original Storyboard visual style and compositing logic as faithfully as possible under Unity URP pipeline. Due to engine architecture differences, pixel-perfect consistency is not guaranteed, but for the vast majority of beatmaps, a viewing experience closely matching the original can be provided. We will continue to align with upstream updates, progressively improving restoration accuracy.

### Phase 4: Multi-Platform Device Adaptation (PC / Quest / Pico) — 🟢 Dual-Platform Build Ready
- [x] **Cross-Platform File System**: All file I/O unified using `Application.persistentDataPath`, supporting .osz drag-and-drop import (PC) and Android native file picker.
- [x] **Android Graphics API**: Configured around Vulkan priority + IL2CPP + ARM64, with ComputeBuffer / GPU Instancing riding that path; driver differences on individual devices are being ironed out through real hardware testing.
- [x] **Shader Anti-Culling**: Dummy Material resource smuggling + Always Included Shaders dual protection, ensuring custom Shaders are not stripped from builds.
- [x] **OpenXR Dual Platform**: PC (OpenXR) + Android (Oculus + OpenXR) dual Loader configuration is in place; per-device controller mapping continues to be verified, and controller profiles on the Android OpenXR path are slated for a future update.
- [ ] **Domestic Device-Specific Optimization**: Dedicated controller high-poly display and precision haptic feedback tailored to the vibration motor characteristics of mainstream domestic headsets like Pico 4.

### Phase 5: Global Multithreading Optimization — 🟢 Core Pipeline Live
- [x] **Storyboard Timeline Multithreading**: Timeline evaluation and matrix computation are Burst-parallelized and written into Persistent NativeArrays; the draw path still calls `ComputeBuffer.SetData` to upload instance data.
- [x] **Particle Color Calculation Job-ified**: `CodeDrivenAmbientParticles` 12000 particle HSV + flicker calculation offloaded to Burst Job.
- [x] **Note SoA Flattening**: `NativeArray<double>` spawnTimes / startTimes plus a type array are filled on the main thread, and spawning narrows the range with a binary search; Burst precomputation of note positions at load time stays on the evaluation list.
- [x] **Binary Search Replaces Linear Scan**: `SpawnNotes` uses O(log N) upper bound search replacing while loops.
- [ ] **Custom Collision Detection**: Today each hand fires one `SphereCastNonAlloc` per frame, with no measurement showing PhysX queries are the bottleneck; "swap it out past 500 active notes" is only a candidate threshold, so the next step is gathering reproducible performance data.

### Phase ???: Beyond the Stars — 🔭 Looking Toward Unity 6

> This is not a mandatory item on the roadmap, more like a distant aspiration. From Unity 2022 LTS to Unity 6 means the full readiness of next-generation rendering stacks like Render Graph and GPU Resident Drawer. We plan to migrate Project Ether to the new engine, maintaining the existing style and experience while stepping into the next technological generation. When exactly we'll embark is still being charted, but the direction is already on the star map.

- [ ] **Engine Upgrade to Unity 6**: Migrate from Unity 2022.3.22f1 LTS, completing API adaptation, package dependency updates and deprecated API replacement. Before setting off, capture a reproducible Unity 2022 baseline for both platform builds, judgement, media playback and frame times on a separate branch, and settle the target version only after it passes.
- [ ] **URP Render Graph Adaptation**: Move the holographic screen, SB instanced rendering and post-processing into Render Graph step by step. SB currently records its own RT work outside the graph through `Graphics.ExecuteCommandBuffer`, and the holographic screen is a MeshRenderer with shared materials, so this is far more than renaming a few APIs — and the first step is measuring whether there is real fallback overhead.
- [ ] **GPU Resident Drawer & STP**: GPU Resident Drawer only helps eligible ordinary scene MeshRenderers — Storyboard draws through procedural instancing and will chart its own path. STP needs its own version and XR support assessment: the Unity 6.0 compatibility table marks it unsupported, so it stays on the watch list rather than the MSAA replacement slot.
- [ ] **Full Pipeline Regression & Dual-Platform Verification**: After upgrade, cover core gameplay judgement, beatmap parsing, Storyboard full-command rendering, and PC VR / Standalone VR dual-platform builds, ensuring no functionality regression and performance no lower than current baseline.

---

## 🤝 Contributing

We warmly welcome both veterans and newcomers to help build Project Ether!

If you have skills (whether in coding, effects, or UI), here's how to get involved:
1. Visit the Github repository's `Issues` section and look for tasks labeled `help wanted` or `good first issue`.
2. Fork this promising repository to your own account.
3. Create a catchy new branch (e.g., `feature/AddAwesomeLaserVFX`).
4. After pouring your sweat into the code, commit your changes and submit a Pull Request to this project.
5. We'll review your code as quickly as possible and merge your brilliant ideas!

---

## 💖 Credits

This project's transformation from cocoon to butterfly would not be possible without the selfless contributions of the following outstanding open-source projects and community developers. Standing on the shoulders of giants, we can gaze at the stars:

* **[osu!](https://osu.ppy.sh/) (by peppy)**: The absolute soul of all gameplay in this project. Its completely open-source beatmap ecosystem (.osu) and exquisitely designed rhythm mechanics are where all dreams begin.
* **[osu!lazer](https://github.com/ppy/osu)**: Core reference for Storyboard command evaluation logic. Its timeline state machine, Loop dynamic iteration, and property priority designs provide the most authoritative implementation basis for this project's SB engine.
* **[storybrew](https://github.com/Damnae/storybrew)**: Reference benchmark for Storyboard editor and rendering simulation. Its command timeline system, easing function implementation, Loop/Trigger runtime parsing, and Sprite compositing logic helped us calibrate every detail of the rendering pipeline.
* **[osu-droid](https://github.com/osudroid/osu-droid)**: Its battle-tested C# open-source parsing code provided critical and irreplaceable reference for our independently written zero-error top-tier beatmap parser (`OsuParser`).
* **[OsuParsers](https://github.com/mrflashstudio/OsuParsers)**: Lightweight .osu/.osb file format parsing library. Its clear decoder architecture and data model design provided important cross-reference for our beatmap parser completion and validation.
* **[Lasp](https://github.com/keijiro/Lasp) (by Keijiro Takahashi)**: The master's ultra-low-latency audio analysis library, the ultimate cornerstone for our real-time capture of high-precision FFT data streams.
* **[AudioLink](https://github.com/llealloo/vrc-udon-audio-link)**: A revolutionary system from the VRChat geek community, bringing unprecedented possibilities to this project's "audio data-driven visuals" (Audio-Reactive Visuals).
* **[X-PostProcessing-Library](https://github.com/QianMo/X-PostProcessing-Library) (by QianMo)**: Providing stunningly beautiful URP customizable post-processing cinematic filter libraries, greatly elevating the project's visual impact ceiling. Deep remembrance for the great QianMo.
* **[Effekseer](https://effekseer.github.io/)**: An extremely powerful and cross-platform open-source particle effect editing tool, the core arsenal for building our anime-style dynamic interactive stage.
* **[Graphy](https://github.com/Tayx94/graphy) (Tayx94 / Martín Pane) [MIT]**: FPS, Memory, and Audio stats and graphs for the floor performance monitor, using v4.0.0 pinned to commit [`1068f3de2bc9f1e4a905150abd59f31e6dead679`](https://github.com/Tayx94/graphy/commit/1068f3de2bc9f1e4a905150abd59f31e6dead679).
* **[Unity](https://unity.com/)**: Thanks to the official **XR Interaction Toolkit**, **Universal Render Pipeline** and **VFX Graph**, enabling every ordinary indie developer to single-handedly create 3D immersive worlds with exceptional fluidity and visual fidelity.

---

## 📄 License
This project proudly embraces the open-source spirit, released under the **GNU General Public License v3.0 (GPL-3.0)**. The GPL-3.0 **permits commercial use**: you are free to use, modify and redistribute this project, but copying, modification and distribution come with licence obligations — distributing a derivative work requires keeping the appropriate notices and providing the corresponding source under GPL-3.0, among other conditions. See the [LICENSE](LICENSE) file in the repository root directory for details; third-party assets may carry their own licences that need separate review.

Graphy is licensed under the **MIT License**, with the notice `Copyright (c) 2018 Martín Pane`. Its complete upstream license is preserved in [Resources/ThirdPartyLicenses/Graphy.txt](ProjectEther/Assets/Resources/ThirdPartyLicenses/Graphy.txt) and included in builds; the in-game Credits also display this copyright notice and the full MIT terms.
