namespace Decatron.Core.Settings
{
    public class TwitchSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string BotToken { get; set; }
        public string BotUsername { get; set; }
        public string ChannelId { get; set; }
        public string RedirectUri { get; set; }
        public string Scopes { get; set; }

        // EventSub Configuration
        public string WebhookSecret { get; set; }
        public string EventSubWebhookSecret { get; set; }
        public string EventSubWebhookUrl { get; set; }
        public int EventSubWebhookPort { get; set; } = 7282;
    }
}