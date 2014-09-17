﻿using jabber.client;
using jabber.connection;
using jabber.protocol.client;
using LegendaryClient.Controls;
using LegendaryClient.Logic.Region;
using LegendaryClient.Logic.Riot;
using LegendaryClient.Logic.Riot.Platform;
using LegendaryClient.Logic.SQLite;
using LegendaryClient.Windows;
using RtmpSharp.Messaging;
using RtmpSharp.Net;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;

namespace LegendaryClient.Logic
{
    /// <summary>
    /// Any logic that needs to be reused over multiple pages
    /// </summary>
    internal static class Client
    {
        /// <summary>
        /// Latest champion for League of Legends login screen
        /// </summary>
        internal const int LatestChamp = 268;

        /// <summary>
        /// Latest version of League of Legends. Retrieved from ClientLibCommon.dat
        /// </summary>
        internal static string Version = "4.00.00";

        /// <summary>
        /// The current directory the client is running from
        /// </summary>
        internal static string ExecutingDirectory = "";

        /// <summary>
        /// Riot's database with all the client data
        /// </summary>
        internal static SQLiteConnection SQLiteDatabase;

        /// <summary>
        /// The database of all the champions
        /// </summary>
        internal static List<champions> Champions;

        /// <summary>
        /// The database of all the champion skins
        /// </summary>
        internal static List<championSkins> ChampionSkins;

        /// <summary>
        /// The database of all the items
        /// </summary>
        internal static List<items> Items;

        /// <summary>
        /// The database of all masteries
        /// </summary>
        internal static List<masteries> Masteries;

        /// <summary>
        /// The database of all runes
        /// </summary>
        internal static List<runes> Runes;

        /// <summary>
        /// The database of all the search tags
        /// </summary>
        internal static List<championSearchTags> SearchTags;

        /// <summary>
        /// The database of all the keybinding defaults & proper names
        /// </summary>
        internal static List<keybindingEvents> Keybinds;

        internal static ChampionDTO[] PlayerChampions;

        internal static List<string> Whitelist = new List<string>();

        #region Chat

        internal static JabberClient ChatClient;

        //Fix for invitations
        public delegate void OnMessageHandler(object sender, jabber.protocol.client.Message e);
        public static event OnMessageHandler OnMessage;

        internal static string _CurrentPresenceMode = "Online";

        internal static string CurrentPresenceMode
        {
            get { return _CurrentPresenceMode; }
            set
            {
                if (_CurrentPresenceMode != value)
                {
                    _CurrentPresenceMode = value;
                    if (ChatClient != null)
                        if (ChatClient.IsAuthenticated)
                            SetChatHover();
                }
            }
        }

        internal static PresenceType _CurrentPresence;

        internal static PresenceType CurrentPresence
        {
            get { return _CurrentPresence; }
            set
            {
                if (_CurrentPresence != value)
                {
                    _CurrentPresence = value;
                    if (ChatClient != null)
                        if (ChatClient.IsAuthenticated)
                            SetChatHover();
                }
            }
        }

        internal static string _CurrentStatus;

        internal static string CurrentStatus
        {
            get { return _CurrentStatus; }
            set
            {
                if (_CurrentStatus != value)
                {
                    _CurrentStatus = value;
                    if (ChatClient != null)
                    {
                        if (ChatClient.IsAuthenticated)
                        {
                            SetChatHover();
                        }
                    }
                }
            }
        }

        internal static RosterManager RostManager;
        internal static PresenceManager PresManager;
        internal static ConferenceManager ConfManager;
        internal static bool UpdatePlayers = true;

        internal static Dictionary<string, ChatPlayerItem> AllPlayers = new Dictionary<string, ChatPlayerItem>();
        internal static List<Group> Groups = new List<Group>();

