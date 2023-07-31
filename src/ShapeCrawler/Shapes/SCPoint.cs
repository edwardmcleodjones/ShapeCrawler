﻿// ReSharper disable once CheckNamespace
namespace ShapeCrawler;

/// <summary>
///     Represent a point.
/// </summary>
public readonly ref struct SCPoint
{
    internal SCPoint(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    
    /// <summary>
    ///     Gets or sets the X coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    ///     Gets or sets the Y coordinate.
    /// </summary>
    public int Y { get; }
}