using Harmony.Services.Localization;

namespace Harmony.Tests;

public class LocalizationTableTests
{
    [Fact]
    public void Get_returns_nav_home_in_english()
    {
        var text = LocalizationService.Instance.T("nav.home");
        Assert.Equal("Home", text);
    }

    [Fact]
    public void Get_returns_russian_when_language_set()
    {
        var loc = LocalizationService.Instance;
        loc.SetLanguage("ru");
        Assert.Equal("Главная", loc.T("nav.home"));
        loc.SetLanguage("en");
    }

    [Fact]
    public void All_keys_resolve_for_every_language()
    {
        var keys = new[]
        {
            "nav.home", "nav.search", "collections.findSongs", "collections.searchAdd",
            "search.placeholder", "library.empty", "albums.subtitle", "playlists.subtitle"
        };
        var langs = new[] { "en", "ru", "uk", "es", "de", "fr", "it", "pt", "pl", "ja" };

        foreach (var lang in langs)
        foreach (var key in keys)
        {
            var loc = LocalizationService.Instance;
            loc.SetLanguage(lang);
            var text = loc.T(key);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.NotEqual(key, text);
        }
        LocalizationService.Instance.SetLanguage("en");
    }
}
