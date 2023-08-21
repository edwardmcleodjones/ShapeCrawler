﻿using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace ShapeCrawler.Drawing;

internal static class HexParser
{
    internal static (SCColorType, string?) FromSolidFill(A.SolidFill aSolidFill, SlideMaster slideMaster)
    {
        var colorHexVariant = GetWithoutScheme(aSolidFill);
        if (colorHexVariant is not null)
        {
            return ((SCColorType, string))colorHexVariant;
        }

        var aSchemeColor = aSolidFill.GetFirstChild<A.SchemeColor>() !;
        var fromScheme = GetByThemeColorScheme(aSchemeColor.Val!, slideMaster); 
        return (SCColorType.Theme, fromScheme);
    }

    internal static (SCColorType, string)? GetWithoutScheme(TypedOpenXmlCompositeElement typedElement)
    {
        var aSrgbClr = typedElement.GetFirstChild<A.RgbColorModelHex>();
        string colorHexVariant;
        if (aSrgbClr != null)
        {
            colorHexVariant = aSrgbClr.Val!;
            {
                return (SCColorType.RGB, colorHexVariant);
            }
        }

        var aSysClr = typedElement.GetFirstChild<A.SystemColor>();
        if (aSysClr != null)
        {
            colorHexVariant = aSysClr.LastColor!;
            {
                return (SCColorType.Standard, colorHexVariant);
            }
        }

        var aPresetColor = typedElement.GetFirstChild<A.PresetColor>();
        if (aPresetColor != null)
        {
            var coloName = aPresetColor.Val!.Value.ToString();
            {
                return (SCColorType.Preset, SCColorTranslator.HexFromName(coloName));
            }
        }

        return null;
    }

    private static string? GetByThemeColorScheme(string schemeColor, SlideMaster slideMaster)
    {
        var hex = GetThemeColorByString(schemeColor, slideMaster);

        if (hex == null)
        {
            hex = GetThemeMappedColor(schemeColor, slideMaster);
        }

        return hex ?? null;
    }

    private static string? GetThemeMappedColor(string fontSchemeColor, SlideMaster slideMaster)
    {
        var slideMasterPColorMap = slideMaster.PSlideMaster.ColorMap;
        var targetSchemeColor = slideMasterPColorMap?.GetAttributes().FirstOrDefault(a => a.LocalName == fontSchemeColor);
        return GetThemeColorByString(targetSchemeColor?.Value?.ToString() !, slideMaster);
    }

    private static string? GetThemeColorByString(string schemeColor, SlideMaster slideMaster)
    {
        var themeAColorScheme = slideMaster.ThemePart.Theme.ThemeElements!.ColorScheme!;
        var color = themeAColorScheme.Elements<A.Color2Type>().FirstOrDefault(c => c.LocalName == schemeColor);
        var hex = color?.RgbColorModelHex?.Val?.Value ?? color?.SystemColor?.LastColor?.Value;
        return hex;
    }
}