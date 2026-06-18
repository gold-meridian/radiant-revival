using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RadiantRevival.Core.SourceGen;

internal sealed class ModelGenerator : IAssetGenerator
{
    public bool PermitsVariant(string path)
    {
        return false;
    }

    public bool Eligible(AssetPath path)
    {
        return path.RelativeOrFullPath.EndsWith(".obj");
    }

    public string GenerateCode(string assemblyName, AssetFile asset, string indent)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{indent}public const string KEY = \"{assemblyName}/{Path.ChangeExtension(asset.Path.RelativeOrFullPath.Replace('\\', '/'), null)}\";");
        sb.AppendLine();
        sb.AppendLine($"{indent}public static global::ReLogic.Content.Asset<global::{assemblyName}.Common.ObjModel> Asset => lazy.Value;");
        sb.AppendLine();
        sb.AppendLine($"{indent}private static readonly global::System.Lazy<global::ReLogic.Content.Asset<global::{assemblyName}.Common.ObjModel>> lazy = new(() => global::Terraria.ModLoader.ModContent.Request<global::{assemblyName}.Common.ObjModel>(KEY));");

        string[] text;

        try
        {
            using var fs = File.OpenRead(asset.Path.FullPath);
            using var reader = new StreamReader(fs);

            text = ReadObjectNames(reader);
        }
        catch (Exception e)
        {
            return $"{indent}#error Failed to parse Obj file \"{asset.Path.FullPath}\": {e.Message}";
        }

        for (var i = 0; i < text.Length; i++)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}public static void Draw{text[i]}()");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    Asset.Wait();");
            sb.AppendLine();
            sb.AppendLine($"{indent}    Asset.Value.Draw({i});");
            sb.AppendLine($"{indent}}}");
        }

        return sb.ToString().TrimEnd();

        static string[] ReadObjectNames(StreamReader reader)
        {
            var names = new List<string>();

            while (reader.ReadLine() is { } text)
            {
                if (text.Length == 0 || text[0] != 'o')
                {
                    continue;
                }

                var name = text.Substring(2);

                name = NameSanitizer.ToValidIdentifier(name);

                names.Add(name);
            }

            return names.ToArray();
        }
    }
}