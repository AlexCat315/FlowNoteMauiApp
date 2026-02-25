using System.Globalization;
using ExampleMauiApp.Resources;
using Microsoft.Maui.Controls;

namespace ExampleMauiApp;

public static class LanguageManager
{
    private const string LanguageKey = "SelectedLanguage";

    public static event Action? LanguageChanged;

    public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    public static void Initialize()
    {
        var savedLanguage = Preferences.Get(LanguageKey, null);
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            var culture = new CultureInfo(savedLanguage);
            SetCulture(culture);
        }
        else
        {
            SetCulture(CultureInfo.CurrentUICulture);
        }
    }

    public static void SetCulture(CultureInfo culture)
    {
        CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        
        AppResources.Culture = culture;

        Preferences.Set(LanguageKey, culture.Name);
        LanguageChanged?.Invoke();
        
        RefreshMainPage();
    }

    private static void RefreshMainPage()
    {
        if (Application.Current?.Windows.Count > 0)
        {
            var window = Application.Current.Windows[0];
            window.Page = new AppShell();
        }
    }

    public static bool IsChinese => CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
}
