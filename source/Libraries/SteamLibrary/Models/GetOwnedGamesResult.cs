using System.Collections.Generic;

// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SteamLibrary.Models
{
    public class GetOwnedGamesResult
    {
        public class Game
        {
            public int appid { get; set; }
            public string name { get; set; }
            public uint playtime_forever { get; set; }
            public uint rtime_last_played { get; set; }
        }

        public class Response
        {
            public int game_count { get; set; }
            public List<Game> games { get; set; }
        }

        public Response response { get; set; }
    }
}
