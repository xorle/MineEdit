﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenMinecraft.Entities;
using OpenMinecraft.TileEntities;

namespace OpenMinecraft
{
    public class Chunk
    {

        public delegate bool FixChunkDelegate(long X, long Z, string File);
        public delegate void ChunkModifierDelegate(long X, long Z);
        public event ChunkModifierDelegate ChunkModified;
        public Dictionary<Guid, Entity> Entities = new Dictionary<Guid, Entity>();
        public Dictionary<Guid, TileEntity> TileEntities = new Dictionary<Guid, TileEntity>();
        public IChunkRenderer Renderer;

        /// <summary>
        /// Global position of chunk
        /// </summary>
        public Vector3i Position
        {
            get { return _Position; }
            set
            {
                _Position = value;
                Changed();
            }
        }
        protected Vector3i _Position;

        /// <summary>
        /// Scale of chunk (L, H, W)
        /// </summary>
        public Vector3i Size
        {
            get { return _Size; }
            set
            {
                _Size = value;
                Changed();
            }
        }
        protected Vector3i _Size;

        /// <summary>
        /// Chunk file location
        /// </summary>
        public string Filename
        {
            get { return _Filename; }
            set
            {
                _Filename = value;
                Changed();
            }
        }
        protected string _Filename;
        /// <summary>
        /// Creator of the chunk
        /// </summary>
        public string Creator
        {
            get { return _Creator; }
            set
            {
                _Creator = value;
                Changed();
            }
        }
        protected string _Creator;
        /// <summary>
        /// Time of creation.
        /// </summary>
        public DateTime CreationDate
        {
            get { return _CreationDate; }
            set
            {
                _CreationDate = value;
                Changed();
            }
        }
        protected DateTime _CreationDate;

        /// <summary>
        /// Maximum height on the map
        /// </summary>
        public int MinHeight { get; set; }
        public int MaxHeight { get; set; }
        /// <summary>
        /// Stores block positions.
        /// </summary>
        public byte[, ,] Blocks
        {
            get { return _Blocks; }
            set
            {
                _Blocks = value;
                Changed();
            }
        }

        public override string ToString()
        {
            return string.Format("[Chunk {0}]",Position);
        }
        /// <summary>
        /// Stores block data
        /// </summary>
        public byte[, ,] Data {get;set;}
        public void GetOverview(Vector3i pos, out int height, out int underwater_height, out byte block, out byte underwater_block, out int waterdepth)
        {
            height = underwater_height = 0;
            block = underwater_block = 0;
            waterdepth = 0;
            bool hf = false;
            int x = (int)pos.X;
            int z = (int)pos.Z;
            for (int y = (int)pos.Y; y > 0; --y)
            {
                byte b = Blocks[x, y, z];
                if (b == 8 || b == 9)
                {
                    if (!hf)
                    {
                        hf = true;
                        height = y;
                        block = b;
                    }
                    waterdepth++;
                    continue;
                }
                if (b != 0)
                {
                    if (!hf)
                    {
                        height = y;
                        hf = true;
                        block = b;
                    }
                    else
                    {
                        underwater_height = y;
                        underwater_block = b;
                    }
                    return;
                }
            }
        }

        public void UpdateOverview()
        {
            HeightMap = new int[Size.X, Size.Z];
            Overview = new byte[Size.X, Size.Z];
            SkyLight = new byte[Size.X, Size.Y, Size.Z];
            // Sky light
            int light = 0;
            int WaterLevel = 0;
            bool foundheight = false;
            for (int block_x = 0; block_x < 16; block_x++)
            {
                for (int block_z = 0; block_z < 16; block_z++)
                {
                    light = 15;
                    foundheight = false;
                    int blockx_blockz = (block_z << 7) + (block_x << 11);

                    for (int block_y = 127; block_y > 0; block_y--)
                    {
                        byte block = Blocks[block_x,block_y,block_z];

                        light -= OpenMinecraft.Blocks.Get(block).Stop;
                        if (light < 0)
                        {
                            light = 0;
                        }

                        // Calculate heightmap while looping this
                        if ((block != 0) && (foundheight == false))
                        {
                            HeightMap[block_x, block_z] = ((block_y == 127) ? block_y : block_y + 1);
                            Overview[block_x, block_z] = block;
                            foundheight = true;
                        }

                        SkyLight[block_x, block_y, block_z]=(byte)light;
                    }
                }
            }
        }

        protected byte[, ,] _Blocks;

        /// <summary>
        /// Block-based lighting (from torches, lava...)
        /// </summary>
        public byte[, ,] BlockLight { get; set; }
        /// <summary>
        /// Lighting from the environment (Sun)
        /// </summary>
        public byte[, ,] SkyLight { get; set; }
        /// <summary>
        /// 2D heightmap
        /// </summary>
        public int[,] HeightMap { get; protected set; }
        /// <summary>
        /// Overview (without water)
        /// </summary>
        public byte[,] Overview { get; protected set; }
        /// <summary>
        /// Overview of water depth
        /// </summary>
        public int[,] WaterDepth { get; protected set; }
        /// <summary>
        /// Heat (-1 to 1)
        /// </summary>
        public double[,] Temperatures { get; set; }
        /// <summary>
        /// Humidity (-1 to 1)
        /// </summary>
        public double[,] Humidity { get; set; }

        public IMapHandler Map
        {
            get { return _Map; }
            set
            {
                _Map = value;
                ChunkModified += _Map.ChunkModified;
            }
        }
        protected IMapHandler _Map;
        public bool Loading;

        public Chunk(IMapHandler mh) 
        {
            Map = mh;
            Overview = new byte[mh.ChunkScale.X, mh.ChunkScale.Z];
            HeightMap = new int[mh.ChunkScale.X, mh.ChunkScale.Z];
            WaterDepth = new int[mh.ChunkScale.X, mh.ChunkScale.Z];
        }

        public void Delete()
        {
            File.Delete(Filename);
        }

        public void Save()
        {
            Map.SaveChunk(this);
        }

        public byte GetBlock(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= Size.X || y >= Size.Y || z >= Size.Z)
            {
                return Map.GetBlockAt(x + (int)(Size.X * Position.X), Utils.Clamp(y,0,127), z + (int)(Size.Z * Position.Z));
                //return 0;
            }
        	return Blocks[x, y, z];
        }
        
        public Block GetBlockType(int x, int y, int z)
        {
        	return OpenMinecraft.Blocks.Get(GetBlock(x,y,z));
        }

        private void Changed()
        {
            if (Loading) return;
            if (ChunkModified != null)
                ChunkModified(Position.X / Size.X, Position.Z / Size.Z);
        }

        public byte GetBlock(Vector3i bp)
        {
            return Blocks[bp.X, bp.Y, bp.Z];
        }

        public void SetBlock(Vector3i bp, byte p)
        {
            Blocks[bp.X, bp.Y, bp.Z] = p;
        }

        public void StripLighting()
        {
            BlockLight = SkyLight = new byte[_Map.ChunkScale.X, _Map.ChunkScale.Y, _Map.ChunkScale.Z];
            HeightMap = new int[_Map.ChunkScale.X, _Map.ChunkScale.Z];
        }

        public bool TerrainPopulated { get; set; }

        public bool GeneratedByMineEdit { get; set; }

        public int Dimension { get; set; }

        public bool Cached { get; set; }
    }
}
