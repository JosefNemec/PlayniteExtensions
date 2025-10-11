using System.Collections.Generic;


namespace EpicLibrary.Models
{
    public class LibraryItemsResponse
    {
        public ResponseMetadata responseMetadata { get; set; } = new ResponseMetadata();
        public List<Asset> records { get; set; }

        public class ResponseMetadata
        {
            public string nextCursor { get; set; }
            public string stateToken { get; set; }
        }
    }
}
