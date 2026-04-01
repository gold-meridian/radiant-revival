using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RadiantRevival;
using Terraria;
using Terraria.ModLoader;

namespace BlockarozToolkit.Common.Systems.AssetReloading;

internal abstract class HotAssetReloader
{
    public abstract bool QueuePath(string path);

    public abstract void OnWatcherUpdate(HotReloadContext mod, string path);
}

internal class ShaderSourceHotReloader : HotAssetReloader
{
    public override bool QueuePath(string path) => false;

    public override void OnWatcherUpdate(HotReloadContext mod, string path)
    {
        string fullPath = ToolkitUtils.NormalizePath(Path.Combine(mod.SourceFolder, path));

        try
        {
            CompileSingle(fullPath);
        }
        catch (Exception e)
        {
            Main.NewText("Shader compilation failed with exception: " + e.Message, Color.Yellow);
            ModContent.GetInstance<ModImpl>().Logger.Warn(e);
        }
    }

    public static readonly string PathToCompiler = Path.Combine(ModContent.GetInstance<ModImpl>().SourceFolder ?? "", "..", "native", "fxc.exe");

    private const string PROFILE = "fx_2_0";

    public static void CompileSingle(string shaderPath)
    {
        string file = Path.GetFileName(shaderPath);
        string directory = Path.GetDirectoryName(shaderPath) ?? Directory.GetCurrentDirectory();
        string output = shaderPath.Replace(Path.GetExtension(shaderPath), ".fxc");

        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = $"{PathToCompiler}\\fxc.exe",
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // TODO: allow our own compilation args
            Arguments = $"/T {PROFILE} \"{file}\" /Fo \"{Path.GetFileName(output)}\" /O3 /Op /D FX=1 /D __BLUEPRINT=1",
        };

        Process compiler = new Process() { StartInfo = info };
        compiler.OutputDataReceived += (_, e) => Console.WriteLine(e.Data);
        compiler.ErrorDataReceived += (_, e) =>
        {
            Console.Error.WriteLine(e.Data);
            if (!string.IsNullOrEmpty(e.Data)
             && !e.Data.Contains("Effects deprecated")
             && !e.Data.Contains("implicit truncation")
             && !e.Data.Contains("pow(f, e) will not work for"))
            {
                Main.NewText(e.Data, Main.errorColor);
            }

        };

        compiler.Start();
        compiler.BeginOutputReadLine();
        compiler.BeginErrorReadLine();

        if (!compiler.WaitForExit(30 * 1000))
        {
            Main.NewText("Compilation hung for over 30s, stopping", Main.errorColor);
            compiler.Kill();
            return;
        }

        if (compiler.ExitCode != 0)
        {
            Main.NewText($"Failed to compile {file}", Main.errorColor);
            return;
        }

        string xnb = output.Replace(".fxc", ".xnb");
        if (File.Exists(xnb))
            File.Delete(xnb);
    }
}