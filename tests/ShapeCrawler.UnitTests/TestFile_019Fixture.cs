﻿using System;
using SlideDotNet.Models;

namespace ShapeCrawler.UnitTests
{
    public class TestFile_019Fixture : IDisposable
    {
        public PresentationEx pre019 { get; }

        public TestFile_019Fixture()
        {
            pre019 = new PresentationEx(Properties.Resources._019);
        }

        public void Dispose()
        {
            pre019.Close();
        }
    }
}