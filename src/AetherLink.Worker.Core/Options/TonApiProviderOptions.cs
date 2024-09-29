namespace AetherLink.Worker.Core.Options;

public class TonGetBlockProviderOptions
{
        public string Url { get; set; }
        public int Weight { get; set; }
        public string ApiKey { get; set; }
        public int ApiKeyPerSecondRequestLimit { get; set; }
        public int ApiKeyPerDayRequestLimit { get; set; }
        public int ApiKeyPerMonthRequestLimit { get; set; }
}

public class TonCenterProviderApiConfig
{
        public string Url { get; set; }
        public int Weight { get; set; }
        public string ApiKey { get; set; }
        public int ApiKeyPerSecondRequestLimit { get; set; }
        public int NoApiKeyPerSecondRequestLimit { get; set; }
}