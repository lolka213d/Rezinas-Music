namespace Harmony.Helpers;

/// <summary>User-visible product name (window title, dialogs, tray).</summary>
public static class AppBranding
{
    public const string Name = "Rezinas Music";

    /// <summary>Discord Developer Portal application id for Rich Presence.</summary>
    public const string DiscordApplicationId = "1518760719657730068";

    /// <summary>Rich Presence asset key uploaded in the Discord app (Art Assets).</summary>
    public const string DiscordLargeImageKey = "rezinas_logo";

    public const string DiscordLargeImageText = "Rezinas Music";

    /// <summary>GitHub repo for release updates (owner/repo).</summary>
    public const string GitHubOwner = "lolka213d";
    public const string GitHubRepo = "Rezinas-Music";
}

/// <summary>Donation / support links shown in Settings (edit to your profiles).</summary>
public static class AuthorSupport
{
    public const string PayPal = "https://www.paypal.com/ncp/payment/PMVP42DTMTBSL";
    public const string BuyMeACoffee = "https://buymeacoffee.com/rezinas";
    public const string Boosty = "https://boosty.to/moonsfh";
    public const string Patreon = "https://www.patreon.com/rezinas";
    public const string DonateAlerts = "https://www.donationalerts.com/r/rezinas";
}