        internal static bool ChatClient_OnInvalidCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        internal static void ChatClient_OnMessage(object sender, jabber.protocol.client.Message msg)
        {
            MainWin.Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                if (OnMessage != null)
                {
                    OnMessage(sender, msg);
                }

                if (msg.Subject != null)
                {
                    ChatSubjects subject = (ChatSubjects)Enum.Parse(typeof(ChatSubjects), msg.Subject, true);

                    if (subject == ChatSubjects.PRACTICE_GAME_INVITE ||
                        subject == ChatSubjects.GAME_INVITE)
                    {
                        MainWin.FlashWindow();
                        NotificationPopup pop = new NotificationPopup(subject, msg);
                        pop.Height = 230;
                        pop.HorizontalAlignment = HorizontalAlignment.Right;
                        pop.VerticalAlignment = VerticalAlignment.Bottom;
                        NotificationGrid.Children.Add(pop);
                    }
                    else if (subject == ChatSubjects.GAME_MSG_OUT_OF_SYNC)
                    {
                        MessageOverlay messageOver = new MessageOverlay();
                        messageOver.MessageTitle.Content = "Game no longer exists";
                        messageOver.MessageTextBox.Text = "The game you are looking for no longer exists.";
                        Client.OverlayContainer.Content = messageOver.Content;
                        Client.OverlayContainer.Visibility = Visibility.Visible;
                    }
                }
            }));

            //On core thread
            if (msg.Subject != null)
                return;

            if (AllPlayers.ContainsKey(msg.From.User) && !String.IsNullOrWhiteSpace(msg.Body))
            {
                ChatPlayerItem chatItem = AllPlayers[msg.From.User];
                chatItem.Messages.Add(chatItem.Username + "|" + msg.Body);
                MainWin.FlashWindow();
            }
        }

        internal static void ChatClientConnect(object sender)
        {
            Groups.Add(new Group("Online"));

            //Get all groups
            RosterManager manager = sender as RosterManager;
            string ParseString = manager.ToString();
            List<string> StringHackOne = new List<string>(ParseString.Split(new string[] { "@pvp.net=" }, StringSplitOptions.None));
            StringHackOne.RemoveAt(0);
            foreach (string StringHack in StringHackOne)
            {
                string[] StringHackTwo = StringHack.Split(',');
                string Parse = StringHackTwo[0];
                using (XmlReader reader = XmlReader.Create(new StringReader(Parse)))
                {
                    while (reader.Read())
                    {
                        if (reader.IsStartElement())
                        {
                            switch (reader.Name)
                            {
                                case "group":
                                    reader.Read();
                                    string Group = reader.Value;
                                    if (Group != "**Default" && Groups.Find(e => e.GroupName == Group) == null)
                                        Groups.Add(new Group(Group));
                                    break;
                            }
                        }
                    }
                }
            }

            Groups.Add(new Group("Offline", false));
            SetChatHover();
        }

        internal static void SendMessage(string User, string Message)
        {
            ChatClient.Message(User, Message);
        }

        internal static void SetChatHover()
        {
            SetChatHover(_CurrentPresenceMode);
        }

        internal static void SetChatHover(string presenceMode)
        {
            if (ChatClient.IsAuthenticated)
            {
                switch (presenceMode)
                {
                    case "Online":
                        ChatClient.Presence(CurrentPresence, GetPresence(presenceMode), "chat", 0);
                        break;
                    case "Away":
                        ChatClient.Presence(CurrentPresence, GetPresence(presenceMode), "away", 0);
                        break;
                    case "Busy":
                        ChatClient.Presence(CurrentPresence, GetPresence(presenceMode), "dnd", 0);
                        break;
                    case "ChatMobile":
                        ChatClient.Presence(CurrentPresence, GetPresence(presenceMode), "chatMobile", 0);
                        break;
                }
                
            }
        }

        internal static string GetPresence(string presenceMode)
        {
            string status = "<body>" +
                "<profileIcon>" + LoginPacket.AllSummonerData.Summoner.ProfileIconId + "</profileIcon>" +
                "<level>" + LoginPacket.AllSummonerData.SummonerLevel.Level + "</level>" +
                "<wins>" + AmountOfWins + "</wins>" +
                (IsRanked ?
                "<queueType /><rankedLosses>0</rankedLosses><rankedRating>3000</rankedRating><tier>PLATINUM</tier>" + //Unused?
                "<rankedLeagueName>" + LeagueName + "</rankedLeagueName>" +
                "<rankedLeagueDivision>" + Tier + "</rankedLeagueDivision>" +
                "<rankedLeagueTier>" + TierName + "</rankedLeagueTier>" +
                "<rankedLeagueQueue>RANKED_SOLO_5x5</rankedLeagueQueue>" +
                "<rankedWins>" + AmountOfWins + "</rankedWins>" : "") +
                "<gameStatus>" + GameStatus + "</gameStatus>";
            switch (presenceMode)
            {
                case "Online":
                    status += "<statusMsg>" + CurrentStatus + "</statusMsg>";
                    break;
                case "Away":
                    status += "<statusMsg>Away</statusMsg>";
                    break;
                case "Busy":
                    status += "<statusMsg>Busy</statusMsg>";
                    break;
                case "ChatMobile":
                    status += "<statusMsg>Chat Mobile</statusMsg>";
                    break;
            }
            status += "</body>";
            return (status);
        }

        internal static void RostManager_OnRosterItem(object sender, jabber.protocol.iq.Item ri)
        {
            UpdatePlayers = true;

            if (!AllPlayers.ContainsKey(ri.JID.User))
            {
                ChatPlayerItem player = new ChatPlayerItem();
                player.Id = ri.JID.User;
                player.Group = "Online";
                using (XmlReader reader = XmlReader.Create(new StringReader(ri.OuterXml)))
                {
                    while (reader.Read())
                    {
                        if (reader.IsStartElement())
                        {
                            switch (reader.Name)
                            {
                                case "group":
                                    reader.Read();
                                    string TempGroup = reader.Value;
                                    if (TempGroup != "**Default")
                                        player.Group = TempGroup;
                                    break;
                            }
                        }
                    }
                }
                player.Username = ri.Nickname;
                bool PlayerPresence = PresManager.IsAvailable(ri.JID);
                AllPlayers.Add(ri.JID.User, player);
            }
        }

        internal static void PresManager_OnPrimarySessionChange(object sender, jabber.JID bare)
        {
            if (AllPlayers.ContainsKey(bare.User))
            {
                ChatPlayerItem Player = AllPlayers[bare.User];
                Player.IsOnline = false;
                UpdatePlayers = true;
                jabber.protocol.client.Presence[] s = PresManager.GetAll(bare);
                if (s.Length == 0)
                    return;
                string Presence = s[0].Status;
                if (Presence == null)
                    return;
                Player = ParsePresence(Presence);
                Player.IsOnline = true;

                if (String.IsNullOrWhiteSpace(Player.Status))
                    Player.Status = "";
            }
        }

        internal static ChatPlayerItem ParsePresence(string Presence)
        {
            ChatPlayerItem Player = new ChatPlayerItem();
            Player.RawPresence = Presence; //For debugging
            using (XmlReader reader = XmlReader.Create(new StringReader(Presence)))
            {
                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {
                        #region Parse Presence

                        switch (reader.Name)
                        {
                            case "profileIcon":
                                reader.Read();
                                Player.ProfileIcon = Convert.ToInt32(reader.Value);
                                break;

                            case "level":
                                reader.Read();
                                Player.Level = Convert.ToInt32(reader.Value);
                                break;

                            case "wins":
                                reader.Read();
                                Player.Wins = Convert.ToInt32(reader.Value);
                                break;

                            case "leaves":
                                reader.Read();
                                Player.Leaves = Convert.ToInt32(reader.Value);
                                break;

                            case "rankedWins":
                                reader.Read();
                                Player.RankedWins = Convert.ToInt32(reader.Value);
                                break;

                            case "timeStamp":
                                reader.Read();
                                Player.Timestamp = Convert.ToInt64(reader.Value);
                                break;

                            case "statusMsg":
                                reader.Read();
                                Player.Status = reader.Value;
                                if (Player.Status.EndsWith("∟"))
                                {
                                    Player.UsingLegendary = true;
                                }
                                break;

                            case "gameStatus":
                                reader.Read();
                                Player.GameStatus = reader.Value;
                                break;

                            case "skinname":
                                reader.Read();
                                Player.Champion = reader.Value;
                                break;

                            case "rankedLeagueName":
                                reader.Read();
                                Player.LeagueName = reader.Value;
                                break;

                            case "rankedLeagueTier":
                                reader.Read();
                                Player.LeagueTier = reader.Value;
                                break;

                            case "rankedLeagueDivision":
                                reader.Read();
                                Player.LeagueDivision = reader.Value;
                                break;
                        }

                        #endregion Parse Presence
                    }
                }
            }
            return Player;
        }

        internal static void Message(string To, string Message, ChatSubjects Subject)
        {
            Message msg = new Message(ChatClient.Document);
            msg.Type = MessageType.normal;
            msg.To = To + "@pvp.net";
            msg.Subject = ((ChatSubjects)Subject).ToString();
            msg.Body = Message;
            ChatClient.Write(msg);
        }

        //Why do you even have to do this, riot?
        internal static string GetObfuscatedChatroomName(string Subject, string Type)
        {
            int bitHack = 0;
            byte[] data = System.Text.Encoding.UTF8.GetBytes(Subject);
            byte[] result;
            SHA1 sha = new SHA1CryptoServiceProvider();
            result = sha.ComputeHash(data);
            string obfuscatedName = "";
            int incrementValue = 0;
            while (incrementValue < result.Length)
            {
                bitHack = result[incrementValue];
                obfuscatedName = obfuscatedName + Convert.ToString(((uint)(bitHack & 240) >> 4), 16);
                obfuscatedName = obfuscatedName + Convert.ToString(bitHack & 15, 16);
                incrementValue = incrementValue + 1;
            }
            obfuscatedName = Regex.Replace(obfuscatedName, @"/\s+/gx", "");
            obfuscatedName = Regex.Replace(obfuscatedName, @"/[^a-zA-Z0-9_~]/gx", "");
            return Type + "~" + obfuscatedName;
        }

        internal static string GetChatroomJID(string ObfuscatedChatroomName, string password, bool IsTypePublic)
        {
            if (!IsTypePublic)
                return ObfuscatedChatroomName + "@sec.pvp.net";

            if (String.IsNullOrEmpty(password))
                return ObfuscatedChatroomName + "@lvl.pvp.net";

            return ObfuscatedChatroomName + "@conference.pvp.net";
        }

        internal static int AmountOfWins; //Calculate wins for presence
        internal static bool IsRanked;
        internal static string TierName;
        internal static string Tier;
        internal static string LeagueName;
        internal static string GameStatus = "outOfGame";

        #endregion Chat

        //These are controls that need to be modified over one page
        internal static Grid MainGrid;
        internal static Grid NotificationGrid;
        internal static Grid StatusGrid;
        internal static Label StatusLabel;
        internal static Label InfoLabel;
        internal static Button PlayButton;
        internal static ContentControl OverlayContainer;
        internal static ContentControl ChatContainer;
        internal static ContentControl StatusContainer;
        internal static ContentControl NotificationContainer;
        internal static ContentControl NotificationOverlayContainer;
        internal static ListView ChatListView;
        internal static ChatItem ChatItem;
        internal static ListView InviteListView;

        internal static Image MainPageProfileImage;

        #region WPF Tab Change

        /// <summary>
        /// The container that contains the page to display
        /// </summary>
        internal static ContentControl Container;

        /// <summary>
        /// Page cache to stop having to recreate all information if pages are overwritted
        /// </summary>
        internal static List<Page> Pages;

        internal static bool IsOnPlayPage = false;

        /// <summary>
        /// Switches the contents of the frame to the requested page. Also sets background on
        /// the button on the top to show what section you are currently on.
        /// </summary>
        internal static void SwitchPage(Page page)
        {
            IsOnPlayPage = page is PlayPage;
            //Dont cache important pages
            if (!(page is LoginPage ||
                  page is CustomGameLobbyPage ||
                  page is ChampSelectPage ||
                  page is CreateCustomGamePage))
            {
                foreach (Page p in Pages) //Cache pages
                {
                    if (p.GetType() == page.GetType())
                    {
                        Container.Content = p.Content;
                        return;
                    }
                }
            }
            Container.Content = page.Content;
            if (!(page is FakePage))
                Pages.Add(page);
        }
        #endregion WPF Tab Change

        #region League Of Legends Logic

        internal static RtmpClient RtmpConnection;

        /// <summary>
        /// Packet recieved when initially logged on. Cached so the packet doesn't
        /// need to requested multiple times, causing slowdowns
        /// </summary>
        internal static LoginDataPacket LoginPacket;

        /// <summary>
        /// All enabled game configurations for the user
        /// </summary>
        internal static List<GameTypeConfigDTO> GameConfigs;

        /// <summary>
        /// The region the user is connecting to
        /// </summary>
        internal static BaseRegion Region;

        /// <summary>
        /// Is the client logged in to the League of Legends server
        /// </summary>
        internal static bool IsLoggedIn = false;

        /// <summary>
        /// Is the player in game at the moment
        /// </summary>
        internal static bool InGame = false;

        /// <summary>
        /// GameID of the current game that the client is connected to
        /// </summary>
        internal static double GameID = 0;

        /// <summary>
        /// Game Name of the current game that the client is connected to
        /// </summary>
        internal static string GameName = "";

        /// <summary>
        /// The DTO of the game lobby when connected to a custom game
        /// </summary>
        internal static GameDTO GameLobbyDTO;

        /// <summary>
        /// When going into champion select reuse the last DTO to set up data
        /// </summary>
        internal static GameDTO ChampSelectDTO;

        /// <summary>
        /// When connected to a game retrieve details to connect to
        /// </summary>
        internal static PlayerCredentialsDto CurrentGame;

        internal static Session PlayerSession;

        internal static bool AutoAcceptQueue = false;
        internal static object LastPageContent;
        internal static bool IsInGame = false;

        /// <summary>
        /// Fix for champ select. Do not use this!
        /// </summary>
        internal static event EventHandler<MessageReceivedEventArgs> OnFixChampSelect;
        /// <summary>
        /// Allow lobby to still have a connection. Do not use this!
        /// </summary>
        internal static event EventHandler<MessageReceivedEventArgs> OnFixLobby;

        /// <summary>
        /// When an error occurs while connected. Currently un-used
        /// </summary>
        public static void CallbackException(object sender, Exception e)
        {
            ;
        }

        internal static System.Timers.Timer HeartbeatTimer;
        internal static int HeartbeatCount;

        internal static void StartHeartbeat()
        {
            HeartbeatTimer = new System.Timers.Timer();
            HeartbeatTimer.Elapsed += new ElapsedEventHandler(DoHeartbeat);
            HeartbeatTimer.Interval = 120000; // in milliseconds
            HeartbeatTimer.Start();
        }

        internal async static void DoHeartbeat(object sender, ElapsedEventArgs e)
        {
            if (IsLoggedIn)
            {
                string result = await RiotCalls.PerformLCDSHeartBeat(Convert.ToInt32(LoginPacket.AllSummonerData.Summoner.AcctId), PlayerSession.Token, HeartbeatCount,
                            DateTime.Now.ToString("ddd MMM d yyyy HH:mm:ss 'GMT-0700'"));

                HeartbeatCount++;
            }
        }

        internal static void OnMessageReceived(object sender, MessageReceivedEventArgs message)
        {
            MainWin.Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(async () =>
            {
                if (message.Body is StoreAccountBalanceNotification)
                {
                    StoreAccountBalanceNotification newBalance = (StoreAccountBalanceNotification)message.Body;
                    InfoLabel.Content = "IP: " + newBalance.Ip + " ∙ RP: " + newBalance.Rp;
                    LoginPacket.IpBalance = newBalance.Ip;
                    LoginPacket.RpBalance = newBalance.Rp;
                }
                else if (message.Body is GameNotification)
                {
                    GameNotification notification = (GameNotification)message.Body;
                    MessageOverlay messageOver = new MessageOverlay();
                    messageOver.MessageTitle.Content = notification.Type;
                    switch (notification.Type)
                    {
                        case "PLAYER_BANNED_FROM_GAME":
                            messageOver.MessageTitle.Content = "Banned from custom game";
                            messageOver.MessageTextBox.Text = "You have been banned from this custom game!";
                            break;
                        case "PLAYER_QUIT":
                            string[] Name = await RiotCalls.GetSummonerNames(new double[1] { Convert.ToDouble((string)notification.MessageArgument) });
                            messageOver.MessageTitle.Content = "Player has left the queue";
                            messageOver.MessageTextBox.Text = Name[0] + " has left the queue";
                            break;
                        default:
                            messageOver.MessageTextBox.Text = notification.MessageCode + Environment.NewLine;
                            messageOver.MessageTextBox.Text += Convert.ToString(notification.MessageArgument);
                            break;
                    }
                    OverlayContainer.Content = messageOver.Content;
                    OverlayContainer.Visibility = Visibility.Visible;
                    QuitCurrentGame();
                }
                else if (message.Body is EndOfGameStats)
                {
                    EndOfGameStats stats = message.Body as EndOfGameStats;
                    EndOfGamePage EndOfGame = new EndOfGamePage(stats);
                    OverlayContainer.Visibility = Visibility.Visible;
                    OverlayContainer.Content = EndOfGame.Content;
                }
                else if (message.Body is StoreFulfillmentNotification)
                {
                    PlayerChampions = await RiotCalls.GetAvailableChampions();
                }
                else if (message.Body is GameDTO)
                {
                    GameDTO Queue = message.Body as GameDTO;
                    if (!IsInGame && Queue.GameState != "TERMINATED" && Queue.GameState != "TERMINATED_IN_ERROR")
                    {
                        MainWin.Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                        {
                            Client.OverlayContainer.Content = new QueuePopOverlay(Queue).Content;
                            Client.OverlayContainer.Visibility = Visibility.Visible;
                        }));
                    }
                }
                else if (message.Body is SearchingForMatchNotification)
                {
                    SearchingForMatchNotification Notification = message.Body as SearchingForMatchNotification;
                    if (Notification.PlayerJoinFailures != null && Notification.PlayerJoinFailures.Count > 0)
                    {
                        MessageOverlay messageOver = new MessageOverlay();
                        messageOver.MessageTitle.Content = "Could not join the queue";
                        foreach (QueueDodger x in Notification.PlayerJoinFailures)
                        {
                            messageOver.MessageTextBox.Text += x.Summoner.Name + " is unable to join the queue as they recently dodged a game." + Environment.NewLine;
                            TimeSpan time = TimeSpan.FromMilliseconds(x.PenaltyRemainingTime);
                            messageOver.MessageTextBox.Text += "You have " + string.Format("{0:D2}m:{1:D2}s", time.Minutes, time.Seconds) + " remaining until you may queue again";
                        }
                        OverlayContainer.Content = messageOver.Content;
                        OverlayContainer.Visibility = Visibility.Visible;
                    }
                }
            }));
        }

        internal static string InternalQueueToPretty(string InternalQueue)
        {
            switch (InternalQueue)
            {
                case "matching-queue-NORMAL-5x5-game-queue":
                    return "Normal 5v5";

                case "matching-queue-NORMAL-3x3-game-queue":
                    return "Normal 3v3";

                case "matching-queue-NORMAL-5x5-draft-game-queue":
                    return "Draft 5v5";

                case "matching-queue-RANKED_SOLO-5x5-game-queue":
                    return "Ranked 5v5";

                case "matching-queue-RANKED_TEAM-3x3-game-queue":
                    return "Ranked Team 5v5";

                case "matching-queue-RANKED_TEAM-5x5-game-queue":
                    return "Ranked Team 3v3";

                case "matching-queue-ODIN-5x5-game-queue":
                    return "Dominion 5v5";

                case "matching-queue-ARAM-5x5-game-queue":
                    return "ARAM 5v5";

                case "matching-queue-BOT-5x5-game-queue":
                    return "Bot 5v5 Beginner";

                case "matching-queue-ODIN-5x5-draft-game-queue":
                    return "Dominion Draft 5v5";

                case "matching-queue-BOT_TT-3x3-game-queue":
                    return "Bot 3v3 Beginner";

                case "matching-queue-ODINBOT-5x5-game-queue":
                    return "Dominion Bot 5v5 Beginner";

                case "matching-queue-ONEFORALL-5x5-game-queue":
                    return "One For All 5v5";

                default:
                    return InternalQueue;
            }
        }

        internal static string GetGameDirectory()
        {
            string Directory = Path.Combine(ExecutingDirectory, "RADS", "projects", "lol_game_client", "releases");

            DirectoryInfo dInfo = new DirectoryInfo(Directory);
            DirectoryInfo[] subdirs = null;
            try
            {
                subdirs = dInfo.GetDirectories();
            }
            catch { return "0.0.0"; }
            
            int latestVersion = 1;
            // string latestVersion = "0.0.1";
            foreach (DirectoryInfo info in subdirs)
            {
                // latestVersion = info.Name;
                string[] tmp = info.Name.Split('.');
                if (tmp != null && tmp.Length >= 4)
                {
                    if (latestVersion < Convert.ToInt32(tmp[3]))
                        latestVersion = Convert.ToInt32(tmp[3]);
                }
            }
            string latestVersionStr = "0.0.0." + Convert.ToString(latestVersion);
            Directory = Path.Combine(Directory, latestVersionStr, "deploy");
            return Directory;
        }

        internal static void LaunchGame()
        {
            string GameDirectory = GetGameDirectory();

            var p = new System.Diagnostics.Process();
            p.StartInfo.WorkingDirectory = GameDirectory;
            p.StartInfo.FileName = Path.Combine(GameDirectory, "League of Legends.exe");
            p.StartInfo.Arguments = "\"8394\" \"LoLLauncher.exe\" \"" + "" + "\" \"" +
                CurrentGame.ServerIp + " " +
                CurrentGame.ServerPort + " " +
                CurrentGame.EncryptionKey + " " +
                CurrentGame.SummonerId + "\"";
            p.Start();
        }

        internal static void LaunchSpectatorGame(string SpectatorServer, string Key, int GameId, string Platform)
        {
            string GameDirectory = GetGameDirectory();

            var p = new System.Diagnostics.Process();
            p.StartInfo.WorkingDirectory = GameDirectory;
            p.StartInfo.FileName = Path.Combine(GameDirectory, "League of Legends.exe");
            p.StartInfo.Arguments = "\"8393\" \"LoLLauncher.exe\" \"\" \"spectator "
                + SpectatorServer + " "
                + Key + " "
                + GameId + " "
                + Platform + "\"";
            p.Start();
        }

        internal async static void QuitCurrentGame()
        {
            if (OnMessage != null)
            {
                foreach (Delegate d in OnMessage.GetInvocationList())
                {
                    OnMessage -= (OnMessageHandler)d;
                }
            }

            FixChampSelect();
            FixLobby();
            IsInGame = false;

            await RiotCalls.QuitGame();
            StatusGrid.Visibility = System.Windows.Visibility.Hidden;
            PlayButton.Visibility = System.Windows.Visibility.Visible;
            LastPageContent = null;
            GameStatus = "outOfGame";
            SetChatHover();
            SwitchPage(new MainPage());
        }

        internal static void FixLobby()
        {
            if (OnFixLobby != null)
            {
                foreach (Delegate d in OnFixLobby.GetInvocationList())
                {
                    RtmpConnection.MessageReceived -= (EventHandler<MessageReceivedEventArgs>)d;
                    OnFixLobby -= (EventHandler<MessageReceivedEventArgs>)d;
                }
            }
        }

        internal static void FixChampSelect()
        {
            if (OnFixChampSelect != null)
            {
                foreach (Delegate d in OnFixChampSelect.GetInvocationList())
                {
                    RtmpConnection.MessageReceived -= (EventHandler<MessageReceivedEventArgs>)d;
                    OnFixChampSelect -= (EventHandler<MessageReceivedEventArgs>)d;
                }
            }
        }
        #endregion League Of Legends Logic

        internal static MainWindow MainWin;

        #region Public Helper Methods
        internal static void FocusClient()
        {
            if (MainWin.WindowState == WindowState.Minimized)
            {
                MainWin.WindowState = WindowState.Normal;
            }

            MainWin.Activate();
            MainWin.Topmost = true;  // important
            MainWin.Topmost = false; // important
            MainWin.Focus();         // important
        }

        public static String TitleCaseString(String s)
        {
            if (s == null) return s;

            String[] words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;

                Char firstChar = Char.ToUpper(words[i][0]);
                String rest = "";
                if (words[i].Length > 1)
                {
                    rest = words[i].Substring(1).ToLower();
                }
                words[i] = firstChar + rest;
            }
            return String.Join(" ", words);
        }

        public static BitmapSource ToWpfBitmap(System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);

                stream.Position = 0;
                BitmapImage result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();
                return result;
            }
        }

        public static DateTime JavaTimeStampToDateTime(double javaTimeStamp)
        {
            // Java timestamp is millisecods past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddSeconds(Math.Round(javaTimeStamp / 1000)).ToLocalTime();
            return dtDateTime;
        }

        public static void Log(String lines, String type = "LOG")
        {
            /*System.IO.StreamWriter file = new System.IO.StreamWriter(Path.Combine(ExecutingDirectory, "lcdebug.log"), true);
            file.WriteLine(string.Format("({0} {1}) [{2}]: {3}", DateTime.Now.ToShortDateString(), DateTime.Now.ToShortTimeString(), type, lines));
            file.Close();*/
        }

        public static BitmapImage GetImage(string Address)
        {
            Uri UriSource = new Uri(Address, UriKind.RelativeOrAbsolute);
            if (!File.Exists(Address) && !Address.StartsWith("/LegendaryClient;component"))
            {
                Log("Cannot find " + Address, "WARN");
                UriSource = new Uri("/LegendaryClient;component/NONE.png", UriKind.RelativeOrAbsolute);
            }
            return new BitmapImage(UriSource);
        }
        #endregion Public Helper Methods
    }

    public class ChatPlayerItem
    {
        public string Id { get; set; }

        public string Username { get; set; }

        public int ProfileIcon { get; set; }

        public int Level { get; set; }

        public int Wins { get; set; }

        public int RankedWins { get; set; }

        public int Leaves { get; set; }

        public string LeagueTier { get; set; }

        public string LeagueDivision { get; set; }

        public string LeagueName { get; set; }

        public string GameStatus { get; set; }

        public long Timestamp { get; set; }

        public bool Busy { get; set; }

        public string Champion { get; set; }

        public string Status { get; set; }

        public string RawPresence { get; set; }

        public string Group { get; set; }

        public bool UsingLegendary { get; set; }

        public bool IsOnline { get; set; }

        public List<string> Messages = new List<string>();
    }

    public class Group
    {
        public Group(string s, bool Open = true)
        {
            GroupName = s;
            IsOpen = Open;
        }

        public string GroupName { get; set; }

        public bool IsOpen { get; set; }
    }
}