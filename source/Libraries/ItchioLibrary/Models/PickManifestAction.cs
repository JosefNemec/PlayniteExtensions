using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItchioLibrary.Models
{
    public class PickActionAction
    {
        /// <summary>
        /// human-readable or standard name
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// file path(relative to manifest or absolute), URL, etc.
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// icon name(see static/fonts/icomoon/demo.html, don’t include icon- prefix)
        /// </summary>
        public string icon { get; set; }

        /// <summary>
        /// command-line arguments
        /// </summary>
        public string[] args { get; set; }

        /// <summary>
        /// sandbox opt-in
        /// </summary>
        public bool sandbox { get; set; }

        /// <summary>
        /// requested API scope
        /// </summary>
        public string scope { get; set; }

        /// <summary>
        /// don’t redirect stdout/stderr, open in new console window
        /// </summary>
        public bool console { get; set; }

        /// <summary>
        /// Iternal use only for selection dialog.
        /// </summary>
        internal int actionIndex { get; set; }
    }

    public class PickManifestAction
    {
        public List<PickActionAction> actions;
    }
}
