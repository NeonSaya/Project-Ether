// Geometry adapter derived from ppy/osu DrawableStoryboardSprite.cs.
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Full upstream notice is preserved in MIT-ppy.txt alongside this file.
using System.Drawing;
using System.Globalization;
using System.Text.Json;
using osu.Framework;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Drawables;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;

internal static class CaptureEntry
{
    public static int Run(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i += 2) options.Add(args[i], args[i + 1]);
        var fixture = Path.GetFullPath(options["--input"]);
        var assets = Path.GetFullPath(options.GetValueOrDefault("--assets") ?? Path.GetDirectoryName(fixture)!);
        var output = Path.GetFullPath(options.GetValueOrDefault("--output", "capture.png"));
        var storage = Path.GetFullPath(options.GetValueOrDefault("--storage", "storage"));
        var time = double.Parse(options.GetValueOrDefault("--time", "500"), CultureInfo.InvariantCulture);
        using var host = Host.GetSuitableDesktopHost("ProjectEtherStoryboardGpuOracle", new HostOptions { PortableInstallation = true, FriendlyGameName = "Storyboard GPU oracle" });
        using var game = new CaptureGame(host, fixture, assets, output, storage, time, options.GetValueOrDefault("--hidden", "true") == "true");
        host.Run(game);
        return game.Completed ? 0 : 1;
    }
}

internal sealed class CaptureGame : Game
{
    private readonly GameHost captureHost;
    private readonly string fixture, assetRoot, output, storageRoot;
    private readonly double sampleTime;
    private readonly bool hidden;
    private Container root = null!;
    private TextureStore textureStore = null!;
    private readonly ManualClock manualClock = new ManualClock();
    private readonly FramedClock sampleClock;
    private int frames;
    private int spriteCount;
    private bool capturing;
    public bool Completed { get; private set; }

    public CaptureGame(GameHost host, string fixture, string assetRoot, string output, string storageRoot, double sampleTime, bool hidden)
    {
        captureHost = host; this.fixture = fixture; this.assetRoot = assetRoot; this.output = output; this.storageRoot = storageRoot; this.sampleTime = sampleTime; this.hidden = hidden;
        sampleClock = new FramedClock(manualClock);
    }

    protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() => new Dictionary<FrameworkSetting, object>
    {
        [FrameworkSetting.WindowedSize] = new System.Drawing.Size(1920, 1080),
        [FrameworkSetting.WindowMode] = WindowMode.Windowed,
        [FrameworkSetting.Renderer] = RendererType.Direct3D11,
        [FrameworkSetting.FrameSync] = FrameSync.VSync,
        [FrameworkSetting.VolumeUniversal] = 0.0
    };

    protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(storageRoot, host);

