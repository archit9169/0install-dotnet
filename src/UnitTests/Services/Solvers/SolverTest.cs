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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NanoByte.Common.Storage;
using NanoByte.Common.Streams;
using Xunit;
using ZeroInstall.Services.Feeds;
using ZeroInstall.Store;
using ZeroInstall.Store.Model;
using ZeroInstall.Store.Model.Preferences;
using ZeroInstall.Store.Model.Selection;

namespace ZeroInstall.Services.Solvers
{
    /// <summary>
    /// Contains common code for testing specific <see cref="ISolver"/> implementations.
    /// </summary>
    public abstract class SolverTest : TestWithMocksAndRedirect
    {
        [Fact]
        public void TestCases()
        {
            TestCaseSet Load()
            {
                using (var stream = typeof(SolverTest).GetEmbeddedStream("test-cases.xml"))
                    return XmlStorage.LoadXml<TestCaseSet>(stream);
            }

            foreach (var testCase in Load().TestCases)
            {
                if (testCase.Problem == null)
                {
                    this.Invoking(x => x.Solve(testCase.Feeds, testCase.Requirements)
                            .Should().Be(testCase.Selections, testCase.ToString()))
                        .ShouldNotThrow(testCase.ToString());
                }
                else
                {
                    this.Invoking(x => x.Solve(testCase.Feeds, testCase.Requirements))
                        .ShouldThrow<Exception>(testCase.ToString())/*.WithMessage(testCase.Problem)*/;
                }
            }
        }

        [Fact]
        public void CustomFeedReference()
        {
            new InterfacePreferences {Feeds = {new FeedReference {Source = new FeedUri("http://example.com/prog2.xml")}}}.SaveFor(new FeedUri("http://example.com/prog1.xml"));

            var actual = Solve(
                feeds: new[]
                {
                    new Feed
                    {
                        Uri = new FeedUri("http://example.com/prog1.xml"),
                        Elements = {new Implementation {Version = new ImplementationVersion("1.0"), ID = "app1", Commands = {new Command {Name = Command.NameRun, Path = "test-app1"}}}}
                    },
                    new Feed
                    {
                        Uri = new FeedUri("http://example.com/prog2.xml"),
                        Elements = {new Implementation {Version = new ImplementationVersion("2.0"), ID = "app2", Commands = {new Command {Name = Command.NameRun, Path = "test-app2"}}}}
                    }
                },
                requirements: new Requirements("http://example.com/prog1.xml", Command.NameRun));

            actual.Should().Be(new Selections
            {
                InterfaceUri = new FeedUri("http://example.com/prog1.xml"),
                Command = Command.NameRun,
                Implementations =
                {
                    new ImplementationSelection {InterfaceUri = new FeedUri("http://example.com/prog1.xml"), FromFeed = new FeedUri("http://example.com/prog2.xml"), Version = new ImplementationVersion("2.0"), ID = "app2", Commands = {new Command {Name = Command.NameRun, Path = "test-app2"}}}
                }
            });
        }

        [Fact]
        public void ExtraRestrictions()
        {
            var actual = Solve(
                feeds: new[]
                {
                    new Feed
                    {
                        Uri = new FeedUri("http://example.com/prog.xml"),
                        Elements =
                        {
                            new Implementation {Version = new ImplementationVersion("1.0"), ID = "app1", Commands = {new Command {Name = Command.NameRun, Path = "test-app1"}}},
                            new Implementation {Version = new ImplementationVersion("2.0"), ID = "app2", Commands = {new Command {Name = Command.NameRun, Path = "test-app2"}}}
                        }
                    }
                },
                requirements: new Requirements("http://example.com/prog.xml", Command.NameRun)
                {
                    ExtraRestrictions = {{new FeedUri("http://example.com/prog.xml"), new VersionRange("..!2.0")}}
                });

            actual.Should().Be(new Selections
            {
                InterfaceUri = new FeedUri("http://example.com/prog.xml"),
                Command = Command.NameRun,
                Implementations =
                {
                    new ImplementationSelection {InterfaceUri = new FeedUri("http://example.com/prog.xml"), Version = new ImplementationVersion("1.0"), ID = "app1", Commands = {new Command {Name = Command.NameRun, Path = "test-app1"}}}
                }
            });
        }

        private Selections Solve(IEnumerable<Feed> feeds, Requirements requirements)
        {
            var feedLookup = feeds.ToDictionary(
                keySelector: feed => feed.Uri,
                elementSelector: feed =>
                {
                    feed.Normalize(feed.Uri);
                    return feed;
                });

            var feedManagerMock = CreateMock<IFeedManager>();
            feedManagerMock.Setup(x => x[It.IsAny<FeedUri>()]).Returns((FeedUri feedUri) =>
            {
                if (feedLookup.TryGetValue(feedUri, out var feed)) return feed;
                else throw new WebException($"Unable to fetch {feedUri}.");
            });

            return BuildSolver(feedManagerMock.Object).Solve(requirements);
        }

        protected abstract ISolver BuildSolver(IFeedManager feedManager);
    }
}
