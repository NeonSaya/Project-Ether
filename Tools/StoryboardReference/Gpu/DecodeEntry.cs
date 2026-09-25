using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

internal static class DecodeEntry
{
    public static int Inspect()
    {
        var constructor = typeof(TextureUpload).GetConstructor(new[] { typeof(Stream) })!;
        Console.WriteLine(constructor);
        foreach (var call in MethodCalls(constructor))
            Console.WriteLine(call);
        var load = typeof(TextureUpload)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "LoadFromStream");
        Console.WriteLine("LOAD " + load);
        foreach (var call in MethodCalls(load))
            Console.WriteLine(call);
        return 0;
    }

    public static int Export(string[] args)
    {
        if (args.Length != 3)
            throw new ArgumentException("Usage: --decode INPUT_IMAGE OUTPUT_PNG");
        var input = Path.GetFullPath(args[1]);
        var output = Path.GetFullPath(args[2]);
        using var source = File.OpenRead(input);
        using var upload = new TextureUpload(source);
        var rgba = upload.Data.ToArray();
        using var image = Image.LoadPixelData<Rgba32>(rgba.AsSpan(), upload.Width, upload.Height);
        image.SaveAsPng(output);
        using var roundTripStream = File.OpenRead(output);
        using var roundTrip = new TextureUpload(roundTripStream);
        bool bytesIdentical = upload.Data.SequenceEqual(roundTrip.Data);
        if (!bytesIdentical)
            throw new InvalidOperationException(
                "Framework PNG roundtrip changed decoded RGBA bytes."
            );
        using var imageSharpDecoded = Image.Load<Rgba32>(input);
        var imageSharpPixels = new Rgba32[imageSharpDecoded.Width * imageSharpDecoded.Height];
        imageSharpDecoded.CopyPixelDataTo(imageSharpPixels);
        int differingPixels = 0,
            maxRgbDifference = 0;
        long rgbDifferenceSum = 0;
        for (int i = 0; i < rgba.Length; i++)
        {
            var a = rgba[i];
            var b = imageSharpPixels[i];
            int dr = Math.Abs(a.R - b.R),
                dg = Math.Abs(a.G - b.G),
                db = Math.Abs(a.B - b.B);
            if (dr != 0 || dg != 0 || db != 0)
                differingPixels++;
            maxRgbDifference = Math.Max(maxRgbDifference, Math.Max(dr, Math.Max(dg, db)));
            rgbDifferenceSum += dr + dg + db;
        }
        var constructor = typeof(TextureUpload).GetConstructor(new[] { typeof(Stream) })!;
        var metadata = new
        {
            input,
            output,
            decoder = "Installed osu.Framework.Graphics.Textures.TextureUpload(Stream)",
            frameworkVersion = typeof(TextureUpload).Assembly.GetName().Version!.ToString(),
            width = upload.Width,
            height = upload.Height,
            rgbaByteCount = rgba.Length * 4,
            sourceSha256 = HashFile(input),
            decodedRgbaSha256 = Convert
                .ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(rgba.AsSpan())))
                .ToLowerInvariant(),
            pngSha256 = HashFile(output),
            frameworkPngRoundtripBytesIdentical = bytesIdentical,
            comparisonAgainstImageSharpLoad = new
            {
                differingRgbPixels = differingPixels,
                maxRgbByteDifference = maxRgbDifference,
                meanAbsoluteRgbByteDifference = (double)rgbDifferenceSum / (rgba.Length * 3),
            },
            rowOrder = "TextureUpload.Data order, directly copied to ImageSharp rows; no flip or colour conversion",
            constructorCalls = MethodCalls(constructor).ToArray(),
        };
        File.WriteAllText(
            Path.ChangeExtension(output, ".json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true })
        );
        Console.WriteLine(JsonSerializer.Serialize(metadata));
        return 0;
    }

    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
    }

    private static IEnumerable<string> MethodCalls(MethodBase method)
    {
        var opcodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(o => unchecked((ushort)o.Value));
        var il = method.GetMethodBody()!.GetILAsByteArray()!;
        for (int i = 0; i < il.Length; )
        {
            ushort value = il[i++];
            if (value == 0xfe)
                value = (ushort)(0xfe00 | il[i++]);
            var code = opcodes[value];
            if (code.OperandType == OperandType.InlineMethod)
            {
                var called = method.Module.ResolveMethod(
                    BitConverter.ToInt32(il, i),
                    method.DeclaringType!.GetGenericArguments(),
                    method.IsGenericMethod ? method.GetGenericArguments() : null
                );
                yield return called!.DeclaringType!.FullName + "." + called;
            }
            i += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget
                or OperandType.ShortInlineI
                or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
        }
    }
}
