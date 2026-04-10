using Microsoft.CodeAnalysis;

namespace RadiantRevival.Core.SourceGen;

[Generator]
public sealed class DefaultAssetReferencesGenerator : AssetReferencesGenerator
{
    public override IAssetGenerator[] Generators { get; } =
    [
        new ModelGenerator(),
    ];
}
