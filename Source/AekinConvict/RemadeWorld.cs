using Microsoft.Xna.Framework;
using OTAPI.Tile;
using System;
using System.Runtime.InteropServices;
using Terraria;
using Terraria.DataStructures;

namespace AekinConvict.RemadeWorld
{
	public sealed class AekinTile : ITile
	{
		public int collisionType
		{
			get
			{
				bool flag = !this.active();
				int result;
				if (flag)
				{
					result = 0;
				}
				else
				{
					bool flag2 = this.halfBrick();
					if (flag2)
					{
						result = 2;
					}
					else
					{
						bool flag3 = this.slope() > 0;
						if (flag3)
						{
							result = (int)(2 + this.slope());
						}
						else
						{
							bool flag4 = Main.tileSolid[(int)this.type] && !Main.tileSolidTop[(int)this.type];
							if (flag4)
							{
								result = 1;
							}
							else
							{
								result = -1;
							}
						}
					}
				}
				return result;
			}
		}

		public ushort type
		{
			get
			{
				return this.data[this.offset].type;
			}
			set
			{
				this.data[this.offset].type = value;
			}
		}

		public ushort wall
		{
			get
			{
				return this.data[this.offset].wall;
			}
			set
			{
				this.data[this.offset].wall = value;
			}
		}

		public byte liquid
		{
			get
			{
				return this.data[this.offset].liquid;
			}
			set
			{
				this.data[this.offset].liquid = value;
			}
		}

		public short sTileHeader
		{
			get
			{
				return this.data[this.offset].sTileHeader;
			}
			set
			{
				this.data[this.offset].sTileHeader = value;
			}
		}

		public byte bTileHeader
		{
			get
			{
				return this.data[this.offset].bTileHeader;
			}
			set
			{
				this.data[this.offset].bTileHeader = value;
			}
		}

		public byte bTileHeader2
		{
			get
			{
				return this.data[this.offset].bTileHeader2;
			}
			set
			{
				this.data[this.offset].bTileHeader2 = value;
			}
		}

		public byte bTileHeader3
		{
			get
			{
				return this.data[this.offset].bTileHeader3;
			}
			set
			{
				this.data[this.offset].bTileHeader3 = value;
			}
		}

		public short frameX
		{
			get
			{
				return this.data[this.offset].frameX;
			}
			set
			{
				this.data[this.offset].frameX = value;
			}
		}

		public short frameY
		{
			get
			{
				return this.data[this.offset].frameY;
			}
			set
			{
				this.data[this.offset].frameY = value;
			}
		}

		public AekinTile(AekinTileData[] data, int x, int y)
		{
			this.offset = Main.maxTilesY * x + y;
			this.data = data;
		}

		public void CopyFrom(ITile from)
		{
			this.type = from.type;
			this.wall = from.wall;
			this.liquid = from.liquid;
			this.sTileHeader = from.sTileHeader;
			this.bTileHeader = from.bTileHeader;
			this.bTileHeader2 = from.bTileHeader2;
			this.bTileHeader3 = from.bTileHeader3;
			this.frameX = from.frameX;
			this.frameY = from.frameY;
		}

		public new string ToString()
		{
			return string.Format("Tile Type:{0} Active:{1} Wall:{2} Slope:{3} fX:{4} fY:{5}", new object[]
			{
				this.type,
				this.active(),
				this.wall,
				this.slope(),
				this.frameX,
				this.frameY
			});
		}

		public object Clone()
		{
			return base.MemberwiseClone();
		}

		public void ClearEverything()
		{
			this.type = 0;
			this.wall = 0;
			this.liquid = 0;
			this.sTileHeader = 0;
			this.bTileHeader = 0;
			this.bTileHeader2 = 0;
			this.bTileHeader3 = 0;
			this.frameX = 0;
			this.frameY = 0;
		}

		public void ClearTile()
		{
			this.slope(0);
			this.halfBrick(false);
			this.active(false);
			this.inActive(false);
		}

