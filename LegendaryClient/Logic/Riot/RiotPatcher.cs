using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace LegendaryClient.Logic.Riot
{
    public class RiotPatcher
    {
        public string DDragonVersion;

        public RiotPatcher()
        {
        }

        public string GetDragon()
        {
            string dragonJSON = "";
            using (WebClient client = new WebClient())
            {
                dragonJSON = client.DownloadString("http://ddragon.leagueoflegends.com/realms/euw.js");
            }
            dragonJSON = dragonJSON.Replace("Riot.DDragon.m=", "").Replace(";", "");
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> deserializedJSON = serializer.Deserialize<Dictionary<string, object>>(dragonJSON);
            string Version = (string)deserializedJSON["v"];
            string CDN = (string)deserializedJSON["cdn"];
            string s = CDN + "/dragontail-" + Version + ".tgz";
            DDragonVersion = Version;
            return s;
        }

        public string GetGreaterVersion(string ver1, string ver2)
        {
            if (!String.IsNullOrEmpty(ver1) && !String.IsNullOrEmpty(ver2))
            {
                string[] tmp1 = ver1.Split('.');
                string[] tmp2 = ver2.Split('.');
                if (tmp1 != null && tmp2 != null && tmp1.Length == 4 && tmp2.Length == 4)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (Convert.ToInt32(tmp1[i]) < Convert.ToInt32(tmp2[i]))
                            return (ver2);
                    }
                }
            }
            return (ver1);
        }

        public string GetLatestAir()
        {
            string airVersions = "";
            using (WebClient client = new WebClient())
            {
                airVersions = client.DownloadString("http://l3cdn.riotgames.com/releases/live/projects/lol_air_client/releases/releaselisting_EUW");
            }
            return airVersions.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)[0];
        }

        public string GetLatestGame()
        {
            string gameVersions = "";
            using (WebClient client = new WebClient())
            {
                gameVersions = client.DownloadString("http://l3cdn.riotgames.com/releases/live/projects/lol_game_client/releases/releaselisting_EUW");
            }
            return gameVersions.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)[0];
        }

        public string GetCurrentAirInstall(string Location)
        {
            System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            DirectoryInfo dInfo = new DirectoryInfo(Location);
            DirectoryInfo[] subdirs = null;
            try
            {
                subdirs = dInfo.GetDirectories();
            }
            catch { return "0.0.0.0"; }
            string latestVersion = "0.0.0.0";
            foreach (DirectoryInfo info in subdirs)
                latestVersion = GetGreaterVersion(latestVersion, info.Name);

            string AirLocation = Path.Combine(Location, latestVersion, "deploy");

            // Copy common client lib.
            if (File.Exists(Path.Combine(Client.ExecutingDirectory, "ClientLibCommon.dat")))
                File.Delete(Path.Combine(Client.ExecutingDirectory, "ClientLibCommon.dat"));
            File.Copy(Path.Combine(AirLocation, "lib", "ClientLibCommon.dat"), Path.Combine(Client.ExecutingDirectory, "ClientLibCommon.dat"));

            // Copy game stats database.
            if (File.Exists(Path.Combine(Client.ExecutingDirectory, "gameStats_en_US.sqlite")))
                File.Delete(Path.Combine(Client.ExecutingDirectory, "gameStats_en_US.sqlite"));
            File.Copy(Path.Combine(AirLocation, "assets", "data", "gameStats", "gameStats_en_US.sqlite"), Path.Combine(Client.ExecutingDirectory, "gameStats_en_US.sqlite"));

            // Copy champions images.
            Copy(Path.Combine(AirLocation, "assets", "images", "champions"), Path.Combine(Client.ExecutingDirectory, "Assets", "champions"));

            // Store last air version id.
            if (File.Exists(Path.Combine(Client.ExecutingDirectory, "Assets", "VERSION_AIR")))
                File.Delete(Path.Combine(Client.ExecutingDirectory, "Assets", "VERSION_AIR"));
            var VersionAIR = File.Create(Path.Combine(Client.ExecutingDirectory, "Assets", "VERSION_AIR"));
            VersionAIR.Write(encoding.GetBytes(latestVersion), 0, encoding.GetBytes(latestVersion).Length);
            VersionAIR.Close();

            return latestVersion;
        }

        public string GetCurrentGameInstall(string GameLocation)
        {
            System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            DirectoryInfo dInfo = new DirectoryInfo(Path.Combine(GameLocation, "projects", "lol_game_client", "filearchives"));
            DirectoryInfo[] subdirs = null;
            try
            {
                subdirs = dInfo.GetDirectories();
            }
            catch { return "0.0.0.0"; }
            string latestVersion = "0.0.0.0";
            foreach (DirectoryInfo info in subdirs)
                latestVersion = GetGreaterVersion(latestVersion, info.Name);

            string ParentDirectory = Directory.GetParent(GameLocation).FullName;
            if (Directory.Exists(Path.Combine(ParentDirectory, "Config")))
            {
                Copy(Path.Combine(ParentDirectory, "Config"), Path.Combine(Client.ExecutingDirectory, "Config"));
            }

            Copy(Path.Combine(GameLocation, "projects", "lol_game_client"), Path.Combine(Client.ExecutingDirectory, "RADS", "projects", "lol_game_client"));
            File.Copy(Path.Combine(GameLocation, "RiotRadsIO.dll"), Path.Combine(Client.ExecutingDirectory, "RADS", "RiotRadsIO.dll"), true);

            if (File.Exists(Path.Combine("RADS", "VERSION_LOL")))
                File.Delete(Path.Combine("RADS", "VERSION_LOL"));
            var VersionAIR = File.Create(Path.Combine("RADS", "VERSION_LOL"));
            VersionAIR.Write(encoding.GetBytes(latestVersion), 0, encoding.GetBytes(latestVersion).Length);
            VersionAIR.Close();
            return latestVersion;
        }

        private void Copy(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);

            foreach (var directory in Directory.GetDirectories(sourceDir))
                Copy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }
    }
}