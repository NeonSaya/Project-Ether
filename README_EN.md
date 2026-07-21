# Project Ether (VR osu! Player)

![Unity](https://img.shields.io/badge/Made%20with-Unity%202022.3%20LTS-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-blue)
![Platform](https://img.shields.io/badge/Platform-PC%20VR%20%2F%20Standalone%20VR%20(OpenXR)-green)
![Status](https://img.shields.io/badge/Status-v0.7.3-brightgreen)

[中文](README.md) | **English**

## 📖 Overview

In the current VR ecosystem, excellent music rhythm games emerge in endless succession, yet they often share a fatal pain point: **a severe lack of high-quality community-made beatmaps**. Meanwhile, the classic PC rhythm game `osu!`, with over a decade of history, boasts a massive, stunning, and challenging beatmap library.

**Project Ether** was born to bridge these two worlds. Our ultimate goal is to build an **immersive VR osu! beatmap player** powered by the Unity engine.

Our ambition goes beyond simply porting 2D notes into 3D space — we aim to **combine the exhilarating hit feedback of Beat Saber with the top-tier audiovisual spectacle of MMD stages in VRChat.**

Using virtual ray pointers in your hands, you can effortlessly enjoy the audiovisual thrill of every high-quality osu! beatmap amidst pure sound waves and dazzling light effects.

> 🟢 **Current Status: v0.7.3**

> The core loop (Launch -> Song Select -> Play -> Result) is fully functional. Generation and scoring systems for Circle, Slider, and Spinner are all complete. Storyboard full-command parsing + GPU instanced rendering is live, referencing osu!lazer and storybrew's command evaluation logic, with video background playback and three-layer compositing rendering support. The underlying architecture has fully adopted Unity Jobs + Burst multithreading, with the entire Storyboard pipeline multithreaded. Brightness and opacity are controlled via a unified settings panel, with three-layer premultiplied alpha blending ensuring consistent visual output.
>
> **Platform Support**: Dual platform support for PC VR (Windows) and Standalone VR (Android), compatible with mainstream headsets including Pico Neo 3 / Pico 4 / Pico 4 Ultra / Quest 2 / Quest 3.

---

### 🎮 Core Concept

* **Relax**: Completely abandon traditional physical button presses, mouse clicks, and intense physical swinging. We employ an innovative 3D spatial ray hover interaction mechanism, achieving a "point and hit" smooth experience that keeps you relaxed even after extended play sessions.
* **Precision**: While the gameplay is casual, the underlying mechanics are anything but lax. We have perfectly replicated the extremely rigorous hardcore judgement logic of `osu! Lazer` at the code level. From millisecond-level timing window calculations to combo weight multipliers, the core gameplay satisfaction of a top-tier rhythm game is fully preserved.
* **Flow**: The game UI and environment adopt a visual style blending minimalism with cyberpunk aesthetics. All flashy, distracting elements are eliminated, allowing the player's consciousness to dissolve entirely into pure music and rhythm.

---

## 💻 Tech Stack

This project is built with the latest Unity technology stack, laying a solid foundation for future cross-platform support and high-performance rendering:

* **Game Engine**: Unity 2022.3.22f1 LTS — providing long-term, stable architectural support.
* **Rendering**: Universal Render Pipeline (URP 14.0.10) — delivering excellent visual quality while ensuring high rendering efficiency and frame rate for mobile VR devices (such as Quest).
* **VR Interaction Layer**: XR Interaction Toolkit (XRI 3.3.1) — the official powerful XR wrapper library, stably handling headset spatial tracking, controller 6DoF movement, and complex ray interaction logic.
* **Underlying XR Plugin**: Uses the highly compatible OpenXR 1.10.0 protocol standard, with embedded Oculus XR Plugin 4.2.0.
* **Visual & Text Solutions**: TextMeshPro (TMP 3.0.6) ensures crisp font rendering even under close VR inspection; combined with Visual Effect Graph (VFX 14.0.10) driving GPU-level large-scale particle effects.
* **Audio Visualization Stack**: `Lasp` (Keijiro) provides PC-side system-level low-latency FFT audio capture (`#if LASP` macro isolated, Standalone platform only), `AudioLink` provides DFT fine-grained frequency data through reflective integration (cross-platform compatible), `AudioVisualizationManager` unified management of three-band global Shader parameter injection and spectrum analysis pipeline.
* **Multithreading Architecture**: Unity Jobs System + Burst Compiler — Storyboard full pipeline, particle systems, note pre-computation and other core logic are all offloaded to Worker Threads for parallel execution, keeping the main thread lightweight.
* **Architecture**: C# object-oriented design — strictly following modular architecture with data-view separation, providing an extremely friendly environment for open-source community secondary development and large-scale customization.

---

## ✨ Features

* **Native Parsing & Precision Judgement**: Built-in pure C# high-performance beatmap parser (`OsuParser`), directly reading `.osu` files without conversion; strictly replicating `osu! Lazer`'s judgement logic, with precise settlement across the entire chain from Slider Ticks, Repeat points to Spinner RPM. Pre-hit detection at 13ms eliminates frame delay.
* **Storyboard Full-Command Engine**: Complete parsing of `.osb` / `.osu` inline storyboards, supporting Sprite, Animation, Loop, and all Trigger command types. Pure GPU instanced rendering, 50K sprites with zero GameObjects. Referencing osu!lazer and storybrew's evaluation logic, command evaluation and rendering are fully multithreaded, restoring original SB visual presentation as faithfully as possible.
* **Multithreading Architecture (Unity Jobs + Burst)**: Storyboard matrix computation, particle color updates, note coordinate pre-calculation — all offloaded to Worker Threads. `IJobParallelFor` + `[BurstCompile]` SIMD vectorization, main thread load reduced by 30%+.
* **Three-Layer Compositing**: Background image / video / SB three-layer independent compositing, SB Background layer can automatically replace the beatmap background, settings panel controls global brightness and opacity uniformly.
* **Immersive VR Interaction**: Ray hover interaction mechanism achieves "point and hit"; controller haptic feedback (`HapticProfile`) dynamically adjusts based on beatmap volume and judgement results; UI panels use `CurvedUIEffect` physical curvature and `HUDFollower` spring following, completely eliminating VR motion sickness.
* **Complete Game System**: Integrates AutoPlay / HR / FL and other classic Mods, built-in automatic localization system (`LocalizationManager`) supporting multilingual Unicode rendering, sound effects and haptics use `TimingPoint × SampleVolume × Settings` complete multiplication chain, precisely controllable.
* **Data-Driven Audiovisual Performance**: Integrates `AudioLink` and `Lasp` for an audio data closed loop, 128-bar spectrum rendering and 11-layer environment particles responding in real-time to BPM beats and Kiai sections; pure code particle engine (`CodeOnlyVFX`) provides smooth fallback for low-end devices.
* **Cross-Platform Build**: Supports PC VR (Windows OpenXR) and Standalone VR (Android / Pico / Quest) dual platforms. Vulkan Graphics API + IL2CPP + ARM64, Dummy Material anti-culling mechanism ensures Shaders are not stripped. PC and standalone headsets each have four quality presets, standalone headsets run unlocked at the device's maximum refresh rate.

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
* **Mapping Stage**: Next, the mathematical wizard `CoordinateMapper` gets to work. It extracts each note's 2D coordinates, applies trigonometric functions to "bend" them from a flat 2D screen, and precisely deploys them to corresponding positions on a 3D curved ring surface centered on the player's head.
* **Spawning Stage**: The engine conductor `RhythmGameManager` starts monitoring the extremely low-level hardware audio time (DSP Time). Based on the beatmap's approach rate (AR), it pre-calculates the advance amount and calls the logistics chief `NotePoolManager` to awaken sleeping notes one by one from the object pool (Spawn) in front of the player.
* **Judgement Stage**: When the player's ray touches a note, the iron-fisted judge `ScoreManager` calculates your operation error within a millisecond, determining whether it's a Great or Miss. It then immediately signals the visual department `JudgementVisualizer` to detonate dazzling hit text and glow effects at the corresponding 3D coordinates.

### 2. I Want to Change Something — Which File Should I Open?
* **I want to add a new game Mod (e.g., Hidden)**:
  1. First, go to `Data/Enums.cs` and add a name to the `ModType` enum.
  2. Then go to `UI/ModSelectionUI.cs` and add your UI toggle switch.
  3. Finally, write your specific penalty/reward logic in `System/ModEffectsApplier.cs`, and read it during corresponding note generation (e.g., controlling MeshRenderer fade-out).
* **I think the current judgement is too strict**:
  Walk straight into `Rulesets/ScoreManager.cs`. All Hit Windows (timing window milliseconds) and Combo multiplier formulas are defined here uniformly.
* **I want the hit effects to be insanely flashy**:
  Please browse `Visuals/JudgementVisualizer.cs`. To ensure maximum frame rate, current hit effects are entirely generated through pure code meshes. If you want to introduce screen-filling spark particles, it's recommended to call pre-made VFX Graph instances here through the event system.

### 3. Project Development Iron Rules (Untouchable Red Lines)
1. **Absolute Data Layer Purity**: All classes under the `Data/` directory, such as `Beatmap` and `HitObject`, are merely containers for holding parameters. It is **absolutely forbidden** to introduce Unity `GameObject` or `Transform` references within them, ensuring purity for future logic extraction.
2. **Zero Garbage Collection (0 GC) Principle**: In the `Update` loop during music playback, using `Instantiate` and `Destroy` is **strictly prohibited**! Whether it's flying notes or dissipating particles, you must request object pool reuse from `NotePoolManager`. Otherwise, momentary GC stuttering will destroy the player's entire experience.
3. **VR UI Ergonomics**: Any new interactive panel you add must forcibly mount the custom `CurvedUIEffect` script to create concave physical curvature. Flat UI at the edges of VR field of view causes severe visual distortion and eye fatigue.

---

## 🚀 Getting Started

### 1. Hardware & Software Requirements
* **Operating System**: Windows 10/11 (native Mac VR debugging not currently supported).
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
     - **Android**: `Internal Storage/Android/data/com.Nyaon.ProjectEther/files/Songs/` (the import button in settings has issues in the current version, will be fixed in a future update)
   * The project will automatically scan and extract `.osz` files on startup, after which you'll see the corresponding beatmaps in the song selection screen. You can also directly open the Songs folder from the settings panel and drag in `.osz` files.

   > **Tip**: If the `.osz` is in folder form (already extracted beatmaps), it can also be placed directly in the above directory. Ensure each beatmap folder contains `.osu` files, audio files, and background images.
4. **Launch the Game**:
   * You must double-click to enter `Assets/Scenes/MainMenuScene.unity` from the Project panel.
   * Put on and wake up your VR headset.
   * Click the **Play (▶)** button centered at the top of the Unity editor!
   * In VR, click `Play` at the main interface, scroll through the list to select your imported masterpiece, and begin your fantastical journey!

---

## 🕹️ How to Play

To ensure complete data flow and initialization, **you must always start your journey from the MainMenuScene**, otherwise unpredictable null reference errors will occur.

The scene flow order is very clear:
1. `MainMenuScene` (Main Interface): Adjust language, screen brightness, and most importantly, fine-tune audio latency based on your VR streaming situation.
2. `SongSelectScene` (Song Selection): Ray scrolls through the list, right panel enables AutoPlay to watch pros play, or enables other high-difficulty Mods.
3. `GameScene` (Gameplay Core): Fully enjoy the audiovisual feast. Need a bathroom break? Press the `Menu` button on the left controller or `Options` on the right controller to bring up the immersive pause panel.
4. `GameScene` (Gameplay Core): Fully enjoy the audiovisual feast. Need a bathroom break? Press the `Menu` button on the left controller or `Options` on the right controller to bring up the immersive pause panel.
5. `ResultScene` (Results Lobby): Review your highlight moments, statistical charts will show you where you hit early and where you hit late, and claim your well-deserved S rating.

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

**Q4: I'm a broke college student without a VR device — am I unworthy of contributing code?**
A: Absolutely not! Unity officially provides the `XR Device Simulator` plugin. After enabling it, you can simulate headset rotation and hand movement space on your computer screen using WASD and mouse. Of course, if you want to debug millisecond-level feel, it's ultimately recommended to borrow a headset for real device testing.

**Q5: Which devices does the standalone headset version support?**
A: Since v0.7.1, Standalone VR (Android) platform is officially supported, compatible with mainstream headsets including Pico Neo 3 / Pico 4 / Pico 4 Ultra / Meta Quest 2 / Quest 3. Quality presets are optimized for each device, standalone headsets run unlocked at the device's maximum refresh rate. Default is medium quality on first launch, switchable in settings.

**Q6: Why doesn't the Storyboard effect look exactly the same as in osu!?**
A: Our SB engine references osu!lazer and storybrew's open-source implementations, striving to restore the original visual style and compositing logic as faithfully as possible. However, due to architectural differences between Unity engine and osu!'s native rendering (such as floating-point precision, blending modes, texture sampling, etc.), subtle visual differences may exist in rare cases. This is an objective limitation of the current tech stack, and we will continue to optimize in future versions to gradually narrow the gap with the original.

---

## 🚀 To-Do List

The current UI, effects, and global backgrounds are still in a "raw concrete" stage. With the core gameplay mechanics now established, our future focus will shift entirely to **ultimate VR audiovisual performance** and **multi-platform adaptation**. To bring this grand vision to life, we've broken down the development plan into the following achievable milestones:

### Phase 1: Visual Effects Refactoring & Visual Impact Enhancement
- [x] **URP Post-Processing Pipeline Configuration**: Completed URP High Fidelity render pipeline configuration (HDR, MSAA 4x, 4096 shadow resolution), with built-in Tonemapping (ACES), Bloom, and Vignette.
- [x] **Object Fade-In Animation**: All notes and game objects now have physics-based fade-in effects, enhancing visual fluidity and immersion.
- [x] **Hit Feedback Overhaul**: Implemented a pure code-driven high-performance particle effects system (`CodeOnlyVFX`), supporting object pool reuse and HDR highlight burst effects.
- [x] **Refined Judgement Visualization**: Implemented `JudgementVisualizer` with independent color coding and pop-up fade animations for 300/100/50/Miss judgement results.
- [ ] **Advanced Post-Processing Customization**: Integrate `X-PostProcessing-Library` for more advanced visual filter effects (such as radial blur, chromatic aberration, film grain, etc.), further enhancing cinematic visual quality.

### Phase 2: Data-Driven Audio Visualization Stage (Audio $\rightarrow$ Visual) — 🟢 Audiovisual Loop Achieved
This is the project's killer feature. Core logic: `Audio digitization (FFT) -> Data stream fully drives visuals (Shader parameters & particle velocity)`.
- [x] **Precise Audio Band Capture**: Integrated Keijiro's `Lasp` for real-time, ultra-low-latency multi-band FFT audio data streaming. (`#if LASP` macro officially enabled)
- [x] **Established Global Visual Channel**: Integrated VRChat community's powerful `AudioLink`, establishing a fundamental channel for audio data to control global Shader material transitions and ambient lighting through reflective integration.
- [x] **128-Bar Spectrum Visualization**: `EtherealEnvironment` drives 128 spectrum bar rendering, supporting AudioLink DFT fine-grained frequency bands with automatic fallback to three-band (Bass/Mid/Treble) dual channel.
- [x] **BPM Precise Sync & Kiai Detection**: Implemented precise beat synchronization based on beatmap BPM (binary search TimingPoints), parsing and responding to Kiai sections for more impactful light and shadow bursts during Kiai.
- [x] **Code-Driven Environment Particles**: Implemented a pure code computation environment particle system (11 particle layers), providing a low-end fallback for future GPU particle solutions.
- [x] **URP Material Full Cleanup**: Replaced all `Shader.Find("Standard")` with `Universal Render Pipeline/Lit`, unified `_Color` → `_BaseColor` property names, configured floor material for a deep, ethereal mirror effect (high Metallic/Smoothness + subtle Emission).
- [ ] **Complete Scene Model Overhaul**: Deep application of `Effekseer`, combined with AudioLink data, to create the first MMD-style large-scale dynamic stage background that breathes and pulses with music frequency.

### Phase 3: osu! Classic Features VR Revamp — 🟢 Storyboard Engine Live
- [x] **Storyboard Full-Command Parsing**: Complete support for Sprite, Animation, Loop, Trigger and all Fade/Move/Scale/Rotate/Color/Parameter commands.
- [x] **GPU Instanced Rendering**: 50K sprites with zero GameObjects, Alpha Blend and Additive dual-channel rendering.
- [x] **Multithreaded Timeline Evaluation**: Referencing osu!lazer and storybrew's command evaluation logic, timeline evaluation and matrix computation fully Burst-multithreaded, zero main thread overhead.
- [x] **Video Background Playback**: Supports `.mp4` / `.avi` / `.webm` video as background, rendered to holographic screen via `VideoPlayer` + `Graphics.Blit`.
- [x] **Three-Layer Compositing**: Background image / video / SB three-layer independent compositing, SB Background layer can automatically replace the beatmap background, settings panel controls global brightness and opacity uniformly.
- [ ] **Effekseer Effect Performance**: Utilize `Effekseer` to create spectacular particle effects linked with Storyboard.

> **Regarding Storyboard Fidelity:** This engine references osu!lazer and storybrew's open-source implementations, restoring osu!'s original Storyboard visual style and compositing logic as faithfully as possible under Unity URP pipeline. Due to engine architecture differences, pixel-perfect consistency is not guaranteed, but for the vast majority of beatmaps, a viewing experience closely matching the original can be provided. We will continue to align with upstream updates, progressively improving restoration accuracy.

### Phase 4: Multi-Platform Device Adaptation (PC / Quest / Pico) — 🟢 Dual-Platform Build Ready
- [x] **Cross-Platform File System**: All file I/O unified using `Application.persistentDataPath`, supporting .osz drag-and-drop import (PC) and Android native file picker.
- [x] **Android Graphics API**: Vulkan priority enforced + IL2CPP + ARM64, ComputeBuffer / GPU Instancing fully compatible.
- [x] **Shader Anti-Culling**: Dummy Material resource smuggling + Always Included Shaders dual protection, ensuring custom Shaders are not stripped from builds.
- [x] **OpenXR Dual Platform**: PC (OpenXR) + Android (Oculus + OpenXR) dual Loader configuration, controller tracking without loss.
- [ ] **Domestic Device-Specific Optimization**: Dedicated controller high-poly display and precision haptic feedback tailored to the vibration motor characteristics of mainstream domestic headsets like Pico 4.

### Phase 5: Global Multithreading Optimization — 🟢 Core Pipeline Live
- [x] **Storyboard Full Pipeline Multithreading**: Timeline evaluation + matrix computation fully Burst-parallelized, NativeArray zero-copy direct to GPU.
- [x] **Particle Color Calculation Job-ified**: `CodeDrivenAmbientParticles` 12000 particle HSV + flicker calculation offloaded to Burst Job.
- [x] **Note SoA Flattening**: `NativeArray<double>` spawnTimes + `NativeArray<float3>` worldPositions, Burst pre-computation during loading.
- [x] **Binary Search Replaces Linear Scan**: `SpawnNotes` uses O(log N) upper bound search replacing while loops.
- [ ] **Custom Collision Detection**: Consider Burst spatial hash ray detection to replace PhysX when active notes > 500.

### Phase ???: Beyond the Stars — 🔭 Looking Toward Unity 6

> This is not a mandatory item on the roadmap, more like a distant aspiration. From Unity 2022 LTS to Unity 6 means the full readiness of next-generation rendering stacks like Render Graph and GPU Resident Drawer. We plan to migrate Project Ether to the new engine, maintaining the existing style and experience while stepping into the next technological generation. When exactly we'll embark is uncertain, but the direction is already on the star map.

- [ ] **Engine Upgrade to Unity 6 (6000.0.60f1)**: Migrate from Unity 2022.3.22f1 LTS to Unity 6, complete API adaptation, package dependency updates, and deprecated API replacements, ensuring dual-platform (Windows + Android) build pipeline integrity.
- [ ] **URP Render Graph Adaptation**: Fully migrate holographic screen, SB instanced rendering, post-processing and other custom pipelines into Render Graph architecture, eliminating Compatibility Mode fallback overhead and leveraging next-gen URP's scheduling and bandwidth advantages.
- [ ] **GPU Resident Drawer & STP**: Enable GPU Resident Drawer to offload scene static culling and instanced drawing to GPU-driven rendering, evaluate STP (Spatial-Temporal Post-Processing) as a replacement for traditional anti-aliasing solutions.
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
* **[Unity](https://unity.com/)**: Thanks to the official **XR Interaction Toolkit**, **Universal Render Pipeline** and **VFX Graph**, enabling every ordinary indie developer to single-handedly create 3D immersive worlds with exceptional fluidity and visual fidelity.

---

## 📄 License
This project always embraces the open-source spirit, released under the permissive **MIT License**. Use it however you like. For details, please refer to the [LICENSE](LICENSE) file in the repository root directory.
