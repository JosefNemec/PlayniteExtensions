using System;
using System.Collections.Generic;

namespace ItchioLibrary.Models
{
    public class FetchGameRecords
    {
        /// <summary>
        /// Requested game records.
        /// </summary>
        public List<GameRecord> records;
        /// <summary>
        /// True if the info was from local DB and it should be re-queried using “Fresh”.
        /// </summary>
        public bool stale;
    }
}