    protected override void LoadComplete()
    {
        base.LoadComplete();
        if (hidden) Window.Hide();
        Add(new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Depth = 1000 });
        Add(root = new Container { Size = new Vector2(640, 480), Anchor = Anchor.Centre, Origin = Anchor.Centre, Clock = sampleClock, ProcessCustomClock = true });
        textureStore = new TextureStore(captureHost.Renderer, captureHost.CreateTextureLoaderStore(new StorageBackedResourceStore(new NativeStorage(assetRoot, captureHost))), useAtlas: false, scaleAdjust: 1);
        using var stream = File.OpenRead(fixture);
        using var reader = new LineBufferedReader(stream);
        var format = 14;
        if (reader.PeekLine()?.StartsWith("osu file format v") == true)
            format = int.Parse(reader.ReadLine()[17..], CultureInfo.InvariantCulture);
        var storyboard = new LegacyStoryboardDecoder(format).Decode(reader);
        foreach (var layer in storyboard.Layers)
        {
            if (layer.Name == "Fail") continue;
            var target = new Container { RelativeSizeAxes = Axes.Both, Depth = layer.Depth };
            root.Add(target);
            foreach (var source in layer.Elements.OfType<StoryboardSprite>())
            {
                if (sampleTime < source.StartTime || sampleTime >= source.EndTimeForDisplay) continue;
                if (source.TriggerGroups.Count != 0) throw new NotSupportedException("Active trigger sprite requires gameplay events.");
                var texture = textureStore.Get(source.Path, WrapMode.ClampToEdge, WrapMode.ClampToEdge)
                    ?? throw new FileNotFoundException("Missing storyboard texture", source.Path);
                var sprite = new OracleSprite(source) { Texture = texture, Clock = sampleClock };
                source.ApplyTransforms(sprite, null!);
                sprite.ApplyTransformsAt(sampleTime);
                target.Add(sprite);
                spriteCount++;
            }
        }
        manualClock.CurrentTime = sampleTime;
        sampleClock.ProcessFrame();
        Console.WriteLine($"Ready: {spriteCount} sprites, {captureHost.RendererInfo}, window {Window.ClientSize}");
        _ = Task.Run(async () => { await Task.Delay(45000); if (!Completed) captureHost.Exit(); });
    }

    protected override void Update()
    {
        base.Update();
        if (root == null) return;
        root.Scale = new Vector2(DrawHeight / 480);
        if (++frames >= 60 && !capturing)
        {
            capturing = true;
            _ = capture();
        }
    }

    private async Task capture()
    {
        try
        {
            using var image = await captureHost.TakeScreenshotAsync();
            image.SaveAsPng(output);
            File.WriteAllText(Path.ChangeExtension(output, ".json"), JsonSerializer.Serialize(new { fixture, assetRoot, sampleTime, spriteCount, width = image.Width, height = image.Height, renderer = captureHost.RendererInfo, gameVersion = typeof(StoryboardSprite).Assembly.GetName().Version!.ToString(), frameworkVersion = typeof(Game).Assembly.GetName().Version!.ToString(), hidden, frames, textureStoreUseAtlas = false, textureScaleAdjust = 1, textureFiltering = "Linear", manualMipmaps = false, textureWrap = "ClampToEdge", spriteEdgeSmoothness = "0,0", opaqueBlackBackground = true, viewport = "640x480 centred, scale=height/480, wide viewport unclipped" }, new JsonSerializerOptions { WriteIndented = true }));
            Completed = true;
            Console.WriteLine($"Captured {image.Width}x{image.Height}: {output}");
        }
        catch (Exception error) { Console.Error.WriteLine(error); }
        finally { captureHost.Exit(); }
    }
}

internal sealed class OracleSprite : Sprite, IFlippable, IVectorScalable
{
    private bool flipH, flipV;
    private Vector2 vectorScale = Vector2.One;
    public bool FlipH { get => flipH; set { flipH = value; Invalidate(Invalidation.MiscGeometry); } }
    public bool FlipV { get => flipV; set { flipV = value; Invalidate(Invalidation.MiscGeometry); } }
    public Vector2 VectorScale { get => vectorScale; set { vectorScale = value; Invalidate(Invalidation.MiscGeometry); } }
    protected override Vector2 DrawScale => new Vector2(FlipH ? -base.DrawScale.X : base.DrawScale.X, FlipV ? -base.DrawScale.Y : base.DrawScale.Y) * VectorScale;
    public override Anchor Origin => StoryboardExtensions.AdjustOrigin(base.Origin, VectorScale, FlipH, FlipV);
    public override bool RemoveWhenNotAlive => false;
    public OracleSprite(StoryboardSprite source)
    {
        Origin = source.Origin; Position = source.InitialPosition; Name = source.Path;
        LifetimeStart = source.StartTime; LifetimeEnd = source.EndTimeForDisplay;
        typeof(Drawable).GetProperty("RemoveCompletedTransforms")!.GetSetMethod(true)!.Invoke(this, new object[] { false });
    }
    protected override void Update() { base.Update(); if (Alpha > 1) Alpha %= 1; }
}
