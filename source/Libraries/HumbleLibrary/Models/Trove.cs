using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HumbleLibrary.Models
{
    public class TroveGame
    {
        public class CarouselContent
        {
            public List<string> screenshot;
        }

        public class Publisher
        {
            [SerializationPropertyName("publisher-name")]
            public string publisher_name;
        }

        public class Developer
        {
            [SerializationPropertyName("developer-name")]
            public string developer_name;
        }

        public string machine_name;
        public string image;

        [SerializationPropertyName("human-name")]
        public string human_name;

        [SerializationPropertyName("description-text")]
        public string description_text;

        [SerializationPropertyName("carousel-content")]
        public CarouselContent carousel_content;

        public List<Developer> developers;

        public List<Publisher> publishers;
    }
}
