﻿using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Html.Dom;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ShapeCrawler.Drawing;
using ShapeCrawler.Extensions;
using ShapeCrawler.Placeholders;
using ShapeCrawler.Services;
using ShapeCrawler.Shapes;
using ShapeCrawler.Shared;
using ShapeCrawler.Texts;
using SkiaSharp;
using P = DocumentFormat.OpenXml.Presentation;

namespace ShapeCrawler.AutoShapes;

internal sealed record SlideAutoShape : ISlideAutoShape
{
    // SkiaSharp uses 72 Dpi (https://stackoverflow.com/a/69916569/2948684), ShapeCrawler uses 96 Dpi.
    // 96/72=1.4
    private const double Scale = 1.4;

    private readonly Lazy<AutoShapeFill> shapeFill;
    private readonly Lazy<SCTextFrame?> textFrame;
    private readonly ResetableLazy<Dictionary<int, FontData>> lvlToFontData;
    private readonly P.Shape pShape;
    private readonly SlideShapes parentShapeCollection;
    private readonly Shape shape;

    internal SlideAutoShape(P.Shape pShape, SlideShapes parentShapeCollection, Shape shape)
    {
        this.pShape = pShape;
        this.parentShapeCollection = parentShapeCollection;
        this.textFrame = new Lazy<SCTextFrame?>(this.ParseTextFrame);
        this.shapeFill = new Lazy<AutoShapeFill>(this.GetFill);
        this.lvlToFontData = new ResetableLazy<Dictionary<int, FontData>>(this.GetLvlToFontData);
        this.shape = shape;
        this.Outline = new ShapeOutline(this, pShape.ShapeProperties!);
    }
    
    internal event EventHandler<NewAutoShape>? Duplicated;

    public IShapeOutline Outline { get; }

    public int Width
    {
        get => this.shape.Width(); 
        set => this.shape.UpdateWidth(value);
    }

    public int Height
    {
        get => this.shape.Height(); 
        set => this.shape.UpdateHeight(value);
    }
    public int Id => this.shape.Id();
    public string Name => this.shape.Name();
    public bool Hidden { get; }
    public bool IsPlaceholder()
    {
        throw new NotImplementedException();
    }

    public IPlaceholder Placeholder => this.PlaceholderOr();

    private IPlaceholder PlaceholderOr()
    {
        var pPlaceholder = this.pShape.GetPNvPr().GetFirstChild<P.PlaceholderShape>();
        if (pPlaceholder == null)
        {
            return new NullPlaceholder();
        }

        return new SlidePlaceholder(pPlaceholder);
    }

    public SCGeometry GeometryType { get; }
    public string? CustomData { get; set; }
    public SCShapeType ShapeType => SCShapeType.AutoShape;
    public IAutoShape AsAutoShape()
    {
        return this;
    }

    public IShapeFill Fill => this.shapeFill.Value;

    public ITextFrame? TextFrame => this.textFrame.Value;

    public bool IsTextHolder()
    {
        throw new NotImplementedException();
    }

    public double Rotation { get; }

    public void Duplicate()
    {
        var pShapeCopy = this.pShape.CloneNode(true);
        this.parentShapeCollection.Add(pShapeCopy);
    }
    
    internal void Draw(SKCanvas slideCanvas)
    {
        var skColorOutline = SKColor.Parse(this.Outline.Color);

        using var paint = new SKPaint
        {
            Color = skColorOutline,
            IsAntialias = true,
            StrokeWidth = UnitConverter.PointToPixel(this.Outline.Weight),
            Style = SKPaintStyle.Stroke
        };

        if (this.GeometryType == SCGeometry.Rectangle)
        {
            float left = this.X;
            float top = this.Y;
            float right = this.X + this.Width;
            float bottom = this.Y + this.Height;
            var rect = new SKRect(left, top, right, bottom);
            slideCanvas.DrawRect(rect, paint);
            var textFrame = (SCTextFrame)this.TextFrame!;
            textFrame.Draw(slideCanvas, left, this.Y);
        }
    }

    internal string ToJson()
    {
        throw new NotImplementedException();
    }

    internal IHtmlElement ToHtmlElement()
    {
        throw new NotImplementedException();
    }

    internal void ResizeShape()
    {
        if (this.TextFrame!.AutofitType != SCAutofitType.Resize)
        {
            return;
        }

        var baseParagraph = this.TextFrame.Paragraphs.First();
        var popularPortion = baseParagraph.Portions.OfType<SCParagraphTextPortion>().GroupBy(p => p.Font.Size).OrderByDescending(x => x.Count())
            .First().First();
        var font = popularPortion.Font;

        var paint = new SKPaint();
        var fontSize = font!.Size;
        paint.TextSize = fontSize;
        paint.Typeface = SKTypeface.FromFamilyName(font.LatinName);
        paint.IsAntialias = true;

        var lMarginPixel = UnitConverter.CentimeterToPixel(this.TextFrame.LeftMargin);
        var rMarginPixel = UnitConverter.CentimeterToPixel(this.TextFrame.RightMargin);
        var tMarginPixel = UnitConverter.CentimeterToPixel(this.TextFrame.TopMargin);
        var bMarginPixel = UnitConverter.CentimeterToPixel(this.TextFrame.BottomMargin);

        var textRect = default(SKRect);
        var text = this.TextFrame.Text;
        paint.MeasureText(text, ref textRect);
        var textWidth = textRect.Width;
        var textHeight = paint.TextSize;
        var currentBlockWidth = this.Width - lMarginPixel - rMarginPixel;
        var currentBlockHeight = this.Height - tMarginPixel - bMarginPixel;

        this.UpdateHeight(textWidth, currentBlockWidth, textHeight, tMarginPixel, bMarginPixel, currentBlockHeight);
        this.UpdateWidthIfNeed(paint, lMarginPixel, rMarginPixel);
    }