		public bool isTheSameAs(ITile compTile)
		{
			bool flag = compTile == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = this.sTileHeader != compTile.sTileHeader;
				if (flag2)
				{
					result = false;
				}
				else
				{
					bool flag3 = this.active();
					if (flag3)
					{
						bool flag4 = this.type != compTile.type;
						if (flag4)
						{
							return false;
						}
						bool flag5 = Main.tileFrameImportant[(int)this.type] && (this.frameX != compTile.frameX || this.frameY != compTile.frameY);
						if (flag5)
						{
							return false;
						}
					}
					bool flag6 = this.wall != compTile.wall || this.liquid != compTile.liquid;
					if (flag6)
					{
						result = false;
					}
					else
					{
						bool flag7 = compTile.liquid == 0;
						if (flag7)
						{
							bool flag8 = this.wallColor() != compTile.wallColor();
							if (flag8)
							{
								return false;
							}
							bool flag9 = this.wire4() != compTile.wire4();
							if (flag9)
							{
								return false;
							}
						}
						else
						{
							bool flag10 = this.bTileHeader != compTile.bTileHeader;
							if (flag10)
							{
								return false;
							}
						}
						result = true;
					}
				}
			}
			return result;
		}

		public int blockType()
		{
			bool flag = this.halfBrick();
			int result;
			if (flag)
			{
				result = 1;
			}
			else
			{
				int num = (int)this.slope();
				bool flag2 = num > 0;
				if (flag2)
				{
					num++;
				}
				result = num;
			}
			return result;
		}

		public void liquidType(int liquidType)
		{
			switch (liquidType)
			{
				case 0:
					this.bTileHeader &= 159;
					break;
				case 1:
					this.lava(true);
					break;
				case 2:
					this.honey(true);
					break;
			}
		}

		public byte liquidType()
		{
			return (byte)((this.bTileHeader & 96) >> 5);
		}

		public bool nactive()
		{
			return (this.sTileHeader & 96) == 32;
		}

		public void ResetToType(ushort type)
		{
			this.liquid = 0;
			this.sTileHeader = 32;
			this.bTileHeader = 0;
			this.bTileHeader2 = 0;
			this.bTileHeader3 = 0;
			this.frameX = 0;
			this.frameY = 0;
			this.type = type;
		}

		public void ClearMetadata()
		{
			this.liquid = 0;
			this.sTileHeader = 0;
			this.bTileHeader = 0;
			this.bTileHeader2 = 0;
			this.bTileHeader3 = 0;
			this.frameX = 0;
			this.frameY = 0;
		}

		public Color actColor(Color oldColor)
		{
			bool flag = !this.inActive();
			Color result;
			if (flag)
			{
				result = oldColor;
			}
			else
			{
				double num = 0.4;
				result = new Color((int)((byte)(num * (double)oldColor.R)), (int)((byte)(num * (double)oldColor.G)), (int)((byte)(num * (double)oldColor.B)), (int)oldColor.A);
			}
			return result;
		}

		public void actColor(ref Vector3 oldColor)
		{
			bool flag = this.inActive();
			if (flag)
			{
				oldColor *= 0.4f;
			}
		}

		public bool topSlope()
		{
			byte b = this.slope();
			bool flag = b != 1;
			return !flag || b == 2;
		}

		public bool bottomSlope()
		{
			byte b = this.slope();
			bool flag = b != 3;
			return !flag || b == 4;
		}

		public bool leftSlope()
		{
			byte b = this.slope();
			bool flag = b != 2;
			return !flag || b == 4;
		}

		public bool rightSlope()
		{
			byte b = this.slope();
			bool flag = b != 1;
			return !flag || b == 3;
		}

		public bool HasSameSlope(ITile tile)
		{
			return (this.sTileHeader & 29696) == (tile.sTileHeader & 29696);
		}

		public byte wallColor()
		{
			return (byte)(((int)(this.bTileHeader) & 31));
		}

		public void wallColor(byte wallColor)
		{
			this.bTileHeader = (byte)(((int)(this.bTileHeader) & 224) | wallColor);
		}

		public bool lava()
		{
			return (this.bTileHeader & 32) == 32;
		}

		public void lava(bool lava)
		{
			if (lava)
			{
				this.bTileHeader = (byte)(((int)(this.bTileHeader) & 159) | 32);
			}
			else
			{
				this.bTileHeader &= 223;
			}
		}

