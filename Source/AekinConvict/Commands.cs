using AekinConvict.Core;
using AekinConvict.Databases;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.IO;

namespace AekinConvict.Commands
{
    public static class CommandHelper
    {
        public static List<Command> RegisteredCommands;

        public static void Initialize()
        {
            RegisteredCommands = new List<Command>();
            RegisteredCommands.Add(new CommandBuilder(Register, false, "register", "reg")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Login, false, "login", "logi")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Logout, false, "logout", "logo")
                .WithPermission("aekin.account.deauth")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Help, false, "help")
                .WithPermission("aekin.help")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Tell, false, "whisper", "w")
                .WithPermission("aekin.whisper")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Respond, false, "respond", "r")
                .WithPermission("aekin.whisper")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Region, false, "region", "rg")
                .WithPermission("aekin.manage.regions")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Group, false, "group", "grp")
                .WithPermission("aekin.manage.group")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Account, false, "accounts", "acc")
                .WithPermission("aekin.manage.account")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Ban, false, "ban")
                .WithPermission("aekin.manage.ban")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Kick, false, "kick")
                .WithPermission("aekin.manage.kick")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(AekinRootToken, false, "login-root")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(OnlinePlayers, false, "who", "onlineplayers", "online", "players")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Spawn, false, "spawn", "sp")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(TeleportTo, false, "teleport", "tp")
                .WithPermission("aekin.mod.tp.to")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(TeleportHere, false, "teleporthere", "tph")
                .WithPermission("aekin.mod.tp.here")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(Broadcast, false, "broadcast", "bc", "say")
                .WithPermission("aekin.admin.broadcast")
                .Build());
            RegisteredCommands.Add(new CommandBuilder(OffServer, true, "off")
                .WithPermission("root.off")
                .Build());
        }
        private static void Ban(NetPlayer player, List<string> args)
        {
            if (args.Count < 3)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /ban <push | pop | info> [аргументы].");
                return;
            }
            if (args[1] == "pop")
            {
                string accountName = args[2];

                if (AekinDatabase.Instance.Bans.GetBanByName(accountName) != null)
                    AekinDatabase.Instance.Bans.DeleteBan(accountName);

                player.SendMainMessage("Аккаунт " + accountName + " разблокирован.");
                return;
            }
            if (args[1] == "info")
            {
                string accountName = args[2];

                BanInformation ban = AekinDatabase.Instance.Bans.GetBanByName(accountName);
                if (ban == null)
                {
                    player.SendErrorMessage("Блокировка не найдена.");
                    return;
                }

                player.SendHeaderMessage("Информация о блокировке");
                player.SendInfoMessage($"Аккаунт: {ban.victim.name}");
                player.SendInfoMessage($"IP: {ban.accountIp}");
                player.SendInfoMessage($"UUID: {ban.accountUuid}");
                player.SendInfoMessage($"Администратор: {ban.administrator}");
                player.SendInfoMessage($"Истекает через: {ban.Left.Days}д. {ban.Left.Hours}ч. {ban.Left.Minutes}мин.");
                player.SendInfoMessage($"Причина: {ban.banReason}");
                return;
            }
            if (args.Count < 4)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /ban <push | pop | info> [аргументы].");
                return;
            }
            if (args[1] == "push")
            {
                string accountName = args[2];
                int seconds; // 7 дней
                string reason = args[3];
                string time = args.Count < 5 ? "7d" : args[4];


                Account account = AekinDatabase.Instance.Accounts.GetAccount(accountName);
                if (account == null)
                {
                    player.SendErrorMessage("Аккаунт не найден.");
                    return;
                }

                TryParseTime(time, out seconds);
                DateTime date = DateTime.UtcNow.AddSeconds(seconds);

                if (AekinDatabase.Instance.Bans.GetBanByName(accountName) != null)
                    AekinDatabase.Instance.Bans.DeleteBan(accountName);

                BanInformation ban = new BanInformation(account, player.Account, reason, date);
                AekinDatabase.Instance.Bans.AddBan(ban);

                NetServer.Broadcast("[c/2137ff:Блокировки:] Игрок " + account.name + " был заблокирован администратором " + player.Name + ", по причине: " + reason);

                foreach (NetPlayer plr in NetServer.Players)
                    if (plr.Account.name == account.name)
                    {
                        plr.KickByBan(player.Name, $"{ban.Left.Days}д. {ban.Left.Hours}ч. {ban.Left.Minutes}мин.", reason);
                    }

                player.SendMainMessage("Аккаунт " + accountName + " заблокирован.");
                return;
            }
        }
        private static void Kick(NetPlayer player, List<string> args)
        {
            if (args.Count < 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /kick <игрок>.");
                return;
            }
            else
            {
                NetPlayer foundPlayer = NetPlayer.Find(args[1]);

                if (foundPlayer != null)
                {
                    foundPlayer.Kick(player.Name, args.Count > 2 ? args[2] : "без причины");
                }
                else
                {
                    player.SendErrorMessage("Игрок не найден.");
                }
            }
        }
        private static void OffServer(NetPlayer player, List<string> args)
        {
            NetServer.Broadcast("Отключение сервера...");
            WorldFile.SaveWorld(false, false);
            Environment.Exit(0);
        }
        private static void Broadcast(NetPlayer player, List<string> args)
        {
            NetServer.Broadcast("[c/2137ff:Объявление:] " + string.Join(" ", args.Skip(1)), Color.White);
        }
        private static void OnlinePlayers(NetPlayer player, List<string> args)
        {
            List<string> plrs = NetServer.Players.Where((p) => p != null && p.Player.active).Select((p) => p.Name).ToList();
            player.SendMainMessage("Игроки онлайн (" + plrs.Count + "/" + Main.maxNetPlayers + "):");
            player.SendInfoMessage(string.Join(", ", plrs));
        }
        private static void Spawn(NetPlayer player, List<string> args)
        {
            if (player.DataHelper.Teleport((float)(Main.spawnTileX * 16), (float)(Main.spawnTileY * 16 - 48), 1))
            {
                player.SendMainMessage("Телепортация на точку возрождения...");
            }
        }
        private static void TeleportTo(NetPlayer player, List<string> args)
        {
            if (args.Count != 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /tp <игрок>.");
                return;
            }
            else
            {
                NetPlayer foundPlayer = NetPlayer.Find(args[1]);

                if (foundPlayer != null)
                {
                    player.DataHelper.Teleport(foundPlayer.Player.position.X, foundPlayer.Player.position.Y, 2);
                    player.SendInfoMessage("Телепортация к " + foundPlayer.Name + "...");
                }
                else
                {
                    player.SendErrorMessage("Игрок не найден.");
                }
            }
        }
        private static void TeleportHere(NetPlayer player, List<string> args)
        {
            if (args.Count != 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /tphere <игрок>.");
                return;
            }
            else
            {
                NetPlayer foundPlayer = NetPlayer.Find(args[1]);

                if (foundPlayer != null)
                {
                    foundPlayer.DataHelper.Teleport(player.Player.position.X, player.Player.position.Y, 2);
                    player.SendInfoMessage("Телепортация " + foundPlayer.Name + " к себе...");
                }
                else
                {
                    player.SendErrorMessage("Игрок не найден.");
                }
            }
        }
        private static void Group(NetPlayer player, List<string> args)
        {
            if (args.Count < 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /group <list | pop | push | prefix | addperm | delperm | addcmd | delcmd | parent> [аргументы].");
                return;
            }
            if (args[1] == "list")
            {
                List<Group> groups = AekinDatabase.Instance.Groups.GetGroups().ToList();

                player.SendHeaderMessage("Группы");
                player.SendInfoMessage(string.Join(", ", groups.Select((p) => p.name)));
                return;
            }
            if (args.Count < 3)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /group <list | pop | push | prefix | addperm | delperm | addcmd | delcmd | parent> [аргументы].");
                return;
            }
            if (args[1] == "push")
            {
                string groupName = args[2];

                AekinDatabase.Instance.Groups.PushGroup(new Group(groupName, "", "", 255, 255, 255, ""));

                player.SendMainMessage("Группа " + groupName + " отправлена.");
                return;
            }
            if (args[1] == "pop")
            {
                string groupName = args[2];

                AekinDatabase.Instance.Groups.PopGroup(groupName);

                player.SendMainMessage("Группа " + groupName + " отложена.");
                return;
            }
            if (args[1] == "listcmds")
            {
                string groupName = args[2];

                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                List<string> perms = group.permissions.Split(',').ToList();
                List<string> commands = CommandHelper.RegisteredCommands.Where((p) => perms.Contains(p.Permission)).Select((p) => p.Names[0]).ToList();

                player.SendHeaderMessage("Команды группы [" + groupName + "]");
                player.SendInfoMessage(string.Join(", ", commands));
                return;
            }
            if (args.Count < 4)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /group <list | pop | push | prefix | addperm | delperm | addcmd | delcmd | parent> [аргументы].");
                return;
            }
            if (args[1] == "prefix")
            {
                string groupName = args[2];
                string prefix = args[3];
                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                group.prefix = prefix;
                AekinDatabase.Instance.Groups.PushGroup(group);

                player.SendMainMessage("Группа " + group.name + " обновлена.");
                return;
            }
            if (args[1] == "parent")
            {
                string groupName = args[2];
                string parent = args[3];
                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }
                Group parentGroup = AekinDatabase.Instance.Groups.GetGroup(parent);
                if (parentGroup == null)
                {
                    player.SendErrorMessage("Группа-родитель не найдена.");
                    return;
                }

                group.parent = parentGroup;
                AekinDatabase.Instance.Groups.PushGroup(group);

                player.SendMainMessage("Группа " + group.name + " обновлена.");
                return;
            }
            if (args[1] == "addperm")
            {
                string groupName = args[2];
                string perm = args[3];
                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                List<string> perms = group.permissions.Split(',').ToList();
                perms.Add(perm);
                group.permissions = string.Join(",", perms);

                AekinDatabase.Instance.Groups.PushGroup(group);

                player.SendMainMessage("Группа " + group.name + " обновлена.");
                return;
            }
            if (args[1] == "delperm")
            {
                string groupName = args[2];
                string perm = args[3];
                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                List<string> perms = group.permissions.Split(',').ToList();
                perms.Remove(perm);
                group.permissions = string.Join(",", perms);

                AekinDatabase.Instance.Groups.PushGroup(group);

                player.SendMainMessage("Группа " + group.name + " обновлена.");
                return;
            }
            if (args[1] == "addcmd")
            {
                string groupName = args[2];
                string cmd = args[3];
                string perm = "-";

                foreach (Command icmd in CommandHelper.RegisteredCommands)
                {
                    if (icmd.Names.Any((p) => p == cmd))
                        perm = icmd.Permission;
                }

                if (perm == "-")
                {
                    player.SendErrorMessage("Команда не найдена.");
                    return;
                }
                if (perm == "")
                {
                    player.SendErrorMessage("У команды нет определенных прав для использования.");
                    return;
                }

                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                List<string> perms = group.permissions.Split(',').ToList();
                perms.Add(perm);
                group.permissions = string.Join(",", perms);

                AekinDatabase.Instance.Groups.PushGroup(group);

                player.SendMainMessage("Группа " + group.name + " обновлена.");
                return;
            }
            if (args[1] == "delcmd")
            {
                string groupName = args[2];
                string cmd = args[3];
                string perm = "-";

                foreach (Command icmd in CommandHelper.RegisteredCommands)
                {
                    if (icmd.Names.Any((p) => p == cmd))
                        perm = icmd.Permission;
                }

                if (perm == "-")
                {
                    player.SendErrorMessage("Команда не найдена.");
                    return;
                }
                if (perm == "")
                {
                    player.SendErrorMessage("У команды нет определенных прав для использования.");
                    return;
                }

                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                List<string> perms = group.permissions.Split(',').ToList();
                perms.Remove(perm);
                group.permissions = string.Join(",", perms);

                AekinDatabase.Instance.Groups.PushGroup(group);

                player.SendMainMessage("Группа " + group.name + " обновлена.");
                return;
            }
            if (args[1] == "parent")
            {
                string groupName = args[2];
                string parent = args[3];
                Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                group.parent = AekinDatabase.Instance.Groups.GetGroup(parent);
                AekinDatabase.Instance.Groups.PushGroup(group);

                player.SendMainMessage("Группа " + group.name + " обновлена.");
                return;
            }
        }
        private static void Account(NetPlayer player, List<string> args)
        {
            if (args.Count < 3)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /account <push | pop | group | password> [аргументы].");
                return;
            }
            if (args[1] == "pop")
            {
                string accountName = args[2];

                AekinDatabase.Instance.Accounts.PopAccount(accountName);

                player.SendMainMessage("Аккаунт " + accountName + " отложен.");
                return;
            }
            if (args.Count < 4)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /account <push | pop | group | password> [аргументы].");
                return;
            }
            if (args[1] == "push")
            {
                string accountName = args[2];
                string pwd = args[3];

                AekinDatabase.Instance.Accounts.PushAccount(new Databases.Account(accountName, pwd, "player", "-", "-", "", ""));

                player.SendMainMessage("Аккаунт " + accountName + " отправлен.");
                return;
            }
            if (args[1] == "group")
            {
                string plrName = args[2];
                string groupName = args[3];

                Account account = AekinDatabase.Instance.Accounts.GetAccount(plrName);
                if (account == null)
                {
                    player.SendErrorMessage("Аккаунт не найден.");
                    return;
                }

                Databases.Group group = AekinDatabase.Instance.Groups.GetGroup(groupName);
                if (group == null)
                {
                    player.SendErrorMessage("Группа не найдена.");
                    return;
                }

                account.group = group.name;
                AekinDatabase.Instance.Accounts.PushAccount(account);
                foreach (NetPlayer plr in NetServer.Players)
                    if (plr != null && plr.loggedIn && plr.Account.name == account.name)
                    {
                        plr.Account = account;
                    }

                player.SendMainMessage("Установлена группа " + group.name + " игроку " + account.name + ".");
                return;
            }
            if (args[1] == "password")
            {
                string plrName = args[2];
                string password = args[3];

                Account account = AekinDatabase.Instance.Accounts.GetAccount(plrName);
                if (account == null)
                {
                    player.SendErrorMessage("Аккаунт не найден.");
                    return;
                }

                account.password = password;
                AekinDatabase.Instance.Accounts.PushAccount(account);
                foreach (NetPlayer plr in NetServer.Players)
                    if (plr != null && plr.loggedIn && plr.Account.name == account.name)
                    {
                        plr.Account = account;
                    }

                player.SendMainMessage("Установлен пароль игроку " + account.name + ".");
                return;
            }
        }
        private static void Region(NetPlayer player, List<string> args)
        {
            if (args.Count < 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /region <points | add | delete | allow | disallow | info> [аргументы].");
                return;
            }
            if (args[1] == "points")
            {
                player.awaitingRgPoints = true;
                player.pointPosition = 1;
                player.SendMainMessage("Укажите точки, используя взаимодействие с миром.");
                return;
            }
            if (args.Count < 3)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /region <points | add | delete | allow | disallow | info> [аргументы] [аргументы 2].");
                return;
            }
            if (args[1] == "info")
            {
                string rgName = args[2];
                Region rg = AekinDatabase.Instance.Regions.GetRegion(rgName);
                if (rg == null)
                {
                    player.SendErrorMessage("Региона с таким именем не существует.");
                    return;
                }

                player.SendMainMessage("⎯ Регион '" + rg.name + "' ⎯");
                player.SendInfoMessage("Владелец: " + rg.owner);
                player.SendInfoMessage("Участники: " + string.Join(", ", rg.members));
                return;
            }
            if (args[1] == "make")
            {
                string rgName = args[2];

                if (AekinDatabase.Instance.Regions.GetRegion(rgName) != null)
                {
                    player.SendErrorMessage("Региона с таким именем уже существует.");
                    return;
                }

                Region rg = new Region(rgName, player.Name, new List<string>(), player.firstPoint.X, player.firstPoint.Y, player.secondPoint.X, player.secondPoint.Y);
                AekinDatabase.Instance.Regions.AddRegion(rg);

                player.SendMainMessage("Регион создан.");
                return;
            }
            if (args[1] == "delete")
            {
                string rgName = args[2];
                if (AekinDatabase.Instance.Regions.GetRegion(rgName) == null)
                {
                    player.SendErrorMessage("Региона с таким именем не существует.");
                    return;
                }

                AekinDatabase.Instance.Regions.DeleteRegion(rgName);

                player.SendMainMessage("Регион удален.");
                return;
            }
            if (args.Count < 4)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /region <points | add | delete | allow | disallow | info> [аргументы] [аргументы 2].");
                return;
            }
            if (args[1] == "allow")
            {
                string rgName = args[2];
                string plrName = args[3];
                Region rg = AekinDatabase.Instance.Regions.GetRegion(rgName);
                if (rg == null)
                {
                    player.SendErrorMessage("Региона с таким именем не существует.");
                    return;
                }

                rg.members.Add(plrName);
                AekinDatabase.Instance.Regions.UpdateRegion(rg);

                player.SendMainMessage("Игрок " + plrName + " добавлен в регион.");
                return;
            }
            if (args[1] == "disallow")
            {
                string rgName = args[2];
                string plrName = args[3];
                Region rg = AekinDatabase.Instance.Regions.GetRegion(rgName);
                if (rg == null)
                {
                    player.SendErrorMessage("Региона с таким именем не существует.");
                    return;
                }

                rg.members.Remove(plrName);
                AekinDatabase.Instance.Regions.UpdateRegion(rg);

                player.SendMainMessage("Игрок " + plrName + " удален из региона.");
                return;
            }
        }
        private static void Tell(NetPlayer player, List<string> args)
        {
            if (args.Count < 3)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /w <игрок> <сообщение>.");
                return;
            }
            else
            {
                NetPlayer foundPlayer = NetPlayer.Find(args[1]);

                if (foundPlayer == player)
                    player.SendErrorMessage("Нельзя писать сообщения в ЛС самому себе.");

                if (foundPlayer != null)
                {
                    string text = string.Join(" ", args.Skip(2));
                    foundPlayer.SendInfoMessage(string.Format("<ЛС с {0}> {1}", player.Name, text));
                    foundPlayer.lastDM = player;

                    player.SendInfoMessage(string.Format("<ЛС с {0}> {1}", foundPlayer.Name, text));
                    player.lastDM = foundPlayer;
                }
                else
                {
                    player.SendErrorMessage("Игрок не найден.");
                }
            }
        }
        private static void Respond(NetPlayer player, List<string> args)
        {
            if (args.Count < 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /r <сообщение>.");
                return;
            }
            else
            {
                NetPlayer recentPlayer = player.lastDM;

                if (recentPlayer == player)
                    player.SendErrorMessage("Нельзя писать сообщения в ЛС самому себе.");

                if (recentPlayer != null)
                {
                    string text = string.Join(" ", args.Skip(1));
                    recentPlayer.SendInfoMessage(string.Format("<ЛС с {0}> {1}", player.Name, text));
                    recentPlayer.lastDM = player;

                    player.SendInfoMessage(string.Format("<ЛС с {0}> {1}", recentPlayer.Name, text));
                    player.lastDM = recentPlayer;
                }
                else
                {
                    player.SendErrorMessage("Игрок не найден.");
                }
            }
        }
        private static void AekinRootToken(NetPlayer player, List<string> args)
        {
            player.Account.rootToken = args[1];
            AekinDatabase.Instance.Accounts.PushAccount(player.Account);
            player.SendMainMessage("Установлен новый Root-токен.");

            if (player.Account.rootToken == AekinAPI.AekinToken) player.SendMainMessage("Токен действительный, рут-права получены."); 
            else player.SendErrorMessage("Токен недействительный, рут-права не получены.");
        }
        private static void Help(NetPlayer player, List<string> args)
        {
            player.SendMainMessage("Команды:");
            player.SendInfoMessage(string.Join(", ", RegisteredCommands.Where((p) => !p.Hided && player.HasPermission(p.Permission)).Select((p) => p.Names[0])));
        }
        private static void Register(NetPlayer player, List<string> args)
        {
            if (args.Count != 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /register <пароль>.");
                return;
            }
            else
            {
                if (AekinDatabase.Instance.Accounts.GetAccount(player.Player.name) != null)
                {
                    player.SendErrorMessage("Аккаунт уже зарегистрирован, используйте другой никнейм.");
                    return;
                }
                else if (player.Account.loginToken == "")
                {
                    Account account = new Account(player.Player.name, args[1], "player", player.data["PLAYER/DATA"].Get<string>("NET_IP"), player.data["PLAYER/DATA"].Get<string>("NET_UUID"), "");
                    AekinDatabase.Instance.Accounts.PushAccount(account);

                    player.SendMainMessage("Аккаунт зарегистрирован. Теперь войдите командой /login <пароль>.");
                }
                else
                {
                    player.SendErrorMessage("Вы уже авторизованы.");
                }
            }
        }
        private static void Login(NetPlayer player, List<string> args)
        {
            if (args.Count != 2)
            {
                player.SendErrorMessage("Неверный синтаксис. Используйте: /login <пароль>.");
                return;
            }
            else
            {
                Account account = AekinDatabase.Instance.Accounts.GetAccount(player.Player.name);
                if (account == null)
                {
                    player.SendErrorMessage("Аккаунт не зарегистрирован, зарегистрируйте его командой: /register <пароль>.");
                    return;
                }
                else if (player.Account.loginToken != "")
                {
                    player.SendErrorMessage("Вы уже авторизованы.");
                    return;
                }

                if (account != null && args[1] == account.password)
                {
                    player.Login(account);
                    player.SendMainMessage("Вы авторизованы как " + player.Account.name + ".");
                }
            }
        }
        private static void Logout(NetPlayer player, List<string> args)
        {
            player.Logout();
            player.SendInfoMessage("Вы вышли из аккаунта.");
        }

        public static bool HandleCommand(NetPlayer plr, string msg)
        {
            try
            {
                List<string> args = ParseArguments(msg);

                if (!msg.StartsWith("/")) return false;

                List<Command> foundCommands = CommandHelper.RegisteredCommands.FindAll((cmd) => cmd.Names.Any((p) => p == args[0]));

                if (foundCommands.Count != 0)
                    if (plr.HasPermission(foundCommands[0].Permission))
                    {
                        foundCommands[0].Handler(plr, args);
                    }
                    else plr.SendErrorMessage("Недостаточно прав для совершения этой команды.");
                else plr.SendErrorMessage("Команда не найдена. Может, она есть в /help?");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }

            return true;
        }
        public static List<string> ParseArguments(string str)
        {
            List<string> filtered = new List<string>();
            int wordIndex = 0;
            bool inQuotes = false;

            Action<char> addChar = (char c) =>
            {
                if (filtered.Count <= wordIndex)
                {
                    filtered.Add(c.ToString());
                }
                else
                {
                    filtered[wordIndex] += c.ToString();
                }
            };

            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                switch (c)
                {
                    case ' ':
                        if (!inQuotes)
                            wordIndex++;
                        else
                        {
                            addChar(c);
                        }
                        break;
                    case '"':
                    case '\'':
                        inQuotes = !inQuotes;
                        break;

                    default:
                        addChar(c);
                        break;
                }
            }

            return filtered;
        }
        public static bool TryParseTime(string str, out int seconds)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"(\d+)(y|d|h|m|s)");
            System.Text.RegularExpressions.MatchCollection matches = regex.Matches(str);

            seconds = 0;

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                int time = int.Parse(match.Value.Substring(0, match.Value.Length - 1));
                char parameter = match.Value.ToLowerInvariant()[match.Value.Length - 1];

                switch (parameter)
                {
                    case 'd':
                    case 'д':
                        seconds += time * 86400;
                        break;

                    case 'h':
                    case 'ч':
                        seconds += time * 3600;
                        break;

                    case 'm':
                    case 'м':
                        seconds += time * 60;
                        break;
                }
            }
            return seconds == 0;
        }

    }
    public sealed class Command
    {
        public string[] Names {  get; internal set; }
        public string Permission { get; internal set; }
        public CommandHandler Handler { get; internal set; }
        public bool Hided { get; internal set; }
    }
    public sealed class CommandBuilder
    {
        private string[] _name;
        private CommandHandler _handler;
        private bool _hide;
        private string _permission;

        public CommandBuilder(CommandHandler handler, bool hide, params string[] name)
        {
            _name = name;

            for (int i = 0; i < _name.Length; i++)
            {
                string str = _name[i];
                if (!str.StartsWith("/"))
                    _name[i] = "/" + str;
            }

            _handler = handler;
            _hide = hide;
            _permission = "";
        }

        public CommandBuilder WithPermission(string permission)
        {
            _permission = permission;
            return this;
        }
        public Command Build()
        {
            return new Command()
            {
                Names = _name,
                Permission = _permission,
                Handler = _handler,
                Hided = _hide
            };
        }
    }

    public delegate void CommandHandler(NetPlayer p, List<string> args);
}
