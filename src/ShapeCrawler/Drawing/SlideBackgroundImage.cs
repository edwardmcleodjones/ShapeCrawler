﻿using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using ShapeCrawler.Exceptions;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace ShapeCrawler.Drawing;

internal sealed class SlideBackgroundImage : ISlideBackgroundImage
{
    private readonly SCSlide slide;
    private readonly P.ShapeTree pShapeTree;

    private const string NotPresentedErrorMessage =
        $"Background image is not presented. Use {nameof(ISlideBackgroundImage.Present)} to check.";

    public string MIME => this.ParseMIME();

    public string Name => this.ParseName();

    public void Update(Stream stream)
    {
        A.Blip aBlip = ParseABlip();
        var imageParts = this.slide.SDKImageParts();
        ImagePart sdkImagePart = this.SDKImagePartOrNull();
        var isSharedImagePart = imageParts.Count(x => x == sdkImagePart) > 1;
        if (isSharedImagePart)
        {
            var rId = $"rId-{Guid.NewGuid().ToString("N").Substring(0, 5)}";
            sdkImagePart = this.slide.SDKSlidePart().AddNewPart<ImagePart>("image/png", rId);
            aBlip.Embed!.Value = rId;
        }

        stream.Position = 0;
        sdkImagePart.FeedData(stream);
    }

    public void Update(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);

        this.Update(stream);
    }

    public void Update(string filePath)
    {
        byte[] sourceBytes = File.ReadAllBytes(filePath);
        this.Update(sourceBytes);
    }
    
    public byte[] BinaryData()
    {
        var sdkImagePart = this.SDKImagePartOrNull();
        if (sdkImagePart == null)
        {
            throw new SCException(NotPresentedErrorMessage);
        }

        var stream = sdkImagePart.GetStream();
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, (int)stream.Length);
        stream.Close();

        return bytes;
    }

    public bool Present()
    {
        throw new NotImplementedException();
    }

    internal SlideBackgroundImage(SCSlide parentSlide, P.ShapeTree pShapeTree)
    {
        this.slide = parentSlide;
        this.pShapeTree = pShapeTree;
    }

    private A.Blip ParseABlip()
    {
        throw new NotImplementedException();
    }

    private string ParseMIME()
    {
        var sdkImagePart = this.SDKImagePartOrNull();
        if (sdkImagePart == null)
        {
            throw new SCException(
                $"Background image is not presented. Use {nameof(ISlideBackgroundImage.Present)} to check.");
        }

        return sdkImagePart.ContentType;
    }

    private ImagePart SDKImagePartOrNull()
    {
        throw new NotImplementedException();
    }

    private string ParseName()
    {
        var sdkImagePart = this.SDKImagePartOrNull();
        if (sdkImagePart == null)
        {
            throw new SCException(NotPresentedErrorMessage);
        }

        return Path.GetFileName(sdkImagePart.Uri.ToString());
    }
}