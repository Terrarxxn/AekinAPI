using AekinConvict.Commands;
using AekinConvict.Databases;
using AekinConvict.Hooks;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using OTAPI;
using System.IO.Streams;
using System.Linq;
using System.Reflection;
using System.Threading;
using Terraria;
using Terraria.Net.Sockets;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using TerrariaApi.Server;
using System.Net;
using AekinConvict.Network;
using AekinConvict.RemadeWorld;
using Terraria.IO;

namespace AekinConvict.Core
{
    [ApiVersion(2, 1)]
    public class AekinAPI : TerrariaPlugin
    {
        public AekinAPI(Main game) : base(game)
        {
        }

        public override string Author => "Vxlhatero";
        public override string Name => "Aekin API";
        public override Version Version => Assembly.GetAssembly(typeof(AekinAPI)).GetName().Version;
        public static string AekinToken => AekinConfig.Instance.AekinToken;

        public static GlobalPlayerChatSystemHandler PlayerChatHandler;
        public static GlobalPlayerJoinSystemHandler PlayerJoinHandler;
        public static GlobalPlayerLeaveSystemHandler PlayerLeaveHandler;

        internal static System.Timers.Timer AutoSave;

        public void SetConsoleTitle(int additionalValue)
        {
            int m = Main.player.Where((p) => p != null && p.active).Count() + additionalValue;
            Console.Title = "Aekin v" + Version.ToString() + " on Terraria " + Main.versionNumber + " [" + Main.worldName + ": " + m + "/" + Main.maxNetPlayers + "]";
        }

        public void InitializeGlobalHandlers()
        {
            PlayerChatHandler = (NetPlayer plr, string message) =>
            {
                NetServer.Broadcast(plr.Group.prefix + " " + plr.Player.name + ": " + message, new Color(plr.Group.r, plr.Group.g, plr.Group.b));
            };
            PlayerJoinHandler = (NetPlayer plr) =>
            {
                NetServer.Broadcast(string.Format("{0} присоединился.", plr.Name));
            };
            PlayerLeaveHandler = (NetPlayer plr) =>
            {
                NetServer.Broadcast(string.Format("{0} отключился.", plr.Name));
            };
        }
        public void InitializeHooks()
        {
            AekinHookManager.Initialize();
            NetHandlers.InitializeNetHandlers();
            ServerApi.Hooks.NetGetData.Register(this, NetHandlers.HandleNet);
            ServerApi.Hooks.NetGreetPlayer.Register(this, HandleGreet);
            ServerApi.Hooks.ServerJoin.Register(this, HandleJoin);
            ServerApi.Hooks.ServerLeave.Register(this, HandleLeave);
            ServerApi.Hooks.ServerChat.Register(this, HandleChat);
            ServerApi.Hooks.GamePostUpdate.Register(this, HandlePostUpdate);
            ServerApi.Hooks.ServerCommand.Register(this, HandleCommand);

            OTAPI.Hooks.Net.Socket.Create = (() => new AekinTcpSocket());
            OTAPI.Hooks.Player.Announce = ((int playerId) => HookResult.Cancel);
        }
        public void InitializeData()
        {
            AekinDatabase.LoadDatabase();
            AekinConfig.SafeLoadConfig();
        }
        public void InitializeRemadeWorld()
        {
            IDisposable disposable = Main.tile as IDisposable;
            Main.tile = new AekinTileCollection();
            bool flag = disposable != null;
            if (flag)
            {
                disposable.Dispose();
            }
            GC.Collect();
        }
        public void InitializeSaveTimer()
        {
            AutoSave = new System.Timers.Timer(180000)
            {
                AutoReset = true,
                Enabled = true
            };
            AutoSave.Elapsed += (sender, args) => WorldFile.SaveWorld(false, false);
        }

