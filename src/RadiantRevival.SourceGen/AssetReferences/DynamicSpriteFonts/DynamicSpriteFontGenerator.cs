using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RadiantRevival.Core.SourceGen;

internal sealed class DynamicSpriteFontGenerator : IAssetGenerator
{
    public bool PermitsVariant(string path)
    {
        return false;
    }

    public bool Eligible(AssetPath path)
    {
        return path.RelativeOrFullPath.EndsWith(".dsf");
    }

    public string GenerateCode(string assemblyName, AssetFile asset, string indent)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{indent}public const string KEY = \"{assemblyName}/{Path.ChangeExtension(asset.Path.RelativeOrFullPath.Replace('\\', '/'), null)}\";");
        sb.AppendLine();
        sb.AppendLine($"{indent}public static global::ReLogic.Content.Asset<global::ReLogic.Graphics.DynamicSpriteFont> Asset => lazy.Value;");
        sb.AppendLine();
        sb.AppendLine($"{indent}private static readonly global::System.Lazy<global::ReLogic.Content.Asset<global::ReLogic.Graphics.DynamicSpriteFont>> lazy = new(() => global::Terraria.ModLoader.ModContent.Request<global::ReLogic.Graphics.DynamicSpriteFont>(KEY));");

        return sb.ToString().TrimEnd();
    }
}