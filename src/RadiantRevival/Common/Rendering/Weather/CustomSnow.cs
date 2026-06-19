using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Common.Rendering.Sky;
using RadiantRevival.Core;
using System;
using System.Runtime.InteropServices;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using BitOperations = System.Numerics.BitOperations;

namespace RadiantRevival.Common.Rendering.Weather;

/// <summary>
///     The system responsible for the creation and
///     management of custom snow weather particles.
/// </summary>
public static class CustomSnow
{
    private sealed class Data : IStatic<Data>
    {
        /// <summary>
        ///     The vertex buffer responsible for all snow particles.
        /// </summary>
        public required DynamicVertexBuffer Vertices
        {
            get;
            init;
        }

        /// <summary>
        ///     The index buffer responsible for all snow particles.
        /// </summary>
        public required IndexBuffer Indices
        {
            get;
            init;
        }

        /// <summary>
        ///     The render target responsible for the containment
        ///     of snowflakes on screen.
        /// </summary>
        public required RenderTargetLease SnowflakeTarget
        {
            get;
            init;
        }

        /// <summary>
        ///     The render target responsible for the containment
        ///     of snowflakes normals on screen.
        /// </summary>
        public required RenderTargetLease SnowflakeNormalTarget
        {
            get;
            init;
        }

