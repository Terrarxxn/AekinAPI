using AekinConvict.Core;
using AekinConvict.Databases;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Streams;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Terraria;
using Terraria.ID;
using Terraria.Net;
using Terraria.Net.Sockets;
using TerrariaApi.Server;

namespace AekinConvict.Network
{
	public class AekinTcpSocket : ISocket
	{
		public int MessagesInQueue
		{
			get
			{
				return this._messagesInQueue;
			}
		}

		public AekinTcpSocket()
		{
			this._connection = new TcpClient();
			this._connection.NoDelay = true;
		}

		public AekinTcpSocket(TcpClient tcpClient)
		{
			this._connection = tcpClient;
			this._connection.NoDelay = true;
			IPEndPoint ipendPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
			this._remoteAddress = new TcpAddress(ipendPoint.Address, ipendPoint.Port);
		}

		void ISocket.Close()
		{
			this._remoteAddress = null;
			this._connection.Close();
		}

		bool ISocket.IsConnected()
		{
			return this._connection != null && this._connection.Client != null && this._connection.Connected;
		}

		void ISocket.Connect(RemoteAddress address)
		{
			TcpAddress tcpAddress = (TcpAddress)address;
			this._connection.Connect(tcpAddress.Address, tcpAddress.Port);
			this._remoteAddress = address;
		}

		private void ReadCallback(IAsyncResult result)
		{
			Tuple<SocketReceiveCallback, object> tuple = (Tuple<SocketReceiveCallback, object>)result.AsyncState;
			try
			{
				tuple.Item1(tuple.Item2, this._connection.GetStream().EndRead(result));
			}
			catch (InvalidOperationException)
			{
				((ISocket)this).Close();
			}
			catch
			{
			}
		}

		private void SendCallback(IAsyncResult result)
		{
			object[] array = (object[])result.AsyncState;
			LegacyNetBufferPool.ReturnBuffer((byte[])array[1]);
			Tuple<SocketSendCallback, object> tuple = (Tuple<SocketSendCallback, object>)array[0];
			try
			{
				this._connection.GetStream().EndWrite(result);
				tuple.Item1(tuple.Item2);
			}
			catch (Exception)
			{
				((ISocket)this).Close();
			}
		}

		void ISocket.SendQueuedPackets()
		{
		}

		void ISocket.AsyncSend(byte[] data, int offset, int size, SocketSendCallback callback, object state)
		{
			byte[] array = LegacyNetBufferPool.RequestBuffer(data, offset, size);
			this._connection.GetStream().BeginWrite(array, 0, size, new AsyncCallback(this.SendCallback), new object[]
			{
				new Tuple<SocketSendCallback, object>(callback, state),
				array
			});
		}

		void ISocket.AsyncReceive(byte[] data, int offset, int size, SocketReceiveCallback callback, object state)
		{
			this._connection.GetStream().BeginRead(data, offset, size, new AsyncCallback(this.ReadCallback), new Tuple<SocketReceiveCallback, object>(callback, state));
		}

		bool ISocket.IsDataAvailable()
		{
			return this._connection.GetStream().DataAvailable;
		}

		RemoteAddress ISocket.GetRemoteAddress()
		{
			return this._remoteAddress;
		}

		bool ISocket.StartListening(SocketConnectionAccepted callback)
		{
			IPAddress any = IPAddress.Any;
			string ipString;
			if (Program.LaunchParameters.TryGetValue("-ip", out ipString) && !IPAddress.TryParse(ipString, out any))
			{
				any = IPAddress.Any;
			}
			this._isListening = true;
			this._listenerCallback = callback;
			if (this._listener == null)
			{
				this._listener = new TcpListener(any, Netplay.ListenPort);
			}
			try
			{
				this._listener.Start();
			}
			catch (Exception)
			{
				return false;
			}
			ThreadPool.QueueUserWorkItem(new WaitCallback(this.ListenLoop));
			return true;
		}

		void ISocket.StopListening()
		{
			this._isListening = false;
		}

		private void ListenLoop(object unused)
		{
			while (this._isListening && !Netplay.Disconnect)
			{
				try
				{
					ISocket socket = new AekinTcpSocket(this._listener.AcceptTcpClient());
					string i = socket.GetRemoteAddress().ToString().Split(':')[0];

					if (!AekinConfig.Instance.IsPublic && i != "127.0.0.1")
					{
						socket.Close();
					}

					Console.WriteLine("[AekinTcpSocket]: Обнаружено исходящее подключение от " + i + "...");

					this._listenerCallback(socket);
				}
				catch (Exception)
				{
				}
			}
			this._listener.Stop();
			Netplay.IsListening = false;
		}

