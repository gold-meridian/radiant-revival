using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Content.Readers;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

internal sealed class ObjModel : IDisposable
{
    private Mesh[] meshes = [];

    public void Dispose()
    {
        for (var i = 0; i < meshes.Length; i++)
        {
            meshes[i].Dispose();
        }

        Array.Clear(meshes);
    }

    public static ObjModel Create(Stream stream)
    {
        var model = new ObjModel();

        var device = Main.graphics.GraphicsDevice;

        var meshes = new List<Mesh>();

        var vertices = new List<VertexPositionNormalTexture>();
        var positions = new List<Vector3>();
        var textureCoords = new List<Vector2>();
        var normals = new List<Vector3>();
        var indices = new List<int>();

        var meshName = string.Empty;
        var verticesStart = 0;
        var indicesStart = 0;

        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } text)
        {
            var segments = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                continue;
            }

            switch (segments[0])
            {
                case "o":
                {
                    ParseObject(segments);
                    break;
                }
                case "v":
                {
                    ParseVertexPosition(segments);
                    break;
                }
                case "vt":
                {
                    ParseVertexTexture(segments);
                    break;
                }
                case "vn":
                {
                    ParseVertexNormal(segments);
                    break;
                }
                case "f":
                {
                    ParseFace(segments);
                    break;
                }
            }
        }

        AddMesh();

        if (meshes.Count > 0)
        {
            model.meshes = meshes.ToArray();
        }
        else
        {
            throw new InvalidDataException($"{nameof(ObjModel)}: Model did not contain a mesh!");
        }

        return model;

        void AddMesh()
        {
            if (vertices.Count <= 3 || meshName == string.Empty)
            {
                return;
            }

            var vBuffer = new VertexBuffer(device, typeof(VertexPositionNormalTexture), vertices.Count - verticesStart, BufferUsage.None);
            vBuffer.SetData(vertices.ToArray(), verticesStart, vertices.Count - verticesStart);

            var iBuffer = new IndexBuffer(device, typeof(int), indices.Count - indicesStart, BufferUsage.None);
            iBuffer.SetData(indices.ToArray(), indicesStart, indices.Count - indicesStart);

            meshes.Add(new Mesh(meshName, vBuffer, iBuffer));
        }

        void ParseObject(string[] segments)
        {
            if (segments.Length < 2)
            {
                return;
            }

            AddMesh();

            meshName = segments[1];
            verticesStart = vertices.Count;
            indicesStart = indices.Count;
        }

        void ParseVertexPosition(string[] segments)
        {
            if (segments.Length != 4)
            {
                return;
            }

            positions.Add(
                new Vector3(
                    float.Parse(segments[1]),
                    float.Parse(segments[2]),
                    float.Parse(segments[3])
                )
            );
        }

        void ParseVertexTexture(string[] segments)
        {
            if (segments.Length != 3)
            {
                return;
            }

            textureCoords.Add(
                new Vector2(
                    float.Parse(segments[1]),
                    float.Parse(segments[2])
                )
            );
        }

        void ParseVertexNormal(string[] segments)
        {
            if (segments.Length != 4)
            {
                return;
            }

            normals.Add(
                new Vector3(
                    float.Parse(segments[1]),
                    float.Parse(segments[2]),
                    float.Parse(segments[3])
                )
            );
        }

        void ParseFace(string[] segments)
        {
            var start = vertices.Count;

            for (var i = 1; i < segments.Length; i++)
            {
                VertexPositionNormalTexture vertex = new();

                var components = segments[i].Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (components.Length != 3)
                {
                    continue;
                }

                vertex.Position = positions[int.Parse(components[0]) - 1];

                var coord = textureCoords[int.Parse(components[1]) - 1];
                coord.Y = 1 - coord.Y;

                vertex.TextureCoordinate = coord;

                var normal = normals[int.Parse(components[2]) - 1];
                vertex.Normal = normal;

                vertices.Add(vertex);
            }

            switch (segments.Length - 1)
            {
                case 3:
                {
                    indices.AddRange(
                        [start, start + 2, start + 1]
                    );
                    break;
                }
                case 4:
                {
                    indices.AddRange(
                        [
                            start, start + 2, start + 1,
                            start + 2, start + 3, start + 1,
                        ]
                    );
                    break;
                }
            }
        }
    }

    public void Draw(GraphicsDevice device, string name)
    {
        var i = Array.FindIndex(meshes, m => m.Name == name);

        if (i != -1)
        {
            Draw(device, i);
        }
    }

    public void Draw(GraphicsDevice device, int i = 0)
    {
        var mesh = meshes[i];

        device.Indices = mesh.Indices;
        device.SetVertexBuffer(mesh.Vertices);
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.Vertices.VertexCount, 0, mesh.Indices.IndexCount / 3);
        device.SetVertexBuffer(null);
        device.Indices = null;
    }

    private readonly record struct Mesh(string Name, VertexBuffer Vertices, IndexBuffer Indices) : IDisposable
    {
        public void Dispose()
        {
            Vertices.Dispose();
            Indices.Dispose();
        }
    }
}

[Autoload(false)]
internal sealed class ObjModelReader : IAssetReader, ILoadable
{
    private const string extension = ".obj";

    public void Load(Mod mod)
    {
        var assetReaderCollection = Main.instance.Services.Get<AssetReaderCollection>();

        if (!assetReaderCollection.TryGetReader(extension, out var reader) || reader != this)
        {
            assetReaderCollection.RegisterReader(this, extension);
        }
    }

    public void Unload() { }

    public async ValueTask<T> FromStream<T>(Stream stream, MainThreadCreationContext mainThreadCtx) where T : class
    {
        if (typeof(T) != typeof(ObjModel))
        {
            throw AssetLoadException.FromInvalidReader<ObjModelReader, T>();
        }

        await mainThreadCtx;

        var result = ObjModel.Create(stream);

        return (result as T)!;
    }
}
