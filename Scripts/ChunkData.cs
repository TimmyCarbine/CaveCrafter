using System;

public sealed class ChunkData
{
    public readonly int ChunkX;
    public readonly int ChunkY;

    // === LAYERS (SoA) ===
    public readonly ushort[] Terrain;       // TileIds
    public readonly byte[] Sunlight;        // 0 - 255
    public readonly byte[] LiquidAmount;    // 0 - 255
    public readonly byte[] LiquidType;      // 0 - 255
    public readonly ushort[] Flags;         // bitflags

    // === DIRTY FLAGS ===
    public bool DirtyRender = true;
    public bool DirtyCollision = true;
    public bool DirtyLight = true;
    public bool DirtyLiquid = true;

    public ChunkData(int chunkX, int chunkY)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;

        int cellCount = WorldConstants.CHUNK_W * WorldConstants.CHUNK_H;

        Terrain = new ushort[cellCount];
        Sunlight = new byte[cellCount];
        LiquidAmount = new byte[cellCount];
        LiquidType = new byte[cellCount];
        Flags = new ushort[cellCount];

        // Default to AIR (0) already, arrays zero indexed.
    }

    public static int ToIndex(int localX, int localY)
        => localX + (localY * WorldConstants.CHUNK_W);
}