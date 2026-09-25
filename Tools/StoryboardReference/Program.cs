using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

try
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2)
    {
        if (i + 1 == args.Length || !args[i].StartsWith("--"))
            throw new ArgumentException("Usage: --input FILE [--times 0,250,500] [--output FILE] [--lazer DIRECTORY] [--format-version 14]");
        options.Add(args[i], args[i + 1]);
    }
    var input = Path.GetFullPath(options["--input"]);
    var times = options.GetValueOrDefault("--times", "-1,0,100,250,500,750,999,1000,1100,1250,1400,1500,1700,2000,2300")
        .Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
    if (times.Any(t => !double.IsFinite(t))) throw new ArgumentException("Sample times must be finite.");
    var lazer = Path.GetFullPath(options.GetValueOrDefault("--lazer")
        ?? Environment.GetEnvironmentVariable("LAZER_DIRECTORY")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osulazer", "current"));
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
        var path = Path.Combine(lazer, name.Name + ".dll");
        return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
    };
    var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(lazer, "osu.Game.dll"));
    var framework = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(lazer, "osu.Framework.dll"));
    var decoderType = game.GetType("osu.Game.Beatmaps.Formats.LegacyStoryboardDecoder", true)!;
    var readerType = game.GetType("osu.Game.IO.LineBufferedReader", true)!;
    var spriteType = game.GetType("osu.Game.Storyboards.StoryboardSprite", true)!;
    var drawableType = game.GetType("osu.Game.Storyboards.Drawables.DrawableStoryboardSprite", true)!;
    var retention = drawableType.GetProperty("RemoveCompletedTransforms")!;
    var apply = spriteType.GetMethod("ApplyTransforms")!.MakeGenericMethod(drawableType);
    var applyAt = drawableType.GetMethod("ApplyTransformsAt", new[] { typeof(double), typeof(bool) })!;
    var update = drawableType.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)!;
    var manualClockType = framework.GetType("osu.Framework.Timing.ManualClock", true)!;
    var framedClockType = framework.GetType("osu.Framework.Timing.FramedClock", true)!;
    var clockConstructor = framedClockType.GetConstructors().Single(c => c.GetParameters().Length > 0 && c.GetParameters()[0].ParameterType.IsAssignableFrom(manualClockType));

    var format = int.Parse(options.GetValueOrDefault("--format-version", "14"), CultureInfo.InvariantCulture);
    using var inputStream = File.OpenRead(input);
    using var reader = (IDisposable)Activator.CreateInstance(readerType, inputStream, false)!;
    var firstLine = (string?)readerType.GetMethod("PeekLine")!.Invoke(reader, null);
    if (firstLine?.StartsWith("osu file format v", StringComparison.Ordinal) == true)
    {
        format = int.Parse(firstLine[17..], CultureInfo.InvariantCulture);
        readerType.GetMethod("ReadLine")!.Invoke(reader, null);
    }
    var decoder = Activator.CreateInstance(decoderType, format)!;
    var storyboard = decoderType.GetMethod("Decode")!.Invoke(decoder, new object[] { reader, Array.CreateInstance(readerType, 0) })!;
    var sprites = new List<object>();
    var skipped = new List<string>();
    var index = 0;
    foreach (var layer in Items(Get(storyboard, "Layers")))
    foreach (var element in Items(Get(layer, "Elements")))
    {
        if (!spriteType.IsInstanceOfType(element))
        {
            skipped.Add(element.GetType().FullName!);
            continue;
        }
        if (Items(Get(element, "TriggerGroups")).Any())
            throw new NotSupportedException("Trigger groups require gameplay events and are outside this deterministic oracle.");
        var samples = new List<object>();
        foreach (var time in times)
        {
            // 每个采样点都重新创建 drawable，无论采样顺序如何（任意或倒序），都不会丢失已完成的 transform。
            using var drawable = (IDisposable)Activator.CreateInstance(drawableType, element)!;
            retention.GetSetMethod(true)!.Invoke(drawable, new object[] { false });
            var manualClock = Activator.CreateInstance(manualClockType)!;
            var clockArguments = clockConstructor.GetParameters().Select((p, i) => i == 0 ? manualClock : p.DefaultValue).ToArray();
            var clock = clockConstructor.Invoke(clockArguments);
            drawableType.GetProperty("Clock")!.SetValue(drawable, clock);
            apply.Invoke(element, new object?[] { drawable, null });
            applyAt.Invoke(drawable, new object[] { time, false });
            var alphaBeforeUpdate = Number(Get(drawable, "Alpha"));
            // 只调用这个 override（不加载依赖、不运行时钟循环、不使用 host/GPU，也不调用 UpdateSubTree）。
            update.Invoke(drawable, null);
            var colour = Get(Get(Get(drawable, "Colour"), "TopLeft"), "SRGB");
            var blend = Get(drawable, "Blending");
            var declaredBlend = Blend(blend);
            blend.GetType().GetMethod("ApplyDefaultToInherited")!.Invoke(blend, null);
            samples.Add(new
            {
                time,
                position = Vector(Get(drawable, "Position")),
                scale = Vector(Get(drawable, "Scale")),
                vectorScale = Vector(Get(drawable, "VectorScale")),
                rotationDegrees = Number(Get(drawable, "Rotation")),
                alphaBeforeUpdate,
                alpha = Number(Get(drawable, "Alpha")),
                colour = new { r = Number(Get(colour, "R")), g = Number(Get(colour, "G")), b = Number(Get(colour, "B")), a = Number(Get(colour, "A")) },
                flipH = (bool)Get(drawable, "FlipH"),
                flipV = (bool)Get(drawable, "FlipV"),
                origin = Get(drawable, "Origin").ToString(),
                declaredBlending = declaredBlend,
                blending = Blend(blend)
            });
        }
        sprites.Add(new
        {
            index = index++,
            layer = Get(layer, "Name"),
            layerDepth = Get(layer, "Depth"),
            path = Get(element, "Path"),
            sourceType = element.GetType().Name,
            startTime = Number(Get(element, "StartTime")),
            endTime = Number(Get(element, "EndTime")),
            endTimeForDisplay = Number(Get(element, "EndTimeForDisplay")),
            samples
        });
    }
    var result = new
    {
        schemaVersion = 1,
        gameVersion = game.GetName().Version!.ToString(),
        frameworkVersion = framework.GetName().Version!.ToString(),
        gameAssemblySha256 = Hash(game.Location),
        frameworkAssemblySha256 = Hash(framework.Location),
        input,
        inputSha256 = Hash(input),
        formatVersion = format,
        evaluation = "Actual LegacyStoryboardDecoder -> fresh DrawableStoryboardSprite -> StoryboardSprite.ApplyTransforms -> ApplyTransformsAt -> protected Update override only",
        limitations = new[] { "No texture loading, animation-frame selection, rendering, parent hierarchy, masks or gameplay triggers.", "Colour is local encoded sRGB; alphaBeforeUpdate preserves raw transform alpha; alpha includes actual sprite overflow correction.", "Blending inheritance is resolved to framework defaults without a parent. Values outside sprite lifetime remain available for comparison, not visibility." },
        skippedElementTypes = skipped,
        sprites
    };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
    if (options.TryGetValue("--output", out var output))
    {
        File.WriteAllText(output, json + Environment.NewLine, new System.Text.UTF8Encoding(false));
        Console.Error.WriteLine($"Wrote {sprites.Count} sprites x {times.Length} samples to {Path.GetFullPath(output)}");
    }
    else Console.WriteLine(json);
    return 0;
}
catch (Exception error)
{
    while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
    Console.Error.WriteLine(error);
    return 1;
}

static object Get(object instance, string name)
{
    var type = instance.GetType();
    return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance)
        ?? type.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance)
        ?? throw new MissingMemberException(type.FullName, name);
}
static IEnumerable<object> Items(object value) => ((IEnumerable)value).Cast<object>();
static double Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
static object Vector(object value) => new { x = Number(Get(value, "X")), y = Number(Get(value, "Y")) };
static Dictionary<string, string> Blend(object value) => new[] { "Source", "Destination", "SourceAlpha", "DestinationAlpha", "RGBEquation", "AlphaEquation" }
    .ToDictionary(name => name == "RGBEquation" ? "rgbEquation" : char.ToLowerInvariant(name[0]) + name[1..], name => Get(value, name).ToString()!);
static string Hash(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}
