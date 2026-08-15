using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;

namespace VideoDownloader.Services;

/// <summary>
/// 主题管理器：色板与语义键全部定义在 Styles/Themes/Themes.axaml（标准 Avalonia 资源字典）。
/// 控件只引用无前缀的语义键；Apply 时把选中主题的语义键解析到 Application.Resources。
/// </summary>
public static class ThemeManager
{
    public static readonly string[] Themes = { "Default", "Cyberpunk", "Neoclassical" };

    // 语义键：控件引用的主题色（无前缀），每个主题在 Themes.axaml 中按 Theme.{主题}.{语义键} 定义
    private static readonly string[] SemanticKeys =
    {
        "BackgroundLight", "BackgroundDark",
        "TabLight", "TabDark",
        "ButtonLight", "ButtonDark", "ButtonDanger", "ButtonHover", "ButtonPressed",
        "TextTheme", "TextLight", "TextDark",
        "DividerColor", "BorderLight", "BorderDark"
    };

    public static bool IsValid(string name) =>
        Themes.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase));

    public static void Apply(string name)
    {
        if (!IsValid(name))
            name = Themes[0];

        var app = global::Avalonia.Application.Current;
        if (app?.Resources == null) return;

        foreach (var key in SemanticKeys)
        {
            var themeKey = $"Theme.{name}.{key}";
            if (app.Resources.TryGetResource(themeKey, null, out var value) && value is SolidColorBrush brush)
                app.Resources[key] = brush;
        }
    }
}