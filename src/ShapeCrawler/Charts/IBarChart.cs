﻿using System.Collections.Generic;
using DocumentFormat.OpenXml.Packaging;
using OneOf;
using ShapeCrawler.Charts;
using ShapeCrawler.Shapes;
using P = DocumentFormat.OpenXml.Presentation;

// ReSharper disable once CheckNamespace
namespace ShapeCrawler;

/// <summary>
///     Represents a Bar or Column chart.
/// </summary>
public interface IBarChart : IChart
{
}

internal sealed class SCSlideBarChart : SCSlideChart, IBarChart
{
    internal SCSlideBarChart(
        P.GraphicFrame pGraphicFrame, 
        OneOf<SCSlide, SCSlideLayout, SCSlideMaster> slideOf,
        OneOf<SCSlideShapes, SCSlideGroupShape> shapeCollectionOf,
        TypedOpenXmlPart slideTypedOpenXmlPart,
        List<ChartWorkbook> chartWorkbooks)
        : base(pGraphicFrame, slideOf, shapeCollectionOf, slideTypedOpenXmlPart, chartWorkbooks)
    {
    }
}