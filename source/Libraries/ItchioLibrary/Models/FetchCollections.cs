using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItchioLibrary.Models
{
    public class FetchCollections
    {
        /// <summary>
        /// Requested collections.
        /// </summary>
        public List<Collection> items;
        /// <summary>
        /// True if the info was from local DB and it should be re-queried using “Fresh”.
        /// </summary>
        public bool stale;
    }

    public static class GameRecordsSource
    {
        public const string Owned = "owned";
        public const string Installed = "installed";
        public const string Profile = "profile";
        public const string Collection = "collection";
    }
}
