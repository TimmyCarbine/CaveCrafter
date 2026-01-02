using System;

public sealed class WorldData
{
    public readonly int WorldWidthTiles;
    public readonly int WorldHeightTiles;

    public readonly int ChunkCountX;
    public readonly int ChunkCountY;

    private readonly ChunkData[,] _chunks;

    public WorldData(int worldWidthTiles, int worldHeightTiles)
    {
        WorldWidthTiles = worldWidthTiles;
        WorldHeightTiles = worldHeightTiles;

        ChunkCountX = (int)Math.Ceiling(worldWidthTiles / (double)WorldConstants.CHUNK_W);
        ChunkCountY = (int)Math.Ceiling(worldHeightTiles / (double)WorldConstants.CHUNK_H);

        _chunks = new ChunkData[ChunkCountX, ChunkCountY];

        for (int cx = 0; cx < ChunkCountX; cx++)
            for(int cy = 0; cy < ChunkCountY; cy++)
                _chunks[cx, cy] = new ChunkData(cx, cy);
    }

    public int WrapX(int x)
    {
        int w = WorldWidthTiles;
        int r = x % w;
        return r < 0 ? r + w : r;
    }

    public bool InBoundsY(int y) => y >= 0 && y < WorldHeightTiles;

    public ChunkData GetChunkByWorldCell(int worldX, int worldY, out int localX, out int localY)
    {
        int x = WrapX(worldX);
        localX = x % WorldConstants.CHUNK_W;

        int y = worldY;
        localY = y % WorldConstants.CHUNK_H;

        int chunkX = x / WorldConstants.CHUNK_W;
        int chunkY = y / WorldConstants.CHUNK_H;

        ChunkData chunk = _chunks[chunkX, chunkY];
        if(chunk == null)
        {
            chunk = new ChunkData(chunkX, chunkY);
            _chunks[chunkX, chunkY] = chunk;
        }
        return chunk;
    }

    public void SetTerrain(int worldX, int worldY, ushort tileId)
    {
        if (!InBoundsY(worldY)) return;

        ChunkData c = GetChunkByWorldCell(worldX, worldY, out int lx, out int ly);
        int i = ChunkData.ToIndex(lx, ly);

        if (c.Terrain[i] == tileId) return;

        c.Terrain[i] = tileId;
        c.DirtyRender = true;
        c.DirtyCollision = true;
        c.DirtyLight = true;
    }

    public ushort GetTerrain(int worldX, int worldY)
    {
        if (!InBoundsY(worldY)) return TileIds.AIR;

        ChunkData c = GetChunkByWorldCell(worldX, worldY, out int lx, out int ly);
        return c.Terrain[ChunkData.ToIndex(lx, ly)];
    }

    public ChunkData GetChunk(int chunkX, int chunkY) => _chunks[chunkX, chunkY];

    public bool IsSolid(int worldX, int worldY)
    {
        ushort t = GetTerrain(worldX, worldY);
        return t != TileIds.AIR;
    }

    public void Dig(int worldX, int worldY)
    {
        SetTerrain(worldX, worldY, TileIds.AIR);
    }

    public void Place(int worldX, int worldY, ushort tileId)
    {
        SetTerrain(worldX, worldY, tileId);
    }
}