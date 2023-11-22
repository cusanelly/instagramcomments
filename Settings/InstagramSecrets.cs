namespace InstagramComments.Settings
{

    internal class InstagramSecrets
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PostId { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string[] InstagramAccounts { get; set; } = Array.Empty<string>();
    }
}