        public override void Initialize()
        {
            InitializeGlobalHandlers();
            InitializeHooks();
            InitializeData();
            InitializeRemadeWorld();
            InitializeSaveTimer();

            AekinServerPlayer.Instance = new AekinServerPlayer();
            CommandHelper.Initialize();
        }

        private void HandlePostUpdate(EventArgs e)
        {
            for (int i = 0; i < NetServer.Players.Length; i++)
                if (NetServer.Players[i] != null && !NetServer.Players[i].loggedIn && AekinConfig.Instance.SSCEnable)
                {
                    NetServer.Players[i].DataHelper.PushBuff(Terraria.ID.BuffID.Frozen, 180, false);
                    NetServer.Players[i].DataHelper.PushBuff(Terraria.ID.BuffID.Stoned, 180, false);
                    NetServer.Players[i].DataHelper.PushBuff(Terraria.ID.BuffID.Webbed, 180, false);

                    if (NetServer.Players[i].regionWarnThreshold > 0)
                        NetServer.Players[i].regionWarnThreshold--;
                }
        }

        private void HandleJoin(JoinEventArgs e)
        {
            int id = e.Who;

            NetServer.Players[id] = new NetPlayer(Main.player[id], Netplay.Clients[id].ClientUUID, Netplay.Clients[id].Socket.GetRemoteAddress().ToString().Split(':')[0]);
        }
        private void HandleGreet(GreetPlayerEventArgs e)
        {
            NetPlayer plr = NetServer.Players[e.Who];
            Account account = AekinDatabase.Instance.Accounts.GetAccount(plr.Player.name);

            if (account != null && plr.data["PLAYER/DATA"].Get<string>("NET_UUID") == account.clientUUID)
            {
                plr.Login(account);
            }
            plr.CheckBan();
            PlayerJoinHandler(plr);

            e.Handled = true;
        }
        private void HandleLeave(LeaveEventArgs e)
        {
            NetPlayer plr = NetServer.Players[e.Who];

            if (plr == null)
                return;

            if (!plr.hideKick)
                PlayerLeaveHandler(plr);
            plr = null;
        }
        private void HandleChat(ServerChatEventArgs e)
        {
            e.Handled = true;
            bool skip = false;
            if (e.Text.StartsWith("/"))
            {
                AekinHookManager.AggregateHook((hs) =>
                {
                    if (hs.OnCommand(NetServer.Players[e.Who], e.Text))
                        skip = true;
                });
                if (skip) return;

                CommandHelper.HandleCommand(NetServer.Players[e.Who], e.Text);
                return;
            }

            AekinHookManager.AggregateHook((hs) =>
            {
                if (hs.OnChat(NetServer.Players[e.Who], e.Text))
                    skip = true;
            });
            if (skip) return;
            PlayerChatHandler(NetServer.Players[e.Who], e.Text);
        }
        private void HandleCommand(CommandEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(e.Command))
            {
                e.Handled = true;
                return;
            }

            string text = e.Command.StartsWith("/") ? e.Command : "/" + e.Command;
            CommandHelper.HandleCommand(AekinServerPlayer.Instance, text);

