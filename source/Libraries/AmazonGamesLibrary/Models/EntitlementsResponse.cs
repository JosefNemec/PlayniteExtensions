using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmazonGamesLibrary.Models
{
    public class Entitlement
    {
        public class Product
        {
            public class ProductDetail
            {
                public class ProductDetails
                {
                    public string backgroundUrl1;
                    public string backgroundUrl2;
                    public string publisher;
                    public List<string> screenshots;
                    public List<string> videos;
                }

                public string iconUrl;
                public ProductDetails details;
            }

            public string asin;
            public int asinVersion;
            public string id;
            public ProductDetail productDetail;
            public string productLine;
            public string sku;
            public string title;
            public string type;
            public string vendorId;
        }

        public string channelId;
        public string id;
        public Product product;
        public string state;

        public override string ToString()
        {
            return product?.title ?? id;
        }
    }

    public class EntitlementsResponse
    {
        public List<Entitlement> entitlements;
        public string nextToken;
    }
}
