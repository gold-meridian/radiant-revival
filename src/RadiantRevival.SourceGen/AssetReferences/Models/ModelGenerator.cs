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

        return sb.ToString().TrimEnd();
    }
}