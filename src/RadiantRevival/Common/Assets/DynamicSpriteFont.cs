using ReLogic.Content;
using ReLogic.Content.Readers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ReLogic.Graphics;
using ReLogic.Utilities;
using Terraria;

namespace RadiantRevival.Common;

internal sealed class DynamicSpriteFontReader : XnbReader, IAssetReader
{
    private const string extension = ".dsf";

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Load()
    {
        var reader = new DynamicSpriteFontReader();

        var assetReaderCollection = Main.instance.Services.Get<AssetReaderCollection>();

        if (!assetReaderCollection.TryGetReader(extension, out var currentReader) || currentReader != reader)
        {
            assetReaderCollection.RegisterReader(reader, extension);
        }
    }
#pragma warning restore CA2255

    private DynamicSpriteFontReader() : base(Main.instance.Services)
    { }

    async ValueTask<T> IAssetReader.FromStream<T>(Stream stream, MainThreadCreationContext mainThreadCtx) where T : class
    {
        if (typeof(T) != typeof(DynamicSpriteFont))
        {
            throw AssetLoadException.FromInvalidReader<DynamicSpriteFontReader, T>();
        }

        await mainThreadCtx;

        var result = base.FromStream<DynamicSpriteFont>(stream, mainThreadCtx).GetAwaiter().GetResult();

        return (result as T)!;
    }
}