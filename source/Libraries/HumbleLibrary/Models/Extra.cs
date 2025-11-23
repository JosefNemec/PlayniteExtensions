namespace HumbleLibrary.Models
{
    public class Extra
    {
        // TODO gameKeys are private. should this json file be encrypted?
        public string GameKey { get; set; }

        public string Sha1 { get; set; }

        /// <summary>
        /// For asm.js humble games - they have unique url (with gameKey) that does not require authentication and never changes
        /// </summary>
        public string PermanentUrl { get; set; }

    }
}
