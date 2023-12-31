﻿namespace InstagramComments.Settings
{

    internal class InstagramSecrets
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PostId { get; set; } = ""; 
        public string JaiberPost { get; set; } = "";
        public string[] LikeAccounts { get; set; } = Array.Empty<string>();
        public string PhoneNumber { get; set; } = "";
        public string[] InstagramAccounts { get; set; } = Array.Empty<string>();
    }
}