		public bool honey()
		{
			return (this.bTileHeader & 64) == 64;
		}

		public void honey(bool honey)
		{
			if (honey)
			{
				this.bTileHeader = (byte)(((int)(this.bTileHeader) & 159) | 64);
			}
			else
			{
				this.bTileHeader &= 191;
			}
		}

		public bool wire4()
		{
			return (this.bTileHeader & 128) == 128;
		}

		public void wire4(bool wire4)
		{
			if (wire4)
			{
				this.bTileHeader |= 128;
			}
			else
			{
				this.bTileHeader &= 127;
			}
		}

		public int wallFrameX()
		{
			return (int)((this.bTileHeader2 & 15) * 36);
		}

		public void wallFrameX(int wallFrameX)
		{
			this.bTileHeader2 = (byte)((int)(this.bTileHeader2 & 240) | (wallFrameX / 36 & 15));
		}

		public byte frameNumber()
		{
			return (byte)((this.bTileHeader2 & 48) >> 4);
		}

		public void frameNumber(byte frameNumber)
		{
			this.bTileHeader2 = (byte)((int)(this.bTileHeader2 & 207) | (int)(frameNumber & 3) << 4);
		}

		public byte wallFrameNumber()
		{
			return (byte)((this.bTileHeader2 & 192) >> 6);
		}

		public void wallFrameNumber(byte wallFrameNumber)
		{
			this.bTileHeader2 = (byte)((int)(this.bTileHeader2 & 63) | (int)(wallFrameNumber & 3) << 6);
		}

		public int wallFrameY()
		{
			return (int)((this.bTileHeader3 & 7) * 36);
		}

		public void wallFrameY(int wallFrameY)
		{
			this.bTileHeader3 = (byte)((int)(this.bTileHeader3 & 248) | (wallFrameY / 36 & 7));
		}

		public bool checkingLiquid()
		{
			return (this.bTileHeader3 & 8) == 8;
		}

		public void checkingLiquid(bool checkingLiquid)
		{
			if (checkingLiquid)
			{
				this.bTileHeader3 |= 8;
			}
			else
			{
				this.bTileHeader3 &= 247;
			}
		}

		public bool skipLiquid()
		{
			return (this.bTileHeader3 & 16) == 16;
		}

		public void skipLiquid(bool skipLiquid)
		{
			if (skipLiquid)
			{
				this.bTileHeader3 |= 16;
			}
			else
			{
				this.bTileHeader3 &= 239;
			}
		}

		public byte color()
		{
			return (byte)(this.sTileHeader & 31);
		}

		public void color(byte color)
		{
			this.sTileHeader = (short)(((int)this.sTileHeader & 65504) | (int)color);
		}

		public bool active()
		{
			return (this.sTileHeader & 32) == 32;
		}

		public void active(bool active)
		{
			if (active)
			{
				this.sTileHeader |= 32;
			}
			else
			{
				this.sTileHeader = (short)((int)this.sTileHeader & 65503);
			}
		}

		public bool inActive()
		{
			return (this.sTileHeader & 64) == 64;
		}

		public void inActive(bool inActive)
		{
			if (inActive)
			{
				this.sTileHeader |= 64;
			}
			else
			{
				this.sTileHeader = (short)((int)this.sTileHeader & 65471);
			}
		}

		public bool wire()
		{
			return (this.sTileHeader & 128) == 128;
		}

		public void wire(bool wire)
		{
			if (wire)
			{
				this.sTileHeader |= 128;
			}
			else
			{
				this.sTileHeader = (short)((int)this.sTileHeader & 65407);
			}
		}

		public bool wire2()
		{
			return (this.sTileHeader & 256) == 256;
		}

		public void wire2(bool wire2)
		{
			if (wire2)
			{
				this.sTileHeader |= 256;
			}
			else
			{
				this.sTileHeader = (short)((int)this.sTileHeader & 65279);
			}
		}

		public bool wire3()
		{
			return (this.sTileHeader & 512) == 512;
		}

		public void wire3(bool wire3)
		{
			if (wire3)
			{
				this.sTileHeader |= 512;
			}
			else
			{
				this.sTileHeader = (short)((int)this.sTileHeader & 65023);
			}
		}

