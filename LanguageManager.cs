using System.Globalization;
using FlowNoteMauiApp.Resources;
using Microsoft.Maui.ApplicationModel;

namespace FlowNoteMauiApp;

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
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;

        AppResources.Culture = culture;

        Preferences.Set(LanguageKey, culture.Name);

        if (MainThread.IsMainThread)
        {
            LanguageChanged?.Invoke();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => LanguageChanged?.Invoke());
        }
    }

    public static bool IsChinese => CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
}
