using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamLibrary.Models
{
    public class GetOwnedGamesResult
    {
        public class Game
        {
            public int appid { get; set; }
            public string name { get; set; }
            public int playtime_forever { get; set; }
            public string img_icon_url { get; set; }
            public string img_logo_url { get; set; }
            public bool has_community_visible_stats { get; set; }
        }

        public class Response
        {
            public int game_count { get; set; }
            public List<Game> games { get; set; }
        }

        public Response response { get; set; }
    }

    public class RgGame
    {
        public int appid { get; set; }
        public string name { get; set; }
        public int playtime_forever { get; set; }
        public int rtime_last_played { get; set; }
        public string sort_as { get; set; }
    }

    public class ProfilePageOwnedGames
    {
        public string strProfileName { get; set; }
        public bool bViewingOwnProfile { get; set; }
        public string strSteamId { get; set; }
        public List<RgGame> rgGames { get; set; }
    }
}
