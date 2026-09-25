using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

internal static class Program
{
    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string path);

    [STAThread]
    public static int Main(string[] args)
    {
        var lazer = Environment.GetEnvironmentVariable("LAZER_DIRECTORY") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osulazer", "current");
        SetDllDirectory(lazer);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(lazer, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        try
        {
            if (args.Length > 0 && args[0] == "--inspect-decode") return DecodeEntry.Inspect();
            if (args.Length > 0 && args[0] == "--decode") return DecodeEntry.Export(args);
            return Run(args);
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args) => CaptureEntry.Run(args);
}
