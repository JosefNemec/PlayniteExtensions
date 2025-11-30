namespace HumbleLibrary.Models
{
    public class Extra
    {
        /// <summary>
        /// This is sensitive info, that's why json file with extras is encrypted
        /// </summary>
        public string GameKey { get; set; }

        /// <summary>
        /// Helps identify given "extra" in a list of "downloads" returned from api
        /// </summary>
        public string Sha1 { get; set; }

        /// <summary>
        /// For asm.js humble games - they have unique url (with gameKey) that does not require authentication and never changes
        /// </summary>
        public string PermanentUrl { get; set; }

    }
}
