using AekinConvict.Core;
using Mono.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AekinConvict.Databases
{
    public abstract class Database
    {
        public string Name { get; private set; }
        public DatabaseSectionsContainer Sections { get; private set; }
        public string DbFile => "data/databases/" + Name + "DB.sqlite";

        internal SqliteConnection DbConnection { get; private set;  }

        public Database(string name)
        {
            Name = name;
            Sections = new DatabaseSectionsContainer();
        }

        public virtual void Initialize()
        {
            Directory.CreateDirectory("data/databases/");
            bool createDb = !File.Exists(DbFile);

            DbConnection = new SqliteConnection(string.Format("Data Source=" + DbFile, Array.Empty<object>()));
            DbConnection.Open();

            DatabaseSection[] sections = InitializeSections();
            foreach (DatabaseSection section in sections)
            {
                try
                {
                    DbConnection.MakeTable(section.Name, section.TableFormat);
                }
                catch { }
                Sections.Push(section.Name, section);
            }
        }
        public abstract DatabaseSection[] InitializeSections();
    }

    public sealed class AekinDatabase : Database
    {
        public static AekinDatabase Instance;

        public CharactersSection Characters => Sections.Get("Characters") as CharactersSection;
        public AccountsSection Accounts => Sections.Get("Accounts") as AccountsSection;
        public GroupsSection Groups => Sections.Get("Groups") as GroupsSection;
        public RegionsSection Regions => Sections.Get("Regions") as RegionsSection;
        public BansSection Bans => Sections.Get("Bans") as BansSection;

        public static void LoadDatabase()
        {
            Instance = new AekinDatabase();
            Instance.Initialize();
        }

        public AekinDatabase() : base("Aekin") { }

        public override void Initialize()
        {
            base.Initialize();

            if (Groups.GetGroup("unregistered") == null || Groups.GetGroup("player") == null)
            {
                Groups.PushGroup(new Group("unregistered", "aekin.help,aekin.accounts.auth", "[[c/525252:Гость]]", 155, 155, 155, ""));
                Groups.PushGroup(new Group("player", "aekin.help,aekin.accounts.unauth,aekin.whispers,aekin.help,aekin.world.edit", "[[c/ff715e:Игрок]]", 255, 255, 255, ""));
            }

        }

        public override DatabaseSection[] InitializeSections()
        {
            return new DatabaseSection[]
            {
                new AccountsSection(this),
                new CharactersSection(this),
                new RegionsSection(this),
                new GroupsSection(this),
                new BansSection(this)
            };
        }
    }

    public sealed class DatabaseSectionsContainer
    {
        private Dictionary<string, DatabaseSection> _data = new Dictionary<string, DatabaseSection>();

        public void Push(string name, DatabaseSection value)
        {
            name = name.ToUpper();

            if (_data.ContainsKey(name))
            {
                _data.Remove(name);
            }

            _data.Add(name, value);
        }
        public DatabaseSection Get(string name)
        {
            name = name.ToUpper();
            return _data.ContainsKey(name) ? _data[name] : null;
        }
    }
    public abstract class DatabaseSection
    {
        internal AekinDatabase AekinDB { get; private set; }
        internal SqliteConnection DbConnection => AekinDB.DbConnection;


        public DatabaseSection(AekinDatabase db)
        {
            AekinDB = db;
        }

        public abstract string TableFormat { get; }
        public abstract string Name { get; }
    }

    public sealed class AccountsSection : DatabaseSection
    {
        public AccountsSection(AekinDatabase db) : base(db) { }

        public override string TableFormat => "'Name' TEXT, 'Password' TEXT, 'GroupName' TEXT, 'IP' TEXT, 'ClientUUID' TEXT, 'FakeGroup' TEXT, 'Token' TEXT";
        public override string Name => "Accounts";

        public Account GetAccount(string name)
        {
            using (SqliteDataReader reader = DbConnection.ReadDB(string.Format("SELECT * FROM Accounts WHERE Name='{0}'", name)))
            {
                if (reader.HasRows && reader.Read())
                {
                    string accountName = reader.Read<string>(0);
                    string password = reader.Read<string>(1);
                    string group = reader.Read<string>(2);
                    string ip = reader.Read<string>(3);
                    string clientUUID = reader.Read<string>(4);
                    string fg = reader.Read<string>(5);
                    string hotOp = reader.Read<string>(6);

                    return new Account(accountName, password, group, ip, clientUUID, fg, hotOp);
                }
            }

            return null;
        }
        public void PushAccount(Account account)
        {
            if (GetAccount(account.name) != null)
            {
                DbConnection.WriteDB(string.Format("UPDATE Accounts SET Password='{1}', GroupName='{2}', IP='{3}', ClientUUID='{4}', FakeGroup='{5}', Token='{6}' WHERE Name='{0}'", account.name, account.password, account.group, account.ip, account.clientUUID, account.fakeGroup, account.rootToken));
                return;
            }
            DbConnection.WriteDB(string.Format("INSERT INTO Accounts (Name, Password, GroupName, IP, ClientUUID, FakeGroup, Token) VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}')", account.name, account.password, account.group, account.ip, account.clientUUID, account.fakeGroup, account.rootToken));
            Character createdData = new Character(new NetItem[260], 100, 20);
            for (int i = 0; i < AekinConfig.Instance.SSCDefaultItems.Length; i++)
                createdData.InventoryData[i] = AekinConfig.Instance.SSCDefaultItems[i];
            DbConnection.WriteDB(string.Format("INSERT INTO Characters (Account, Data) VALUES ('{0}', '{1}')", account.name, JsonConvert.SerializeObject(createdData, Formatting.Indented)));
        }
        public void PopAccount(string name)
        {
            DbConnection.WriteDB(string.Format("DELETE FROM Accounts WHERE Name='{0}'", name));
        }
    }
    public sealed class CharactersSection : DatabaseSection
    {
        public CharactersSection(AekinDatabase db) : base(db) { }

        public override string TableFormat => "'Account' TEXT, 'Data' TEXT";
        public override string Name => "Characters";

        public void PushCharacter(string account, Character data)
        {
            DbConnection.WriteDB(string.Format("UPDATE Characters SET Data='{1}' WHERE Account='{0}'", account, JsonConvert.SerializeObject(data, Formatting.Indented)));
        }
        public Character GetCharacter(string name)
        {
            using (SqliteDataReader reader = DbConnection.ReadDB(string.Format("SELECT * FROM Characters WHERE Account='{0}'", name)))
            {
                if (reader.HasRows && reader.Read())
                {
                    string account = reader.Read<string>(0);
                    string data = reader.Read<string>(1);

                    return JsonConvert.DeserializeObject<Character>(data);
                }
            }
            return default(Character);
        }
    }
    public sealed class RegionsSection : DatabaseSection
    {
        public RegionsSection(AekinDatabase db) : base(db) { }

        public override string TableFormat => "'Name' TEXT, 'Owner' TEXT, 'Members' TEXT, 'X' INTEGER, 'Y' INTEGER, 'X2' INTEGER, 'Y2' INTEGER";
        public override string Name => "Regions";

        private List<Region> _regions;

        public Region GetRegion(string name)
        {
            using (SqliteDataReader reader = DbConnection.ReadDB(string.Format("SELECT * FROM Regions WHERE Name='{0}'", name)))
            {
                if (reader.HasRows && reader.Read())
                {
                    string regionName = reader.Read<string>(0);
                    string owner = reader.Read<string>(1);
                    string members = reader.Read<string>(2);
                    int x = (int)reader.Read<long>(3);
                    int y = (int)reader.Read<long>(4);
                    int x2 = (int)reader.Read<long>(5);
                    int y2 = (int)reader.Read<long>(5);

                    return new Region(regionName, owner, JsonConvert.DeserializeObject<List<string>>(members), x, y, x2, y2);
                }
            }

            return null;
        }
        public void DeleteRegion(string name)
        {
            _regions.RemoveAll((p) => p.name == name);
            DbConnection.WriteDB(string.Format("DELETE FROM Regions WHERE Name='{0}'", name));
        }
        public List<Region> GetRegionsCachable()
        {
            if (_regions == null)
            {
                _regions = GetRegions().ToList();
            }

            return _regions;
        }
        public IEnumerable<Region> GetRegions()
        {
            using (SqliteDataReader reader = DbConnection.ReadDB("SELECT * FROM Regions"))
            {
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        string regionName = reader.Read<string>(0);
                        string owner = reader.Read<string>(1);
                        string members = reader.Read<string>(2);
                        int x = (int)reader.Read<long>(3);
                        int y = (int)reader.Read<long>(4);
                        int x2 = (int)reader.Read<long>(5);
                        int y2 = (int)reader.Read<long>(5);

                        yield return new Region(regionName, owner, JsonConvert.DeserializeObject<List<string>>(members), x, y, x2, y2);
                    }
                }
            }
        }
        public void AddRegion(Region region)
        {
            _regions.Add(region);
            DbConnection.WriteDB(string.Format("INSERT INTO Regions (Name, Owner, Members, X, Y, X2, Y2) VALUES ('{0}', '{1}', '{2}', {3}, {4}, {5}, {6})", region.name, region.owner, JsonConvert.SerializeObject(region.members, Formatting.Indented), region.startX, region.startY, region.endX, region.endY));
        }
        public void UpdateRegion(Region region)
        {
            _regions.RemoveAll((p) => p.name == region.name);
            _regions.Add(region);
            DbConnection.WriteDB(string.Format("UPDATE Regions SET Owner='{1}', Members='{2}', X={3}, Y={4}, X2={5}, Y2={6} WHERE Name='{0}'", region.name, region.owner, JsonConvert.SerializeObject(region.members, Formatting.Indented), region.startX, region.startY, region.endX, region.endY));
        }
    }
    public sealed class GroupsSection : DatabaseSection
    {
        public GroupsSection(AekinDatabase db) : base(db) { }

        public override string TableFormat => "'Name' TEXT, 'Permissions' TEXT, 'Prefix' TEXT, 'Red' INTEGER, 'Green' INTEGER, 'Blue' INTEGER, 'Parent' TEXT";
        public override string Name => "Groups";

        private List<Group> _groups;

        public Group GetGroup(string name)
        {
            if (_groups == null) _groups = new List<Group>();

            if (name == "")
                return null;

            if (_groups.Any((p) => p.name == name))
                return _groups.Find((p) => p.name == name);

            using (SqliteDataReader reader = DbConnection.ReadDB(string.Format("SELECT * FROM Groups WHERE Name='{0}'", name)))
            {
                if (reader.HasRows && reader.Read())
                {
                    string groupName = reader.Read<string>(0);
                    string permissions = reader.Read<string>(1);
                    string prefix = reader.Read<string>(2);
                    byte red = (byte)reader.Read<long>(3);
                    byte green = (byte)reader.Read<long>(4);
                    byte blue = (byte)reader.Read<long>(5);
                    string parent = reader.Read<string>(6);

                    Group group = new Group(groupName, permissions, prefix, red, green, blue, parent);
                    if (!_groups.Any((p) => p.name == name))
                        _groups.Add(group);

                    return group;
                }
            }

            return null;
        }
        public IEnumerable<Group> GetGroups()
        {
            using (SqliteDataReader reader = DbConnection.ReadDB("SELECT * FROM Groups"))
            {
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        string groupName = reader.Read<string>(0);
                        string permissions = reader.Read<string>(1);
                        string prefix = reader.Read<string>(2);
                        byte red = (byte)reader.Read<long>(3);
                        byte green = (byte)reader.Read<long>(4);
                        byte blue = (byte)reader.Read<long>(5);
                        string parent = reader.Read<string>(6);

                        yield return new Group(groupName, permissions, prefix, red, green, blue, parent);
                    }
                }
            }
        }
        public void PushGroup(Group group)
        {
            if (GetGroup(group.name) != null)
            {
                DbConnection.WriteDB(string.Format("UPDATE Groups SET Permissions='{1}', Prefix='{2}', Red={3}, Green={4}, Blue={5}, Parent='{6}' WHERE Name='{0}'", group.name, group.permissions, group.prefix, group.r, group.g, group.b, group.parent == null ? "" : group.parent.name));
                return;
            }
            DbConnection.WriteDB(string.Format("INSERT INTO Groups (Name, Permissions, Prefix, Red, Green, Blue, Parent) VALUES ('{0}', '{1}', '{2}', {3}, {4}, {5}, '{6}')", group.name, group.permissions, group.prefix, group.r, group.g, group.b, group.parent == null ? "" : group.parent.name));
        }
        public void PopGroup(string name)
        {
            if (_groups == null) _groups = new List<Group>();
            _groups.RemoveAll((p) => p.name == name);
            DbConnection.WriteDB(string.Format("DELETE FROM Groups WHERE Name='{0}'", name));
        }
    }
    public sealed class BansSection : DatabaseSection
    {
        public BansSection(AekinDatabase db) : base(db) { }

        public override string TableFormat => "'VictimAccount' TEXT, 'VictimIP' TEXT, 'VictimUUID' TEXT, 'BanTime' TEXT, 'Administrator' TEXT, 'Reason' TEXT";
        public override string Name => "Bans";

        public BanInformation GetBanByName(string victim)
        {
            using (SqliteDataReader reader = DbConnection.ReadDB(string.Format("SELECT * FROM Bans WHERE VictimAccount='{0}'", victim)))
            {
                if (reader.HasRows && reader.Read())
                {
                    victim = reader.Read<string>(0);
                    string victimIp = reader.Read<string>(1);
                    string victimUuid = reader.Read<string>(2);
                    string banTime = reader.Read<string>(3);
                    string administrator = reader.Read<string>(4);
                    string reason = reader.Read<string>(5);

                    BanInformation info = new BanInformation(victim, victimIp, victimUuid, administrator, reason, banTime);
                    return info;
                }
            }

            return null;
        }
        public BanInformation GetBanByIP(string ip)
        {
            using (SqliteDataReader reader = DbConnection.ReadDB(string.Format("SELECT * FROM Bans WHERE VictimIP='{0}'", ip)))
            {
                if (reader.HasRows && reader.Read())
                {
                    string victim = reader.Read<string>(0);
                    string victimIp = reader.Read<string>(1);
                    string victimUuid = reader.Read<string>(2);
                    string banTime = reader.Read<string>(3);
                    string administrator = reader.Read<string>(4);
                    string reason = reader.Read<string>(5);

                    BanInformation info = new BanInformation(victim, victimIp, victimUuid, administrator, reason, banTime);
                    return info;
                }
            }

            return null;
        }
        public BanInformation GetBanByUUID(string uuid)
        {
            using (SqliteDataReader reader = DbConnection.ReadDB(string.Format("SELECT * FROM Bans WHERE VictimUUID='{0}'", uuid)))
            {
                if (reader.HasRows && reader.Read())
                {
                    string victim = reader.Read<string>(0);
                    string victimIp = reader.Read<string>(1);
                    string victimUuid = reader.Read<string>(2);
                    string banTime = reader.Read<string>(3);
                    string administrator = reader.Read<string>(4);
                    string reason = reader.Read<string>(5);

                    BanInformation info = new BanInformation(victim, victimIp, victimUuid, administrator, reason, banTime);
                    return info;
                }
            }

            return null;
        }
        public IEnumerable<BanInformation> GetBans()
        {
            using (SqliteDataReader reader = DbConnection.ReadDB("SELECT * FROM Bans"))
            {
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        string victim = reader.Read<string>(0);
                        string victimIp = reader.Read<string>(1);
                        string victimUuid = reader.Read<string>(2);
                        string banTime = reader.Read<string>(3);
                        string administrator = reader.Read<string>(4);
                        string reason = reader.Read<string>(5);

                        BanInformation info = new BanInformation(victim, victimIp, victimUuid, administrator, reason, banTime);
                        yield return info;
                    }
                }
            }
        }
        public void AddBan(BanInformation ban)
        {
            if (GetBanByName(ban.victim.name) != null)
                DeleteBan(ban.victim.name);

            if (GetBanByIP(ban.accountIp) != null)
                DeleteBanByIP(ban.victim.ip);

            DbConnection.WriteDB(string.Format("INSERT INTO Bans (VictimAccount, VictimIP, VictimUUID, BanTime, Administrator, Reason) VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}')", ban.victim.name, ban.accountIp, ban.accountUuid, ban.banExpiration.ToString(), ban.administrator.name, ban.banReason));
        }
        public void DeleteBan(string victim)
        {
            DbConnection.WriteDB(string.Format("DELETE FROM Bans WHERE VictimAccount='{0}'", victim));
        }
        public void DeleteBanByIP(string ip)
        {
            DbConnection.WriteDB(string.Format("DELETE FROM Bans WHERE VictimIP='{0}'", ip));
        }
        public void DeleteBanByUUID(string uuid)
        {
            DbConnection.WriteDB(string.Format("DELETE FROM Bans WHERE VictimUUID='{0}'", uuid));
        }
    }

    public static class DatabaseExtensions
    {
        public static void WriteDB(this SqliteConnection connection, string command)
        {
            if (!command.EndsWith(";"))
                command += ";";

            SqliteCommand sqliteCommand = new SqliteCommand()
            {
                Connection = connection,
                CommandText = command
            };
            sqliteCommand.ExecuteNonQuery();
        }
        public static SqliteDataReader ReadDB(this SqliteConnection connection, string command)
        {
            if (!command.EndsWith(";"))
                command += ";";

            SqliteCommand sqliteCommand = new SqliteCommand()
            {
                Connection = connection,
                CommandText = command
            };
            return sqliteCommand.ExecuteReader();
        }
        public static void MakeTable(this SqliteConnection connection, string tableName, string parameters)
        {
            string command = string.Format("CREATE TABLE {0}({1});", tableName, parameters);
            SqliteCommand sqliteCommand = new SqliteCommand()
            {
                Connection = connection,
                CommandText = command
            };
            sqliteCommand.ExecuteNonQuery();
        }
        public static T Read<T>(this SqliteDataReader reader, int index)
        {
            return (T)reader.GetValue(index);
        }
    }
    public class Account
    {
        public Account(string name, string password, string group, string ip, string clientUUID, string fakeGroup, string hotOpToken = "")
        {
            this.name = name;
            this.password = password;
            this.group = group;
            this.ip = ip;
            this.clientUUID = clientUUID;
            this.fakeGroup = fakeGroup;

            this.rootToken = hotOpToken;
        }
        public Account()
        {
            this.loginToken = "";
        }

        internal string rootToken;
        public string name;
        public string password;
        public string group;
        public string ip;
        public string fakeGroup;
        public string clientUUID;
        public string loginToken;
    }
    public class Group
    {
        public Group(string name, string perms, string prefix, byte r, byte g, byte b, string parent)
        {
            this.name = name;
            this.permissions = perms;
            this.prefix = prefix;
            this.r = r;
            this.g = g;
            this.b = b;
            if (!string.IsNullOrEmpty(parent))
            this.parent = AekinDatabase.Instance.Groups.GetGroup(parent); 
        }
        public bool HasPermission(string permission)
        {
            if (parent != null && parent.HasPermission(permission))
                return true;

            if (permission.StartsWith("root."))
                return false;

            if (permission == "")
                return true;

            List<string> p = permissions.Split(',').ToList();
            string[] array = permission.Split(new char[]
            {
                '.'
            });

            if (p.Contains(permission))
            {
                return true;
            }
            for (int i = array.Length - 1; i >= 0; i--)
            {
                array[i] = "*";
                if (p.Contains(string.Join(".", array, 0, i + 1)))
                {
                    return true;
                }
            }
            return false;
        }

        public Group parent;
        public string name;
        public string permissions;
        public string prefix;
        public byte r;
        public byte g;
        public byte b;
    }
    public class Region
    {
        public Region(string name, string owner, List<string> members, int startX, int startY, int endX, int endY)
        {
            this.name = name;
            this.owner = owner;
            this.members = members;
            this.startX = startX;
            this.startY = startY;
            this.endX = endX;
            this.endY = endY;
        }

        public Region()
        {
        }

        public string name;

        public string owner;
        public List<string> members;

        public int startX;
        public int startY;

        public int endX;
        public int endY;
    }
    public class BanInformation
    {
        public BanInformation(string victim, string ip, string uuid, string administrator, string banReason, string banExpiration)
        {
            this.accountIp = ip;
            this.accountUuid = uuid;
            this.banReason = banReason;
            this.victim = AekinDatabase.Instance.Accounts.GetAccount(victim);
            this.administrator = AekinDatabase.Instance.Accounts.GetAccount(administrator);
            DateTime banOccured;

            if (DateTime.TryParse(banExpiration, out banOccured))
            {
                this.banExpiration = banOccured;
            }
        }
        public BanInformation(Account victim, Account administrator, string banReason, DateTime banExpiration)
        {
            this.accountIp = victim.ip;
            this.accountUuid = victim.clientUUID;
            this.banReason = banReason;
            this.victim = victim;
            this.administrator = administrator;
            this.banExpiration = banExpiration;
        }

        public bool Expired => banExpiration < DateTime.UtcNow;
        public TimeSpan Left => banExpiration - DateTime.UtcNow;

        public readonly Account victim;
        public readonly string accountIp;
        public readonly string accountUuid;
        public readonly DateTime banExpiration;
        public readonly Account administrator;
        public readonly string banReason;
    }
    public struct Character
    {
        public Character(NetItem[] inv, int maxhp, int maxmp)
        {
            InventoryData = inv;
            LifeMax = maxhp;
            ManaMax = maxmp;
        }

        public NetItem[] InventoryData;
        public int LifeMax;
        public int ManaMax;
    }
    public struct NetItem
    {
        public NetItem(int id, int stack, int prefix)
        {
            ID = id;
            Stack = stack;
            Prefix = prefix;
        }

        public int ID;
        public int Stack;
        public int Prefix;
    }
}