            e.Handled = true;
        }
    }
    public sealed class AekinConfig
    {
        public static AekinConfig Instance { get; private set; }
        public static void SafeLoadConfig()
        {
            AekinConfig.Instance = new AekinConfig();

            Action action = JsonFileExists ? new Action(LoadConfig) : new Action(SaveConfig);
            action();
        }
        internal static void LoadConfig()
        {
            AekinConfig.Instance = JsonConvert.DeserializeObject<AekinConfig>(File.ReadAllText("data/configurations/Aekin.json"));
        }
        internal static void SaveConfig()
        {
            if (!DirectoryExists)
                Directory.CreateDirectory("data/configurations");
            File.WriteAllText("data/configurations/Aekin.json", JsonConvert.SerializeObject(AekinConfig.Instance, Formatting.Indented));
        }

        public static bool DirectoryExists => Directory.Exists("data/configurations");
        public static bool JsonFileExists => File.Exists("data/configurations/Aekin.json");

        public bool DisableTombstones = true;
        public string AekinToken = Guid.NewGuid().ToString();
        public string ServerName = "Aekin Server";
        public bool IsPublic;
        public int MaxPlayers = 75;

        public bool SSCEnable = false;
        public NetItem[] SSCDefaultItems = new NetItem[]
        {
            new NetItem(Terraria.ID.ItemID.GoldPickaxe, 1, 0),
            new NetItem(Terraria.ID.ItemID.GoldAxe, 1, 0),
            new NetItem(Terraria.ID.ItemID.Torch, 25, 0)
        };
    }
    public sealed class AekinMOTD
    {
        public static AekinMOTD Instance { get; private set; }
        public static void SafeLoadConfig()
        {
            AekinMOTD.Instance = new AekinMOTD();

            Action action = JsonFileExists ? new Action(LoadConfig) : new Action(SaveConfig);
            action();
        }
        internal static void LoadConfig()
        {
            AekinMOTD.Instance = JsonConvert.DeserializeObject<AekinMOTD>(File.ReadAllText("data/configurations/AekinMOTD.json"));
        }
        internal static void SaveConfig()
        {
            if (!DirectoryExists)
                Directory.CreateDirectory("data/configurations");
            File.WriteAllText("data/configurations/AekinMOTD.json", JsonConvert.SerializeObject(AekinConfig.Instance, Formatting.Indented));
        }
        internal static string Format(string motd)
        {
            return string.Format(motd, Main.worldName, Main.player.Where((p) => p.active).Count(), Main.maxNetPlayers);
        }

        public static bool DirectoryExists => Directory.Exists("data/configurations");
        public static bool JsonFileExists => File.Exists("data/configurations/AekinMOTD.json");

        public bool enableMotd;
        public AekinMOTDLine[] motdLines = new AekinMOTDLine[]
        {
            new AekinMOTDLine(Color.SkyBlue, "Добро пожаловать на наш сервер!"),
            new AekinMOTDLine(Color.SkyBlue, "Мир: {0}"),
            new AekinMOTDLine(Color.SkyBlue, "Онлайн: {1}/{2}")
        };
    }

    public struct AekinMOTDLine
    {
        public AekinMOTDLine(Color color, string message)
        {
            this.color = color;
            this.message = message;
        }
        public Color color;
        public string message;
    }


    public delegate void GlobalPlayerChatSystemHandler(NetPlayer author, string message);
    public delegate void GlobalPlayerJoinSystemHandler(NetPlayer author);
    public delegate void GlobalPlayerLeaveSystemHandler(NetPlayer author);

    public static class NetServer
    {
        public static NetPlayer[] Players = new NetPlayer[256];
        public static List<string> BannedProxies = new List<string>();

        public static void BroadcastStatus(string message)
        {
            NetMessage.SendData(9, -1, -1, NetworkText.FromLiteral(message), 32767, 3f, 0f, 0f, 0, 0, 0);
        }
        public static void Broadcast(string message)
        {
            Broadcast(message, Color.White);
        }
        public static void Broadcast(string message, Color color)
        {
            ChatHelper.BroadcastChatMessage(new Terraria.Localization.NetworkText(message, Terraria.Localization.NetworkText.Mode.Literal), color, -1);
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
    public class NetPlayer
    {
        public static NetPlayer Find(string name)
        {
            List<NetPlayer> players = (from p in NetServer.Players
                                       where p != null && p.Player.active && p.Name.ToLowerInvariant() == name.ToLowerInvariant()
                                       select p).ToList<NetPlayer>();
            List<NetPlayer> players2 = (from p in NetServer.Players
                                        where p != null && p.Player.active && p.Name.ToLowerInvariant().StartsWith(name.ToLowerInvariant())
                                        select p).ToList<NetPlayer>();
            return (players.Count != 0) ? players[0] : ((players2.Count != 0) ? players2[0] : null);
        }

        public virtual Player Player => data["PLAYER/DATA"].Get<Player>("TERRARIA_PLAYER");
        public virtual Group Group => AekinDatabase.Instance.Groups.GetGroup(Account.group);
        public virtual Account Account { get; internal set; }
        public Character Character { get; set; }
        public NetPlayerDataHelper DataHelper { get; internal set; }
        public NetPlayerSSCHelper SSCHelper { get; internal set; }
        public virtual string Name => Player.name;
        public int Index => Player.whoAmI;

        public bool disableSpawnLogin;

        public readonly bool realPlayer;
        public bool sentInventory;
        public bool loggedIn;
        public Dictionary<string, NetPlayerDataContainer> data;

        internal int regionWarnThreshold;
        internal NetPlayer lastDM;

        internal bool hideKick;

        internal bool awaitingRgPoints;
        internal Point firstPoint;
        internal Point secondPoint;
        internal byte pointPosition;

        internal NetItem[] aekinInventory;

        public NetPlayer(Player plr, string uuid, string ip)
        {
            realPlayer = true;

            data = new Dictionary<string, NetPlayerDataContainer>();

            Account = new Account();
            Account.group = "unregistered";

            data.Add("PLAYER/DATA", new NetPlayerDataContainer());
            data["PLAYER/DATA"].Push("TERRARIA_PLAYER", plr);
            data["PLAYER/DATA"].Push("NET_UUID", uuid);
            data["PLAYER/DATA"].Push("NET_IP", ip);

            DataHelper = new NetPlayerDataHelper(this);

            aekinInventory = new NetItem[260];
        }

        public NetPlayer(string accname, string group)
        {
            realPlayer = false;

            data = new Dictionary<string, NetPlayerDataContainer>();

            Account = new Account(accname, "", group, "", "", "");

            DataHelper = new NetPlayerDataHelper(this);

            aekinInventory = new NetItem[260];
        }

        public void GiveItem(int type, int stack, int prefix = 0)
        {
            int number = Item.NewItem((int)this.Player.position.X, (int)this.Player.position.Y, this.Player.width, this.Player.height, type, stack, true, prefix, true, false);
            NetMessage.SendDataDirect((int)PacketTypes.ItemDrop, Index, -1, null, number);
        }

        public virtual bool HasPermission(string permission)
        {
            return Group.HasPermission(permission) || Account.rootToken == AekinAPI.AekinToken;
        }

        public void CheckBan()
        {
            BanInformation ban1 = AekinDatabase.Instance.Bans.GetBanByName(data["PLAYER/DATA"].Get<string>("NET_UUID"));
            if (ban1 != null && !ban1.Expired)
            {
                KickByBan(
                    ban1.administrator.name,
                    $"{ban1.Left.Days}д. {ban1.Left.Hours}ч. {ban1.Left.Minutes}мин.",
                    ban1.banReason);
                return;
            }
            BanInformation ban2 = AekinDatabase.Instance.Bans.GetBanByIP(data["PLAYER/DATA"].Get<string>("NET_IP"));
            if (ban2 != null && !ban2.Expired)
            {
                KickByBan(
                    ban2.administrator.name,
                    $"{ban2.Left.Days}д. {ban2.Left.Hours}ч. {ban2.Left.Minutes}мин.",
                    ban2.banReason);
                return;
            }
            BanInformation ban3 = AekinDatabase.Instance.Bans.GetBanByName(Name);
            if (ban3 != null && !ban3.Expired)
            {
                KickByBan(
                    ban3.administrator.name,
                    $"{ban3.Left.Days}д. {ban3.Left.Hours}ч. {ban3.Left.Minutes}мин.",
                    ban3.banReason);
                return;
            }
        }

        public bool CanBuild(int x, int y)
        {
            IEnumerable<Region> regions = AekinDatabase.Instance.Regions.GetRegionsCachable().Where((pair) => new Rectangle(pair.startX, pair.startY, pair.endX - pair.startX, pair.endY - pair.startY).Contains(x,y ));
            Region region = new Region();
            if (regions != null && regions.Count() != 0)
                foreach (Region rg in regions)
                {
                    if (rg != null)
                    {
                        region = rg;
                        break;
                    }
                }
            else return true;

            bool hasBuildAccess = region == null || (Account != null &&
                (region.owner == Account.name
                || region.members.Contains(Account.name)))
                || HasPermission("aekin.regions.edit");
            return hasBuildAccess;
        }
        public bool CanBuild(int x, int y, int w, int h)
        {
            IEnumerable<Region> regions = AekinDatabase.Instance.Regions.GetRegionsCachable().Where((pair) => new Rectangle(pair.startX, pair.startY, pair.endX - pair.startX, pair.endY - pair.startY).Intersects(new Rectangle(x, y, w, h)));
            Region region = new Region();
            if (regions != null && regions.Count() != 0)
                foreach (Region rg in regions)
                {
                    if (rg != null)
                    {
                        region = rg;
                        break;
                    }
                }
            else return true;

            bool hasBuildAccess = (Account != null &&
                (region.owner == Account.name
                || region.members.Contains(Account.name)))
                || HasPermission("aekin.regions.edit")
                || region == null;

            return hasBuildAccess;
        }

        public void Kick(string admin, string reason)
        {
            NetServer.Broadcast("Администратор " + admin + " исключил " + this.Name + ", по причине: " + reason);
            NetMessage.SendData(2, this.Index, -1, NetworkText.FromLiteral("» Вас исключили с сервера!\n  Причина: " + reason + "\n  Администратор: " + admin));
        }
        public void KickByBan(string admin, string date, string reason)
        {
            hideKick = true;
            NetMessage.SendData(2, this.Index, -1, NetworkText.FromLiteral("-- » Вы заблокированы!\n  Администратор: " + admin + "\n  Срок: " +  date + "\n  Причина: " + reason));
        }

        public void Login(Account account)
        {
            bool skip = false;
            AekinHookManager.AggregateHook((hs) =>
            {
                if (hs.OnLogin(this, account))
                    skip = true;
            });
            if (skip) return;

            Account = account;
            SendSSC();

            loggedIn = true;
            UpdateAccount();
        }

        public void UpdateAccount()
        {
            Account.clientUUID = data["PLAYER/DATA"].Get<string>("NET_UUID");
            Account.ip = data["PLAYER/DATA"].Get<string>("NET_IP");
            AekinDatabase.Instance.Accounts.PushAccount(Account);
        }

        public void SendSSC()
        {
            SSCHelper = new NetPlayerSSCHelper(this);
            SSCHelper.PushCharacter();
        }
        public void SendSSCv2()
        {
            SSCHelper = new NetPlayerSSCHelper(this);
            SSCHelper.PushSSCData();
        }

        public void Logout()
        {
           Account = new Account();
           Account.group = "unregistered";
           loggedIn = false;
        }

        public virtual void SendMainMessage(string message) => SendMessage(message, Color.DeepSkyBlue);
        public virtual void SendHeaderMessage(string message) => SendMessage("⎯ " + message + " ⎯", Color.DeepSkyBlue);
        public virtual void SendErrorMessage(string message) => SendMessage(message, Color.BlueViolet);
        public virtual void SendInfoMessage(string message) => SendMessage(message, Color.LightSkyBlue);
        public virtual void SendMessage(string message, Color c)
        {
            ChatHelper.SendChatMessageToClient(new Terraria.Localization.NetworkText(message, Terraria.Localization.NetworkText.Mode.Literal), c, this.Player.whoAmI);
        }
    }

    public sealed class AekinServerPlayer : NetPlayer
    {
        public static AekinServerPlayer Instance;

        public AekinServerPlayer() : base("Aekin Server", "player")
        {
        }

        public override string Name => "Aekin Server";

        public override bool HasPermission(string permission)
        {
            return true;
        }

        public override void SendHeaderMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("⎯ " + message + " ⎯");
            Console.ResetColor();
        }
        public override void SendErrorMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public override void SendInfoMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public override void SendMainMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public override void SendMessage(string message, Color c)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    public sealed class NetPlayerSSCHelper
    {
        private NetPlayer _plr;
        public NetPlayerSSCHelper(NetPlayer player)
        {
            this._plr = player;
        }

        public void PushCharacter()
        {
            _plr.Character = AekinDatabase.Instance.Characters.GetCharacter(_plr.Account.name);
            PushSSCData();
        }
        public void PushSSCData()
        {
            SendHealthData();
            SendManaData();
            SendInventoryData();
        }

        public void PushBuffs()
        {
            _plr.DataHelper.PushBuff(Terraria.ID.BuffID.Frozen, 180, false);
            _plr.DataHelper.PushBuff(Terraria.ID.BuffID.Stoned, 180, false);
            _plr.DataHelper.PushBuff(Terraria.ID.BuffID.Webbed, 180, false);
        }

        private void SendHealthData()
        {
            foreach (NetPlayer globalPlr in NetServer.Players)
                if (globalPlr != null)
                    globalPlr.DataHelper.PushHealth((short)_plr.Character.LifeMax, (short)_plr.Character.LifeMax, _plr.Index);
        }
        private void SendManaData()
        {
            foreach (NetPlayer globalPlr in NetServer.Players)
                if (globalPlr != null)
                    globalPlr.DataHelper.PushMana((short)_plr.Character.ManaMax, (short)_plr.Character.ManaMax, _plr.Index);
        }
        private void SendInventoryData()
        {
            for (int i = 0; i < 260; i++)
            {
                NetItem item = _plr.Character.InventoryData[i];

                foreach (NetPlayer globalPlr in NetServer.Players)
                    if (globalPlr != null)
                        globalPlr.DataHelper.PushSlot(i, item.ID, item.Stack, (byte)item.Prefix, _plr.Index);
            }
        }
    }
    public sealed class NetPlayerDataHelper
    {
        private NetPlayer _plr;
        public NetPlayerDataHelper(NetPlayer plr)
        {
            _plr = plr;
        }

        public void PushEmptyProjectile(short id)
        {
            PacketWriter packet = new PacketWriter()
                .SetType(27)
                .PackInt16(id)
                .PackSingle(-1f)
                .PackSingle(-1f)
                .PackSingle(-1f)
                .PackSingle(-1f)
                .PackByte((byte)_plr.Index)
                .PackInt16(0)
                .PackByte(0)
                .PackSingle(-1f)
                .PackSingle(-1f)
                .PackInt16(0)
                .PackByte(0)
                .PackInt16(0)
                .PackInt16(0);

            byte[] bytes = packet.GetByteData();
            Netplay.Clients[_plr.Index].Socket.AsyncSend(bytes, 0, bytes.Length, new SocketSendCallback(Netplay.Clients[_plr.Index].ServerWriteCallBack), null);
        }
        public void PushSlot(int slot, int netId, int stack, byte prefix, int fromPlr)
        {
            PacketWriter packet = new PacketWriter()
                .SetType(5)
                .PackByte((byte)fromPlr)
                .PackInt16((short)slot)
                .PackInt16((short)stack)
                .PackByte((byte)prefix)
                .PackInt16((short)netId);

            byte[] bytes = packet.GetByteData();
            Netplay.Clients[_plr.Index].Socket.AsyncSend(bytes, 0, bytes.Length, new SocketSendCallback(Netplay.Clients[_plr.Index].ServerWriteCallBack), null);
        }
        public void PushHealth(short hp, short hpMax, int fromPlr)
        {
            PacketWriter packet = new PacketWriter()
                .SetType(16)
                .PackByte((byte)fromPlr)
                .PackInt16((short)hp)
                .PackInt16((short)hpMax);

            byte[] bytes = packet.GetByteData();
            Netplay.Clients[_plr.Index].Socket.AsyncSend(bytes, 0, bytes.Length, new SocketSendCallback(Netplay.Clients[_plr.Index].ServerWriteCallBack), null);
        }
        public void PushMana(short mp, short mpMax, int fromPlr)
        {
            PacketWriter packet = new PacketWriter()
                .SetType(42)
                .PackByte((byte)fromPlr)
                .PackInt16((short)mp)
                .PackInt16((short)mpMax);

            byte[] bytes = packet.GetByteData();
            Netplay.Clients[_plr.Index].Socket.AsyncSend(bytes, 0, bytes.Length, new SocketSendCallback(Netplay.Clients[_plr.Index].ServerWriteCallBack), null);
        }
        public bool SendTileRectangle(int x, int y, int width = 10, int height = 10, TileChangeType changeType = TileChangeType.None)
        {
            try
            {
                NetMessage.SendTileSquare(_plr.Index, x, y, width, height, changeType);
                return true;
            }
            catch
            {
            }
            return false;
        }
        public bool Teleport(float x, float y, byte style = 1)
        {
            NetMessage.SendTileSquare(_plr.Index, (int)(x / 16f), (int)(y / 16f), 15, 0);
            _plr.Player.Teleport(new Vector2(x, y), (int)style, 0);
            NetMessage.SendData(65, -1, -1, NetworkText.Empty, 0, (float)_plr.Player.whoAmI, x, y, (int)style, 0, 0);
            return true;
        }
        public void PushBuff(int type, int time = 3600, bool bypass = false)
        {
            NetMessage.SendDataDirect((int)PacketTypes.PlayerAddBuff, _plr.Index, -1, null, _plr.Index, (float)type, (float)time, 0f, 0);
        }
    }
    public sealed class NetPlayerDataContainer
    {
        private Dictionary<string, object> _data = new Dictionary<string, object>();

        public void Push(string name, object value)
        {
            name = name.ToUpper();

            if (_data.ContainsKey(name))
            {
                _data.Remove(name);
            }

            _data.Add(name, value);
        }
        public T Get<T>(string name)
        {
            name = name.ToUpper();
            return (T)(_data.ContainsKey(name) ? _data[name] : null);
        }

    }

    public sealed class PacketWriter
    {
        public PacketWriter()
        {
            this.memoryStream = new MemoryStream();
            this.writer = new BinaryWriter(this.memoryStream);
            this.writer.BaseStream.Position = 3L;
        }
        public PacketWriter SetType(byte type)
        {
            long position = this.writer.BaseStream.Position;
            this.writer.BaseStream.Position = 2L;
            this.writer.Write(type);
            this.writer.BaseStream.Position = position;
            return this;
        }
        public PacketWriter PackSByte(sbyte num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackByte(byte num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackInt16(short num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackUInt16(ushort num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackInt32(int num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackUInt32(uint num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackUInt64(ulong num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackSingle(float num)
        {
            this.writer.Write(num);
            return this;
        }
        public PacketWriter PackString(string str)
        {
            this.writer.Write(str);
            return this;
        }
        public PacketWriter PackColor(byte r, byte g, byte b)
        {
            this.writer.Write(r);
            this.writer.Write(g);
            this.writer.Write(b);
            return this;
        }
        public PacketWriter PackBoolean(bool status)
        {
            this.writer.Write(status);
            return this;
        }
        private void UpdateLength()
        {
            long position = this.writer.BaseStream.Position;
            this.writer.BaseStream.Position = 0L;
            this.writer.Write((short)position);
            this.writer.BaseStream.Position = position;
        }
        public byte[] GetByteData()
        {
            this.UpdateLength();
            return this.memoryStream.ToArray();
        }

        public MemoryStream memoryStream;
        public BinaryWriter writer;
    }
}