        /// <summary>
        ///     The render target responsible for the containment
        ///     of snowflakes position data on screen.
        /// </summary>
        public required RenderTargetLease SnowflakePositionTarget
        {
            get;
            init;
        }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(() =>
            {
                var vertices = new DynamicVertexBuffer(Main.instance.GraphicsDevice, Vertex.VERTEX_DECLARATION, MAX_SNOWFLAKES * 4, BufferUsage.None);
                var indices = new IndexBuffer(Main.instance.GraphicsDevice, IndexElementSize.ThirtyTwoBits, MAX_SNOWFLAKES * 6, BufferUsage.None);
                var indicesSubmission = new int[MAX_SNOWFLAKES * 6];
                for (var i = 0; i < MAX_SNOWFLAKES; i++)
                {
                    var bufferIndex = i * 6;
                    var vertexIndex = i * 4;
                    indicesSubmission[bufferIndex] = vertexIndex;
                    indicesSubmission[bufferIndex + 1] = vertexIndex + 1;
                    indicesSubmission[bufferIndex + 2] = vertexIndex + 2;
                    indicesSubmission[bufferIndex + 3] = vertexIndex + 2;
                    indicesSubmission[bufferIndex + 4] = vertexIndex + 3;
                    indicesSubmission[bufferIndex + 5] = vertexIndex;
                }
                indices.SetData(indicesSubmission);

                return new Data
                {
                    Vertices = vertices,
                    Indices = indices,
                    SnowflakeTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice),
                    SnowflakeNormalTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, RenderTargetDescriptor.Default with
                    {
                        Format = SurfaceFormat.Vector4
                    }),
                    SnowflakePositionTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, RenderTargetDescriptor.Default with
                    {
                        Format = SurfaceFormat.Vector4
                    }),
                };
            }).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(() =>
            {
                data.Vertices.Dispose();
                data.Indices.Dispose();
                data.SnowflakeTarget.Dispose();
                data.SnowflakeNormalTarget.Dispose();
                data.SnowflakePositionTarget.Dispose();
            });
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Vertex(Vector3 position, Color color, Vector4 rotationQuaternion, Vector2 textureCoordinate) : IVertexType
    {
        public Vector3 Position = position;

        public Color Color = color;

        public Vector4 RotationQuaternion = rotationQuaternion;

        public Vector2 TextureCoordinate = textureCoordinate;

        public static readonly VertexDeclaration VERTEX_DECLARATION;

        readonly VertexDeclaration IVertexType.VertexDeclaration => VERTEX_DECLARATION;

        static Vertex()
        {
            VERTEX_DECLARATION = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(32, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1)
            );
        }
    }

    /// <summary>
    ///     Whether any snowflakes are active this frame.
    /// </summary>
    private static bool anySnowflakesActive;

    /// <summary>
    ///     A consistent CPU-bound cache for the holding
    ///     of vertex data for holding snowflakes.
    /// </summary>
    private static readonly Vertex[] vertex_cache = new Vertex[MAX_SNOWFLAKES * 4];

    /// <summary>
    ///     The internal binary mappings that
    ///     determine whether snowflakes are
    ///     active or not.
    /// </summary>
    private static readonly ulong[] activity_bit_chunks = new ulong[(int)Math.Ceiling((double)MAX_SNOWFLAKES / bits_per_chunk)];

    /// <summary>
    ///     The set of all snow particles maintained.
    /// </summary>
    private static readonly SnowParticle[] snowflake_particles = InitializeParticleArray();

    /// <summary>
    ///     The amount of bits contained within each
    ///     chunk in the <see cref="activity_bit_chunks"/> array.
    /// </summary>
    private const int bits_per_chunk = sizeof(ulong) * 8;

    /// <summary>
    ///     The active view-projection matrix in use by this
    ///     system.
    /// </summary>
    internal static Matrix ActiveViewProjection
    {
        get;
        private set;
    } = Matrix.Identity;

    /// <summary>
    ///     The maximum amount of snowflakes supported
    ///     by this system.
    /// </summary>
    public const int MAX_SNOWFLAKES = 4096;

    private static SnowParticle[] InitializeParticleArray()
    {
        var particles = new SnowParticle[MAX_SNOWFLAKES];
        for (var i = 0; i < particles.Length; i++)
            particles[i] = new SnowParticle();

        return particles;
    }

    [OnLoad]
    private static void Load()
    {
        On_Main.DrawDust += Render;
        On_Main.snowing += CreateSnowParticles;
    }

    private static void CreateSnowParticles(On_Main.orig_snowing orig)
    {
        if (Main.remixWorld)
            return;

        if (Main.gamePaused || Main.SceneMetrics.SnowTileCount <= 0)
            return;

        var snowfallIntensity = MathF.Pow(Main.SceneMetrics.SnowTileCount / (float)SceneMetrics.SnowTileMax, 2.4f);
        var particleCount = (int)MathF.Round(snowfallIntensity * 3f + MathF.Abs(Main.windSpeedCurrent) * 7f + Main.cloudAlpha * 8f) + 2;
        var screenTop = new Vector3(Main.screenPosition + new Vector2(Main.screenWidth * 0.5f - Main.windSpeedCurrent * 900f, 0f), 0f);
        for (var i = 0; i < particleCount; i++)
        {
            var dx = Main.rand.NextFloatDirection() * 1000f;
            var dy = Main.rand.NextFloat(-200f, 200f);
            var z = Main.rand.NextFloat(300f, 700f);
            var spawnPosition = screenTop + new Vector3(dx, dy, z);
            var scale = Main.rand.NextFloat(2f, 4f);
            var velocity = new Vector3(Main.rand.NextFloatDirection() * 1.6f, Main.rand.NextFloat(1f, 1.5f), Main.rand.NextFloatDirection() * 2.5f) / scale * 8f;
            velocity.X += Main.windSpeedCurrent * Main.rand.NextFloat(23.5f, 39.5f);
            velocity.Y += MathF.Abs(Main.windSpeedCurrent) * Main.rand.NextFloat(25f);

            var rotation = Quaternion.CreateFromYawPitchRoll(Main.rand.NextFloat(MathF.Tau), Main.rand.NextFloat(MathF.Tau), Main.rand.NextFloat(MathF.Tau));
            var lifetime = (int)(scale * 50f) + Main.rand.Next(40, 75);

            TryCreate(spawnPosition, velocity, Vector2.One * scale, rotation, lifetime);
        }
    }

    [ModSystemHooks.ClearWorld]
    private static void OnClearWorld()
    {
        for (var i = 0; i < activity_bit_chunks.Length; i++)
            activity_bit_chunks[i] = 0uL;
    }

    [ModSystemHooks.PostUpdateDusts]
    private static void Update()
    {
        Main.windSpeedCurrent = 0.96f;
        Main.maxRaining = 0.7f;
        for (int i = 0; i < 50; i++)
            Main.npc[i].active = false;

        anySnowflakesActive = false;

        for (var i = 0; i < activity_bit_chunks.Length; i++)
        {
            var bits = activity_bit_chunks[i];
            while (bits != 0)
            {
                var bitIndex = BitOperations.TrailingZeroCount(bits);
                var snowflakeIndex = i * bits_per_chunk + bitIndex;

                snowflake_particles[snowflakeIndex].Update();

                // Flip off the activity bit if this snowflake
                // needs to restore honor to its ancestors and die.
                if (snowflake_particles[snowflakeIndex].KillIfNecessary())
                    activity_bit_chunks[i] ^= 1uL << bitIndex;
                else
                    anySnowflakesActive = true;

                // Clear the lowest set bit, gradually
                // whittling down until all active snowflakes
                // are accounted for.
                bits &= bits - 1;
            }
        }
    }

    /// <summary>
    ///     Attempts to find and return the first
    ///     available index for a new snowflake.
    /// </summary>
    private static int? SelectFirstAvailableIndex()
    {
        for (var i = 0; i < activity_bit_chunks.Length; i++)
        {
            var offset = BitOperations.TrailingZeroCount(~activity_bit_chunks[i]);
            var allBitsAreOccupied = offset == bits_per_chunk;
            if (allBitsAreOccupied)
                continue;

            return offset + i * bits_per_chunk;
        }

        return null;
    }

    /// <summary>
    ///     Tries to create a new snow particle in the world.
    /// </summary>
    /// <param name="spawnPosition">The position to spawn the snow particle.</param>
    /// <param name="velocity">The velocity to spawn the snow particle with.</param>
    /// <param name="scale">The scale of the snow particle.</param>
    /// <param name="rotation">The starting rotation of the snow particle.</param>
    /// <param name="lifetime">How long the snow particle should exist for, in frames.</param>
    public static void TryCreate(Vector3 spawnPosition, Vector3 velocity, Vector2 scale, Quaternion rotation, int lifetime)
    {
        var index = SelectFirstAvailableIndex();
        if (!index.HasValue)
            return;

        var snowParticle = snowflake_particles[index.Value];
        snowParticle.Position = spawnPosition;
        snowParticle.Velocity = velocity;
        snowParticle.Scale = scale;
        snowParticle.Rotation = rotation;
        snowParticle.Lifetime = lifetime;

        var chunkIndex = index.Value / bits_per_chunk;
        var bitIndex = index.Value % bits_per_chunk;
        activity_bit_chunks[chunkIndex] ^= 1uL << bitIndex;
    }

    private static int PrepareRender()
    {
        var snowflakeCounter = 0;
        for (var i = 0; i < activity_bit_chunks.Length; i++)
        {
            var bits = activity_bit_chunks[i];
            while (bits != 0)
            {
                var bitIndex = BitOperations.TrailingZeroCount(bits);
                var snowflakeIndex = i * bits_per_chunk + bitIndex;

                snowflake_particles[snowflakeIndex].PrepareRender(new Span<Vertex>(vertex_cache, snowflakeCounter * 4, 4));
                anySnowflakesActive = true;
                snowflakeCounter++;

                // Clear the lowest set bit, gradually
                // whittling down until all active snowflakes
                // are accounted for.
                bits &= bits - 1;
            }
        }

        Data.Instance.Vertices.SetData(vertex_cache, 0, snowflakeCounter * 4);
        return snowflakeCounter;
    }

    private static void RenderWithOverlayTexture(int snowflakeCount)
    {
        var gd = Main.instance.GraphicsDevice;
        var previousBindings = gd.GetRenderTargets();
        gd.SetRenderTargets(
        [
            Data.Instance.SnowflakeTarget.Target,
            Data.Instance.SnowflakeNormalTarget.Target,
            Data.Instance.SnowflakePositionTarget.Target,
        ]);

        var fov = MathF.PI * 0.5f;
        var cameraSize = Main.ScreenSize.ToVector2();
        var cameraPosition = new Vector3(Main.screenPosition + cameraSize * 0.5f, 0f);
        var zoom = Matrix.CreateScale(Main.GameViewMatrix.Zoom.X, Main.GameViewMatrix.Zoom.Y, 1f);
        var view = Matrix.CreateLookAt(cameraPosition - Vector3.UnitZ, cameraPosition, -Vector3.UnitY) * zoom;
        var aspectRatio = Main.instance.GraphicsDevice.Viewport.AspectRatio;
        var projection = Matrix.CreatePerspectiveFieldOfView(fov, aspectRatio, 0.1f, 5000f);
        ActiveViewProjection = view * projection;

        var shader = AssetReferences.Assets.Weather.Snow.SnowflakeShader.CreateAutoloadPass();
        shader.Parameters.viewProjectionMatrix = ActiveViewProjection;
        shader.Parameters.baseTexture = new HlslSampler2D
        {
            Texture = Assets.Weather.Snow.Snowflake.Asset.Value,
            Sampler = SamplerState.LinearClamp
        };
        shader.Parameters.normalTexture = new HlslSampler2D
        {
            Texture = Assets.Weather.Snow.SnowflakeNormal.Asset.Value,
            Sampler = SamplerState.LinearClamp
        };
        shader.Apply();

        gd.Indices = Data.Instance.Indices;
        gd.SetVertexBuffer(Data.Instance.Vertices);
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, snowflakeCount * 4, 0, snowflakeCount * 2);

        gd.SetRenderTargets(previousBindings);
    }

    private static void Render(On_Main.orig_DrawDust orig, Main self)
    {
        orig(self);

        if (!anySnowflakesActive)
            return;

        var snowflakeCount = PrepareRender();
        RenderWithOverlayTexture(snowflakeCount);

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.EffectMatrix);

        var shader = Assets.Weather.Snow.SnowflakeReflectionShader.CreateAutoloadPass();
        shader.Parameters.snowNormalTexture = new HlslSampler2D
        {
            Texture = Data.Instance.SnowflakeNormalTarget.Target,
            Sampler = SamplerState.LinearClamp
        };
        shader.Parameters.reflectionColorTexture = new HlslSampler2D
        {
            Texture = AuroraReplacement.auroraLease?.Target ?? TextureAssets.BlackTile.Value,
            Sampler = SamplerState.LinearClamp
        };
        shader.Parameters.reflectionDepthTexture = new HlslSampler2D
        {
            Texture = AuroraReplacement.depthLease?.Target ?? TextureAssets.BlackTile.Value,
            Sampler = SamplerState.LinearClamp
        };
        shader.Parameters.lightMapTexture = new HlslSampler2D
        {
            Texture = LightingEngine.TileSpaceBuffer.Target,
            Sampler = SamplerState.LinearClamp
        };
        shader.Parameters.positionTexture = new HlslSampler2D
        {
            Texture = Data.Instance.SnowflakePositionTarget.Target,
            Sampler = SamplerState.LinearClamp
        };
        shader.Parameters.reflectivityInterpolant = 0.72f;
        shader.Parameters.zoom = Main.GameViewMatrix.Zoom;
        shader.Apply();

        var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        Main.spriteBatch.Draw(Data.Instance.SnowflakeTarget.Target, viewportArea, Color.White);

        Main.spriteBatch.End();
    }
}
