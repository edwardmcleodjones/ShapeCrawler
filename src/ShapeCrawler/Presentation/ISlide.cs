using System.Collections.Generic;
#if DEBUG
using System.Threading.Tasks;
#endif
using DocumentFormat.OpenXml.Packaging;

namespace ShapeCrawler;

/// <summary>
///     Represents a slide.
/// </summary>
public interface ISlide
{
    /// <summary>
    ///     Gets background image.
    /// </summary>
    IImage Background { get; }

    /// <summary>
    ///     Gets or sets custom data. It returns <see langword="null"/> if custom data is not presented.
    /// </summary>
    string? CustomData { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the slide is hidden.
    /// </summary>
    bool Hidden { get; }

    /// <summary>
    ///     Gets referenced Slide Layout.
    /// </summary>
    ISlideLayout SlideLayout { get; }

    /// <summary>
    /// Gets a list of all textboxes on that slide, including those in tables.
    /// </summary>
    public IList<ITextFrame> TextFrames();

    /// <summary>
    ///     Hides slide.
    /// </summary>
    void Hide();
    
    int Number { get; }

#if DEBUG
    
    Task<string> ToHtml();
    
    /// <summary>
    ///     Saves slide as PNG image.
    /// </summary>
    void SaveAsPng(System.IO.Stream stream);
#endif
}