		public byte[] _packetBuffer = new byte[1024];
		public int _packetBufferLength;
		public List<object> _callbackBuffer = new List<object>();
		public int _messagesInQueue;
		public TcpClient _connection;
		public TcpListener _listener;
		public SocketConnectionAccepted _listenerCallback;
		public RemoteAddress _remoteAddress;
		public bool _isListening;
	}

	public static class NetUtils
    {
        public static void AggregateBuildings(this NetPlayer plr, GetDataEventArgs e, Rectangle rectangle)
        {
            if (!plr.HasPermission("aekin.world.edit"))
            {
                if (plr.regionWarnThreshold < 1)
                {
                    plr.SendErrorMessage("Недостаточно прав для взаимодействия с миром.");
                    plr.regionWarnThreshold = 5000;
                }
                plr.DataHelper.SendTileRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, TileChangeType.None);
                e.Handled = true;
            }
            if (!plr.CanBuild(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height))
            {
                if (plr.regionWarnThreshold < 1)
                {
                    plr.SendErrorMessage("Недостаточно прав для взаимодействия с регионом.");
                    plr.regionWarnThreshold = 5000;
                }
                plr.DataHelper.SendTileRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, TileChangeType.None);
                e.Handled = true;
            }
        }
        public static void AggregateBuildings(this NetPlayer plr, GetDataEventArgs e, int x, int y)
        {
            if (!plr.HasPermission("aekin.world.edit"))
            {
                if (plr.regionWarnThreshold < 1)
                {
                    plr.SendErrorMessage("Недостаточно прав для взаимодействия с миром.");
                    plr.regionWarnThreshold = 5000;
                }
                plr.DataHelper.SendTileRectangle(x, y, 2, 2, TileChangeType.None);
                e.Handled = true;
                return;
            }
            if (!plr.CanBuild(x, y))
            {
                if (plr.regionWarnThreshold < 1)
                {
                    plr.SendErrorMessage("Недостаточно прав для взаимодействия с регионом.");
                    plr.regionWarnThreshold = 5000;
                }
                plr.DataHelper.SendTileRectangle(x, y, 2, 2, TileChangeType.None);
                e.Handled = true;
            }
        }
        public static void AggregateRegionMarkups(this NetPlayer plr, GetDataEventArgs e, int x, int y)
        {
            if (plr.awaitingRgPoints)
            {
                if (plr.pointPosition == 1)
                {
                    plr.SendInfoMessage("Указана точка '1': X:'" + x + "' Y:'" + y + "'");
                    plr.firstPoint = new Point(x, y);
                    plr.pointPosition++;
                    e.Handled = true;
                }
                else if (plr.pointPosition == 2)
                {
                    plr.SendInfoMessage("Указана точка '2': X:'" + x + "' Y:'" + y + "'");
                    plr.secondPoint = new Point(x, y);
                    plr.pointPosition = 0;
                    plr.awaitingRgPoints = false;
                    e.Handled = true;
                }
            }
        }
    }
    public delegate void NetDataHandler(GetDataEventArgs e, MemoryStream stream);
    public static class NetHandlers
    {
        internal static Dictionary<PacketTypes, NetDataHandler> NetDataHandlers;
        internal static void HandleNet(GetDataEventArgs e)
        {
            if (e.Handled)
                return;

            using (MemoryStream memoryStream = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length - 1))
            {
                if (NetDataHandlers.ContainsKey(e.MsgID))
                {
                    NetDataHandlers[e.MsgID](e, memoryStream);
                }
            }
        }
        internal static void InitializeNetHandlers()
        {
            NetDataHandlers = new Dictionary<PacketTypes, NetDataHandler>()
            {
                { PacketTypes.ProjectileNew, HandleProjectileNew },
                { PacketTypes.ConnectRequest, HandlePlayerConnect },
                { PacketTypes.PlayerHp, HandlePlayerLife },
                { PacketTypes.PlayerMana, HandlePlayerMana },
                { PacketTypes.PlayerSlot, HandlePlayerSlot },
                { PacketTypes.Tile, HandleTileEdit },
                { PacketTypes.PlaceChest, HandleChestPlace },
                { PacketTypes.ChestItem, HandleChestItem },
                { PacketTypes.ChestOpen, HandleChestOpen },
                { PacketTypes.FoodPlatterTryPlacing, HandleFoodPlatter },
                { PacketTypes.LiquidSet, HandleLiquidSet },
                { PacketTypes.SignNew, HandleSignText },
                { PacketTypes.PaintTile, HandleTilePaint },
                { PacketTypes.PaintWall, HandleWallPaint },
                { PacketTypes.TileSendSquare, HandleTileRect },
                { PacketTypes.PlaceObject, HandlePlaceObject }
            };
        }

        public static void HandlePlayerConnect(GetDataEventArgs e, MemoryStream stream)
        {
            Main.ServerSideCharacter = AekinConfig.Instance.SSCEnable;
            Main.maxNetPlayers = AekinConfig.Instance.MaxPlayers;
            Main.worldName = AekinConfig.Instance.ServerName;
        }


        public static PacketHandlers<ProjectileNewPacketArgs> ProjectileNew = new PacketHandlers<ProjectileNewPacketArgs>();
        public static void HandleProjectileNew(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short projIndex = stream.ReadInt16();
            float x = stream.ReadSingle();
            float y = stream.ReadSingle();
            float velx = stream.ReadSingle();
            float vely = stream.ReadSingle();
            byte owner = stream.ReadInt8();
            short projId = stream.ReadInt16();
            BitsByte flags = stream.ReadInt8();
            float ai0 = flags[0] ? stream.ReadSingle() : 0f;
            float ai1 = flags[1] ? stream.ReadSingle() : 0f;
            ushort bannerIdToRespondTo = flags[3] ? stream.ReadUInt16() : (ushort)0;
            short dmg = flags[4] ? stream.ReadInt16() : (short)-1;
            float knockback = flags[5] ? stream.ReadSingle() : -1f;
            short odmg = flags[6] ? stream.ReadInt16() : (short)-1;
            short uuid = flags[7] ? stream.ReadInt16() : (short)-1;

            if (ProjectileNew.Invoke(new ProjectileNewPacketArgs(e, projIndex, new Vector2(x, y), new Vector2(velx, vely), owner, projId, flags, ai0, ai1, dmg, knockback, odmg, uuid)))
                return;

            if (projId == 43 && AekinConfig.Instance.DisableTombstones)
            {
                Main.projectile[projIndex] = new Projectile();
                NetServer.Players[sender].DataHelper.PushEmptyProjectile(projIndex);
                e.Handled = true;
            }
            if (Main.projHostile[projIndex])
            {
                Main.projectile[projIndex] = new Projectile();
                NetServer.Players[sender].DataHelper.PushEmptyProjectile(projIndex);
                e.Handled = true;
                return;
            }
        }
        public static PacketHandlers<PlayerLifePacketArgs> PlayerLife = new PacketHandlers<PlayerLifePacketArgs>();
        public static void HandlePlayerLife(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            byte plrId = stream.ReadInt8();
            short hp = stream.ReadInt16();
            short mhp = stream.ReadInt16();

            if (PlayerLife.Invoke(new PlayerLifePacketArgs(e, hp, mhp)))
                return;

            NetPlayer plr = NetServer.Players[sender];
            if (plr != null && plr.loggedIn && AekinConfig.Instance.SSCEnable)
            {
                Character character = plr.Character;
                character.LifeMax = mhp;
                plr.Character = character;

                new Thread(() => { AekinDatabase.Instance.Characters.PushCharacter(plr.Account.name, plr.Character); }).Start();
            }
        }

        public static PacketHandlers<PlayerManaPacketArgs> PlayerMana = new PacketHandlers<PlayerManaPacketArgs>();
        public static void HandlePlayerMana(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            byte plrId = stream.ReadInt8();
            short mp = stream.ReadInt16();
            short mmp = stream.ReadInt16();

            if (PlayerMana.Invoke(new PlayerManaPacketArgs(e, mp, mmp)))
                return;

            NetPlayer plr = NetServer.Players[sender];
            if (plr != null && plr.loggedIn && AekinConfig.Instance.SSCEnable)
            {
                Character character = plr.Character;
                character.ManaMax = mmp;
                plr.Character = character;

                new Thread(() => { AekinDatabase.Instance.Characters.PushCharacter(plr.Account.name, plr.Character); }).Start();
            }
        }

        public static PacketHandlers<PlayerSlotPacketArgs> PlayerSlot = new PacketHandlers<PlayerSlotPacketArgs>();
        public static void HandlePlayerSlot(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            NetPlayer plr = NetServer.Players[sender];
            byte plrId = stream.ReadInt8();
            short slot = stream.ReadInt16();
            short stack = stream.ReadInt16();
            byte prefix = stream.ReadInt8();
            short id = stream.ReadInt16();

            if (PlayerSlot.Invoke(new PlayerSlotPacketArgs(e, slot, stack, prefix, id)))
                return;

            if (plr != null && AekinConfig.Instance.SSCEnable)
            {
                if (plr.loggedIn)
                {
                    NetItem item = new NetItem(id, stack, prefix);

                    plr.Character.InventoryData[slot] = item;
                    new Thread(() => { AekinDatabase.Instance.Characters.PushCharacter(plr.Account.name, plr.Character); }).Start();

                    foreach (NetPlayer globalPlr in NetServer.Players)
                        if (globalPlr != null && globalPlr.Index != plr.Index)
                            globalPlr.DataHelper.PushSlot(slot, id, stack, prefix, plr.Index);
                }
                else
                {
                    foreach (NetPlayer globalPlr in NetServer.Players)
                        if (globalPlr != null)
                            globalPlr.DataHelper.PushSlot(slot, 0, 0, 0, plr.Index);
                }

            }
            plr.aekinInventory[slot] = new NetItem(id, stack, prefix);

            e.Handled = AekinConfig.Instance.SSCEnable;
        }

        public static PacketHandlers<TilePaintPacketArgs> TilePaint = new PacketHandlers<TilePaintPacketArgs>();
        public static void HandleTilePaint(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            byte pnt = stream.ReadInt8();

            if (TilePaint.Invoke(new TilePaintPacketArgs(e, x, y, pnt)))
                return;

            if ((x < 0 || x > Main.maxTilesX) || (y < 0 || y > Main.maxTilesY))
            {
                NetServer.Players[sender].Kick("ATSAC", "Античит-система заподозрила неладное.");
            }


            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<WallPaintPacketArgs> WallPaint = new PacketHandlers<WallPaintPacketArgs>();
        public static void HandleWallPaint(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            byte pnt = stream.ReadInt8();

            if (WallPaint.Invoke(new WallPaintPacketArgs(e, x, y, pnt)))
                return;

            if ((x < 0 || x > Main.maxTilesX) || (y < 0 || y > Main.maxTilesY))
            {
                NetServer.Players[sender].Kick("ATSAC", "Античит-система заподозрила неладное.");
            }

            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<SignTextPacketArgs> SignText = new PacketHandlers<SignTextPacketArgs>();
        public static void HandleSignText(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short num = stream.ReadInt16();
            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            string str = stream.ReadString();

            if (SignText.Invoke(new SignTextPacketArgs(e, num, x, y, str)))
                return;

            if ((x < 0 || x > Main.maxTilesX) || (y < 0 || y > Main.maxTilesY))
            {
                NetServer.Players[sender].Kick("ATSAC", "Античит-система заподозрила неладное.");
            }

            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<LiquidSetPacketArgs> LiquidSet = new PacketHandlers<LiquidSetPacketArgs>();
        public static void HandleLiquidSet(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            byte liquidCount = stream.ReadInt8();
            byte liquidType = stream.ReadInt8();

            if (LiquidSet.Invoke(new LiquidSetPacketArgs(e, x, y, liquidCount, liquidType)))
                return;

            if ((x < 0 || x > Main.maxTilesX) || (y < 0 || y > Main.maxTilesY))
            {
                NetServer.Players[sender].Kick("ATSAC", "Античит-система заподозрила неладное.");
            }

            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<FoodPlatterPacketArgs> FoodPlatter = new PacketHandlers<FoodPlatterPacketArgs>();
        public static void HandleFoodPlatter(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            short itemID = stream.ReadInt16();
            byte prefix = stream.ReadInt8();
            short stack = stream.ReadInt16();

            if (FoodPlatter.Invoke(new FoodPlatterPacketArgs(e, x, y, itemID, prefix, stack)))
                return;

            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<ChestOpenPacketArgs> ChestOpen = new PacketHandlers<ChestOpenPacketArgs>();
        public static void HandleChestOpen(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();

            if (ChestOpen.Invoke(new ChestOpenPacketArgs(e, x, y)))
                return;

            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<ChestPlacePacketArgs> ChestPlace = new PacketHandlers<ChestPlacePacketArgs>();
        public static void HandleChestPlace(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            int flag = stream.ReadInt32();
            short style = stream.ReadInt16();

            if (ChestPlace.Invoke(new ChestPlacePacketArgs(e, x, y, flag, style)))
                return;

            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<ChestItemPacketArgs> ChestItem = new PacketHandlers<ChestItemPacketArgs>();
        public static void HandleChestItem(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short id = stream.ReadInt16();
            byte slot = stream.ReadInt8();
            short stacks = stream.ReadInt16();
            byte prefix = stream.ReadInt8();
            short type = stream.ReadInt16();

            if (ChestItem.Invoke(new ChestItemPacketArgs(e, id, slot, stacks, prefix, type)))
                return;

            NetServer.Players[sender].AggregateBuildings(e, Main.chest[id].x, Main.chest[id].y);
        }

        public static PacketHandlers<TileEditPacketArgs> TileEdit = new PacketHandlers<TileEditPacketArgs>();
        public static void HandleTileEdit(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            byte editAction = stream.ReadInt8();
            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            short editData = stream.ReadInt16();
            byte style = stream.ReadInt8();

            if (TileEdit.Invoke(new TileEditPacketArgs(e, editAction, x, y, editData, style)))
                return;

            if ((x < 0 || x > Main.maxTilesX) || (y < 0 || y > Main.maxTilesY))
            {
                NetServer.Players[sender].Kick("ATSAC", "Античит-система заподозрила неладное.");
            }

            NetServer.Players[sender].AggregateRegionMarkups(e, x, y);
            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }

        public static PacketHandlers<TileRectPacketArgs> TileRect = new PacketHandlers<TileRectPacketArgs>();
        public static void HandleTileRect(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            byte width = stream.ReadInt8();
            byte height = stream.ReadInt8();
            byte tileChangeType = stream.ReadInt8();

            if (TileRect.Invoke(new TileRectPacketArgs(e, x, y, width, height, tileChangeType)))
                return;

            if ((x < 0 || x > Main.maxTilesX) || (x + width > Main.maxTilesX) || (y < 0 || y > Main.maxTilesY ) || (y + height > Main.maxTilesY))
            {
                NetServer.Players[sender].Kick("ATSAC", "Античит-система заподозрила неладное.");
            }

            NetServer.Players[sender].AggregateBuildings(e, new Rectangle(x, y, width, height));
        }
        public static PacketHandlers<PlaceObjectPacketArgs> PlaceObject = new PacketHandlers<PlaceObjectPacketArgs>();
        public static void HandlePlaceObject(GetDataEventArgs e, MemoryStream stream)
        {
            int sender = e.Msg.whoAmI;

            short x = stream.ReadInt16();
            short y = stream.ReadInt16();
            short type = stream.ReadInt16();
            short style = stream.ReadInt16();
            byte alternate = stream.ReadInt8();
            bool direction = stream.ReadBoolean();

            if (PlaceObject.Invoke(new PlaceObjectPacketArgs(e, x, y, type, style, alternate, direction)))
                return;

            if ((x < 0 || x > Main.maxTilesX) || (y < 0 || y > Main.maxTilesY))
            {
                NetServer.Players[sender].Kick("ATSAC", "Античит-система заподозрила неладное.");
            }

            NetPlayer plr = NetServer.Players[sender];

            Item item = new Item();
            item.SetDefaults(plr.aekinInventory[plr.Player.selectedItem].ID);
            if (type != item.createTile)
            {
                plr.DataHelper.SendTileRectangle(x, y, 4, 4);
                e.Handled = true;
                return;
            }
            if (Main.tileFrame[type] < style)
            {
                // plr.DataHelper.SendTileRectangle(x, y, 4, 4);
                // вдруг он факел крашающий поставит! это же ему достанется ахзазхазхза
                e.Handled = true;
                return;
            }

            NetServer.Players[sender].AggregateBuildings(e, x, y);
        }
    }

    public sealed class PacketHandlers<T>
    {
        private List<PacketHandlerDelegate> _handlers;

        public void Add(PacketHandlerDelegate handler)
        {
            if (_handlers == null) _handlers = new List<PacketHandlerDelegate>();

            if (_handlers.Contains(handler))
            {
                throw new HandlerAlreadyAddedException();
            }

            _handlers.Add(handler);
        }
        public void Remove(PacketHandlerDelegate handler)
        {
            if (_handlers == null) _handlers = new List<PacketHandlerDelegate>();

            _handlers.Remove(handler);
        }

        internal bool Invoke(T packetArgs)
        {
            if (_handlers == null) _handlers = new List<PacketHandlerDelegate>();

            HandlePacketArgs e = packetArgs as HandlePacketArgs;
            foreach (PacketHandlerDelegate handler in _handlers)
            {
                handler(e);
            }

            return e.Handled;
        }
    }
    public delegate void PacketHandlerDelegate(HandlePacketArgs e);

    public class HandlerAlreadyAddedException : Exception
    {
        public HandlerAlreadyAddedException() : base("В коллекции уже присутствует данный обработчик данных (PacketHandlerDelegate).")
        {
        }
    }

    public class HandlePacketArgs
    {
        protected HandlePacketArgs(GetDataEventArgs args)
        {
            Player = NetServer.Players[args.Msg.whoAmI];
            TerrariaPlayer = Main.player[args.Msg.whoAmI];
            GetDataArgs = args;
            Handled = GetDataArgs.Handled;
        }
        public NetPlayer Player { get; protected set; }
        public Player TerrariaPlayer { get; protected set; }
        public GetDataEventArgs GetDataArgs { get; protected set; }
        public bool Handled { get; set; }
    }
    public sealed class ProjectileNewPacketArgs : HandlePacketArgs
    {
        public ProjectileNewPacketArgs(GetDataEventArgs e, short projectile, Vector2 pos, Vector2 vel, byte owner, short projtype, byte flags, float ai0, float ai1, short dmg, float kb, short origDmg, short uuid) : base(e)
        {
            ProjectileIndex = projectile;
            Position = pos;
            Velocity = vel;
            Owner = owner;
            ProjectileID = projtype;
            Flags = flags;
            AI0 = ai0;
            AI1 = ai1;
            Damage = Damage;
            Knockback = kb;
            OriginalDamage = origDmg;
            ProjectileUUID = uuid;
        }

        public short ProjectileIndex { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public byte Owner { get; private set; }
        public short ProjectileID { get; private set; }
        public byte Flags { get; private set; }
        public float AI0 { get; private set; }
        public float AI1 { get; private set; }
        public short Damage { get; private set; }
        public float Knockback { get; private set; }
        public short OriginalDamage { get; private set; }
        public short ProjectileUUID { get; private set; }
    }
    public sealed class PlayerLifePacketArgs : HandlePacketArgs
    {
        public PlayerLifePacketArgs(GetDataEventArgs e, short life, short maxLife) : base(e)
        {
            this.Life = life;
            this.MaxLife = maxLife;
        }

        public short Life { get; private set; }
        public short MaxLife { get; private set; }
    }
    public sealed class PlayerManaPacketArgs : HandlePacketArgs
    {
        public PlayerManaPacketArgs(GetDataEventArgs e, short mp, short maxMp) : base(e)
        {
            this.Mana = mp;
            this.MaxMana = maxMp;
        }

        public short Mana { get; private set; }
        public short MaxMana { get; private set; }
    }
    public sealed class PlayerSlotPacketArgs : HandlePacketArgs
    {
        public PlayerSlotPacketArgs(GetDataEventArgs e, short slot, short stack, byte prefix, short item) : base(e)
        {
            this.Slot = slot;
            this.Stack = stack;
            this.Prefix = prefix;
            this.Item = item;
        }

        public short Slot { get; private set; }
        public short Stack { get; private set; }
        public byte Prefix { get; private set; }
        public short Item { get; private set; }
    }
    public sealed class WallPaintPacketArgs : HandlePacketArgs
    {
        public WallPaintPacketArgs(GetDataEventArgs e, short x, short y, byte paint) : base(e)
        {
            this.X = x;
            this.Y = y;
            this.Paint = paint;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
        public byte Paint { get; private set; }
    }
    public sealed class TilePaintPacketArgs : HandlePacketArgs
    {
        public TilePaintPacketArgs(GetDataEventArgs e, short x, short y, byte paint) : base(e)
        {
            this.X = x;
            this.Y = y;
            this.Paint = paint;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
        public byte Paint { get; private set; }
    }
    public sealed class SignTextPacketArgs : HandlePacketArgs
    {
        public SignTextPacketArgs(GetDataEventArgs e, short sign, short x, short y, string text) : base(e)
        {
            this.SignID = sign;
            this.X = x;
            this.Y = y;
            this.Text = text;
        }

        public short SignID { get; private set; }
        public short X { get; private set; }
        public short Y { get; private set; }
        public string Text { get; private set; }
    }
    public sealed class LiquidSetPacketArgs : HandlePacketArgs
    {
        public LiquidSetPacketArgs(GetDataEventArgs e, short x, short y, byte liquidCount, byte liquidType) : base(e)
        {
            this.X = x;
            this.Y = y;
            this.LiquidCount = liquidCount;
            this.LiquidType = liquidType;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
        public byte LiquidCount { get; private set; }
        public byte LiquidType { get; private set; }
    }
    public sealed class FoodPlatterPacketArgs : HandlePacketArgs
    {
        public FoodPlatterPacketArgs(GetDataEventArgs e, short x, short y, short item, byte prefix, short stack) : base(e)
        {
            this.X = x;
            this.Y = y;
            this.ItemID = item;
            this.Prefix = prefix;
            this.Stack = stack;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
        public short ItemID { get; private set; }
        public byte Prefix { get; private set; }
        public short Stack { get; private set; }
    }
    public sealed class ChestOpenPacketArgs : HandlePacketArgs
    {
        public ChestOpenPacketArgs(GetDataEventArgs e, short x, short y) : base(e)
        {
            this.X = x;
            this.Y = y;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
    }
    public sealed class ChestPlacePacketArgs : HandlePacketArgs
    {
        public ChestPlacePacketArgs(GetDataEventArgs e, short x, short y, int flags, short style) : base(e)
        {
            this.X = x;
            this.Y = y;
            this.Flags = flags;
            this.Style = style;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
        public int Flags { get; private set; }
        public short Style { get; private set; }
    }
    public sealed class ChestItemPacketArgs : HandlePacketArgs
    {
        public ChestItemPacketArgs(GetDataEventArgs e, short chest, byte slot, short stack, byte prefix, short item) : base(e)
        {
            this.ChestID = chest;
            this.Slot = slot;
            this.Stack = stack;
            this.Prefix = prefix;
            this.ItemID = item;
        }

        public short ChestID { get; private set; }
        public byte Slot { get; private set; }
        public short Stack { get; private set; }
        public byte Prefix { get; private set; }
        public short ItemID { get; private set; }
    }
    public sealed class TileEditPacketArgs : HandlePacketArgs
    {
        public TileEditPacketArgs(GetDataEventArgs e, byte editAction, short x, short y, short editData, byte style) : base(e)
        {
            this.EditAction = editAction;
            this.X = x;
            this.Y = y;
            this.EditData = editData;
            this.Style = style;
        }

        public byte EditAction { get; private set; }
        public short X { get; private set; }
        public short Y { get; private set; }
        public short EditData { get; private set; }
        public byte Style { get; private set; }
    }
    public sealed class TileRectPacketArgs : HandlePacketArgs
    {
        public TileRectPacketArgs(GetDataEventArgs e, short x, short y, byte width, byte height, byte tileChangeType) : base(e)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
            this.TileChangeType = tileChangeType;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
        public byte Width { get; private set; }
        public byte Height { get; private set; }
        public byte TileChangeType { get; private set; }
    }
    public sealed class PlaceObjectPacketArgs : HandlePacketArgs
    {
        public PlaceObjectPacketArgs(GetDataEventArgs e, short x, short y, short type, short style, byte alternate, bool direction) : base(e)
        {
            this.X = x;
            this.Y = y;
            this.Type = type;
            this.Style = style;
            this.Alternate = alternate;
            this.Direction = direction;
        }

        public short X { get; private set; }
        public short Y { get; private set; }
        public short Type { get; private set; }
        public short Style { get; private set; }
        public byte Alternate { get; private set; }
        public bool Direction { get; private set; }
    }
}