﻿using Playnite.SDK.Models;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Steam.Models
{
    public class SteamGameMetadata : GameMetadata
    {
        public KeyValue ProductDetails { get; set; }
        public StoreAppDetailsResult.AppDetails StoreDetails { get; set; }
        public AppReviewsResult.QuerySummary UserReviewDetails { get; set; }
    }
}
