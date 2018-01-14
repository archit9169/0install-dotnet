﻿/*
 * Copyright 2010-2016 Bastian Eicher
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser Public License for more details.
 *
 * You should have received a copy of the GNU Lesser Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using Moq;
using NanoByte.Common.Native;
using NanoByte.Common.Storage;
using Xunit;
using ZeroInstall.Store;
using ZeroInstall.Store.Model;

namespace ZeroInstall.DesktopIntegration.Windows
{
    /// <summary>
    /// Contains test methods for <see cref="StubBuilder"/>.
    /// </summary>
    public sealed class StubBuilderTest
    {
        private readonly Mock<IIconStore> iconStoreMock = new Mock<IIconStore>();

        [SkippableFact]
        public void TestBuildStubGui()
        {
            Skip.IfNot(WindowsUtils.IsWindows, "StubBuilder is only used on Windows");

            var target = new FeedTarget(FeedTest.Test1Uri, FeedTest.CreateTestFeed());
            using (var tempFile = new TemporaryFile("0install-unit-tests"))
                StubBuilder.BuildRunStub(target, tempFile, iconStoreMock.Object, needsTerminal: false);
        }

        [SkippableFact]
        public void TestBuildStubNeedsTerminal()
        {
            Skip.IfNot(WindowsUtils.IsWindows, "StubBuilder is only used on Windows");

            var target = new FeedTarget(FeedTest.Test1Uri, FeedTest.CreateTestFeed());
            using (var tempFile = new TemporaryFile("0install-unit-tests"))
                StubBuilder.BuildRunStub(target, tempFile, iconStoreMock.Object, needsTerminal: true);
        }
    }
}