		public bool halfBrick()
		{
			return (this.sTileHeader & 1024) == 1024;
		}

		public void halfBrick(bool halfBrick)
		{
			if (halfBrick)
			{
				this.sTileHeader |= 1024;
			}
			else
			{
				this.sTileHeader = (short)((int)this.sTileHeader & 64511);
			}
		}

		public bool actuator()
		{
			return (this.sTileHeader & 2048) == 2048;
		}

		public void actuator(bool actuator)
		{
			if (actuator)
			{
				this.sTileHeader |= 2048;
			}
			else
			{
				this.sTileHeader = (short)((int)this.sTileHeader & 63487);
			}
		}

		public byte slope()
		{
			return (byte)((this.sTileHeader & 28672) >> 12);
		}

		public void slope(byte slope)
		{
			this.sTileHeader = (short)(((int)this.sTileHeader & 36863) | (int)(slope & 7) << 12);
		}

		public void Clear(TileDataType types)
		{
			bool flag = ((int)(types) & 1) > 0;
			if (flag)
			{
				this.type = 0;
				this.active(false);
				this.frameX = 0;
				this.frameY = 0;
			}
			bool flag2 = ((int)(types) & 4) > 0;
			if (flag2)
			{
				this.wall = 0;
				this.wallFrameX(0);
				this.wallFrameY(0);
			}
			bool flag3 = ((int)(types) & 2) > 0;
			if (flag3)
			{
				this.color(0);
			}
			bool flag4 = ((int)(types) & 8) > 0;
			if (flag4)
			{
				this.wallColor(0);
			}
			bool flag5 = ((int)(types) & 16) > 0;
			if (flag5)
			{
				this.liquid = 0;
				this.liquidType(0);
				this.checkingLiquid(false);
			}
			bool flag6 = ((int)(types) & 128) > 0;
			if (flag6)
			{
				this.slope(0);
				this.halfBrick(false);
			}
			bool flag7 = ((int)(types) & 32) > 0;
			if (flag7)
			{
				this.wire(false);
				this.wire2(false);
				this.wire3(false);
				this.wire4(false);
			}
			bool flag8 = ((int)(types) & 64) > 0;
			if (flag8)
			{
				this.actuator(false);
				this.inActive(false);
			}
		}

		public void Initialise()
		{
			this.type = 0;
			this.wall = 0;
			this.liquid = 0;
			this.sTileHeader = 0;
			this.bTileHeader = 0;
			this.bTileHeader2 = 0;
			this.bTileHeader3 = 0;
			this.frameX = 0;
			this.frameY = 0;
		}

		internal readonly int offset;
		private AekinTileData[] data;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 13)]
	public struct AekinTileData
	{
		public ushort wall;
		public byte liquid;
		public byte bTileHeader;
		public byte bTileHeader2;
		public byte bTileHeader3;
		public ushort type;
		public short sTileHeader;
		public short frameX;
		public short frameY;
	}
	public class AekinTileCollection : ITileCollection, IDisposable
	{
		public int Width
		{
			get
			{
				return this._width;
			}
		}

		public int Height
		{
			get
			{
				return this._height;
			}
		}

		public ITile this[int x, int y]
		{
			get
			{
				bool flag = this.data == null;
				if (flag)
				{
					this.data = new AekinTileData[(Main.maxTilesX + 1) * (Main.maxTilesY + 1)];
					this._width = Main.maxTilesX + 1;
					this._height = Main.maxTilesY + 1;
				}
				return new AekinTile(this.data, x, y);
			}
			set
			{
				new AekinTile(this.data, x, y).CopyFrom(value);
			}
		}

		public void Dispose()
		{
			bool flag = this.data != null;
			if (flag)
			{
				for (int i = 0; i < this.data.Length; i++)
				{
					this.data[i].bTileHeader = 0;
					this.data[i].bTileHeader2 = 0;
					this.data[i].bTileHeader3 = 0;
					this.data[i].frameX = 0;
					this.data[i].frameY = 0;
					this.data[i].liquid = 0;
					this.data[i].type = 0;
					this.data[i].wall = 0;
				}
				this.data = null;
			}
		}

		private AekinTileData[] data;
		private int _width;
		private int _height;
	}
}
