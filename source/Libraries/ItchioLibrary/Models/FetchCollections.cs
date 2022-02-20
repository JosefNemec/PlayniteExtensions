using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItchioLibrary.Models
{
    public class FetchCollections
    {
        public List<Collection> items;
        public Boolean stale;
    }

    public static class GameRecordsSource
    {
        public const string Owned = "owned";
        public const string Installed = "installed";
        public const string Profile = "profile";
        public const string Collection = "collection";
    }
}