    internal void FillFontData(int paraLevel, ref FontData fontData)
    {
        if (this.lvlToFontData.Value.TryGetValue(paraLevel, out var layoutFontData))
        {
            fontData = layoutFontData;
            if (!fontData.IsFilled() && this.IsPlaceholder())
            {
                this.layoutAutoShape.FillFontData(paraLevel, ref fontData);
            }

            return;
        }

        if (this.IsPlaceholder())
        {
            var placeholder = (SCPlaceholder)this.Placeholder;
            var referencedMasterShape = (SlideAutoShape?)placeholder.ReferencedShape.Value;
            if (referencedMasterShape != null)
            {
                referencedMasterShape.FillFontData(paraLevel, ref fontData);
            }
        }
    }

    private Dictionary<int, FontData> GetLvlToFontData()
    {
        var textBody = this.pShape.GetFirstChild<P.TextBody>();
        var lvlToFontData = FontDataParser.FromCompositeElement(textBody!.ListStyle!);

        if (!lvlToFontData.Any())
        {
            var endParaRunPrFs = textBody.GetFirstChild<DocumentFormat.OpenXml.Drawing.Paragraph>() !
                .GetFirstChild<DocumentFormat.OpenXml.Drawing.EndParagraphRunProperties>()?.FontSize;
            if (endParaRunPrFs is not null)
            {
                var fontData = new FontData
                {
                    FontSize = endParaRunPrFs
                };
                lvlToFontData.Add(1, fontData);
            }
        }

        return lvlToFontData;
    }

    private void UpdateHeight(
        float textWidth,
        int currentBlockWidth,
        float textHeight,
        int tMarginPixel,
        int bMarginPixel,
        int currentBlockHeight)
    {
        var requiredRowsCount = textWidth / currentBlockWidth;
        var integerPart = (int)requiredRowsCount;
        var fractionalPart = requiredRowsCount - integerPart;
        if (fractionalPart > 0)
        {
            integerPart++;
        }

        var requiredHeight = (integerPart * textHeight) + tMarginPixel + bMarginPixel;
        this.Height = (int)requiredHeight + tMarginPixel + bMarginPixel + tMarginPixel + bMarginPixel;

        // We should raise the shape up by the amount which is half of the increased offset.
        // PowerPoint does the same thing.
        var yOffset = (requiredHeight - currentBlockHeight) / 2;
        this.Y -= (int)yOffset;
    }

    private void UpdateWidthIfNeed(SKPaint paint, int lMarginPixel, int rMarginPixel)
    {
        if (!this.TextFrame!.TextWrapped)
        {
            var longerText = this.TextFrame.Paragraphs
                .Select(x => new { x.Text, x.Text.Length })
                .OrderByDescending(x => x.Length)
                .First().Text;
            var paraTextRect = default(SKRect);
            var widthInPixels = paint.MeasureText(longerText, ref paraTextRect);
            this.Width = (int)(widthInPixels * Scale) + lMarginPixel + rMarginPixel;
        }
    }

    private SCTextFrame? ParseTextFrame()
    {
        var pTextBody = this.pShape.GetFirstChild<P.TextBody>();
        if (pTextBody == null)
        {
            return null;
        }

        var newTextFrame = new SCTextFrame(this, pTextBody);
        newTextFrame.TextChanged += this.ResizeShape;

        return newTextFrame;
    }

    private AutoShapeFill GetFill()
    {
        var useBgFill = pShape.UseBackgroundFill;
        return new AutoShapeFill(
            this.pShape.GetFirstChild<P.ShapeProperties>() !, 
            this, 
            useBgFill);
    }
    
    public int X { get; set; }
    public int Y { get; set; }

    internal void CopyTo(int id, P.ShapeTree pShapeTree, IEnumerable<string> existingShapeNames, SlidePart targetSdkSlidePart)
    {
        var copy = this.pShape.CloneNode(true);
        copy.GetNonVisualDrawingProperties().Id = new UInt32Value((uint)id);
        pShapeTree.AppendChild(copy);
        var copyName = copy.GetNonVisualDrawingProperties().Name!.Value!;
        if (existingShapeNames.Any(existingShapeName => existingShapeName == copyName))
        {
            var currentShapeCollectionSuffixes = existingShapeNames 
                .Where(c => c.StartsWith(copyName, StringComparison.InvariantCulture))
                .Select(c => c.Substring(copyName.Length))
                .ToArray();

            // We will try to check numeric suffixes only.
            var numericSuffixes = new List<int>();

            foreach (var currentSuffix in currentShapeCollectionSuffixes)
            {
                if (int.TryParse(currentSuffix, out var numericSuffix))
                {
                    numericSuffixes.Add(numericSuffix);
                }
            }

            numericSuffixes.Sort();
            var lastSuffix = numericSuffixes.LastOrDefault() + 1;
            copy.GetNonVisualDrawingProperties().Name = copyName + " " + lastSuffix;
        }
    }

    internal SlideMaster SlideMaster()
    {
        return this.parentShapeCollection.SlideMaster();
    }

    public SlidePart SDKSlidePart()
    {
        return this.parentShapeCollection.SDKSlidePart();
    }

    internal List<ImagePart> SDKImageParts()
    {
        return this.parentShapeCollection.SDKImageParts();
    }

    public PresentationCore Presentation()
    {
        return this.parentShapeCollection.Presentation();
    }
}