// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NanoByte.Common.Net;
using NanoByte.Common.Storage;
using NanoByte.Common.Streams;
using Xunit;
using ZeroInstall.FileSystem;
using ZeroInstall.Model;
using ZeroInstall.Services.Native;
using ZeroInstall.Store;
using ZeroInstall.Store.Implementations;
using ZeroInstall.Store.Implementations.Archives;

namespace ZeroInstall.Services.Fetchers
{
    /// <summary>
    /// Contains test methods for <see cref="IFetcher"/> implementations.
    /// </summary>
    public abstract class FetcherTest : TestWithMocks
    {
        protected static readonly Stream ZipArchiveStream = typeof(FetcherTest).GetEmbeddedStream("testArchive.zip");
        protected static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        protected readonly Config Config = new Config();
        protected readonly Mock<IImplementationStore> StoreMock;

        protected FetcherTest()
        {
            StoreMock = CreateMock<IImplementationStore>();
        }

        protected readonly MockTaskHandler Handler = new MockTaskHandler();

        protected abstract IFetcher BuildFetcher();

        [Fact]
        public void DownloadSingleArchive()
        {
            StoreMock.Setup(x => x.Flush());
            using var server = new MicroServer("archive.zip", ZipArchiveStream);
            TestDownloadArchives(
                new Archive {Href = server.FileUri, MimeType = Archive.MimeTypeZip, Size = ZipArchiveStream.Length, Extract = "extract", Destination = "destination"});
        }

        [Fact]
        public void DownloadLocalArchive()
        {
            StoreMock.Setup(x => x.Flush());
            using var tempFile = new TemporaryFile("0install-unit-tests");
            ZipArchiveStream.CopyToFile(tempFile);
            TestDownloadArchives(
                new Archive {Href = new Uri(tempFile), MimeType = Archive.MimeTypeZip, Size = ZipArchiveStream.Length, Extract = "extract", Destination = "destination"});
        }

        [Fact]
        public void DownloadMultipleArchives()
        {
            StoreMock.Setup(x => x.Flush());
            using var server1 = new MicroServer("archive.zip", ZipArchiveStream);
            using var server2 = new MicroServer("archive.zip", ZipArchiveStream);
            TestDownloadArchives(
                new Archive {Href = server1.FileUri, MimeType = Archive.MimeTypeZip, Size = ZipArchiveStream.Length, Extract = "extract1", Destination = "destination1"},
                new Archive {Href = server2.FileUri, MimeType = Archive.MimeTypeZip, Size = ZipArchiveStream.Length, Extract = "extract2", Destination = "destination2"});
        }

        [Fact]
        public void DownloadSingleFile()
        {
            StoreMock.Setup(x => x.Flush());
            using var server = new MicroServer("regular", TestFile.DefaultContents.ToStream());
            TestDownload(
                new TestRoot {new TestFile("regular") {LastWrite = UnixEpoch}},
                new SingleFile {Href = server.FileUri, Size = TestFile.DefaultContents.Length, Destination = "regular"});
        }

        [Fact]
        public void DownloadRecipe()
        {
            StoreMock.Setup(x => x.Flush());
            using var serverArchive = new MicroServer("archive.zip", ZipArchiveStream);
            using var serverSingleFile = new MicroServer("regular", TestFile.DefaultContents.ToStream());
            TestDownload(
                new TestRoot
                {
                    new TestFile("regular") {LastWrite = new DateTime(2000, 1, 1, 11, 0, 0, DateTimeKind.Utc)},
                    new TestFile("regular2") {LastWrite = UnixEpoch},
                    new TestFile("executable2") {IsExecutable = true, LastWrite = new DateTime(2000, 1, 1, 11, 0, 0, DateTimeKind.Utc)}
                },
                new Recipe
                {
                    Steps =
                    {
                        new Archive {Href = serverArchive.FileUri, MimeType = Archive.MimeTypeZip, Size = ZipArchiveStream.Length},
                        new RenameStep {Source = "executable", Destination = "executable2"},
                        new SingleFile {Href = serverSingleFile.FileUri, Size = TestFile.DefaultContents.Length, Destination = "regular2"}
                    }
                });
        }

        [Fact]
        public void SkipBroken()
        {
            StoreMock.Setup(x => x.Flush());
            using var serverArchive = new MicroServer("archive.zip", ZipArchiveStream);
            using var serverSingleFile = new MicroServer("regular", TestFile.DefaultContents.ToStream());
            TestDownload(
                new TestRoot {new TestFile("regular") {LastWrite = UnixEpoch}},
                // broken: wrong size
                new Archive {Href = serverArchive.FileUri, MimeType = Archive.MimeTypeZip, Size = 0},
                // broken: unknown archive format
                new Archive {Href = serverArchive.FileUri, MimeType = "test/format", Size = ZipArchiveStream.Length},
                // works
                new Recipe {Steps = {new SingleFile {Href = serverSingleFile.FileUri, Size = TestFile.DefaultContents.Length, Destination = "regular"}}});
        }

        #region Helpers
        protected void TestDownloadArchives(params Archive[] archives)
        {
            var digest = new ManifestDigest(sha256New: "test123");
            var archiveInfos = archives.Select(archive => new ArchiveFileInfo("dummy", archive.MimeType)
            {
                Extract = archive.Extract,
                Destination = archive.Destination,
                StartOffset = archive.StartOffset,
                OriginalSource = archive.Href
            }).ToList();
            var testImplementation = new Implementation {ID = "test", ManifestDigest = digest, RetrievalMethods = {GetRetrievalMethod(archives)}};

            StoreMock.Setup(x => x.GetPath(digest)).Returns(() => null);
            StoreMock.Setup(x => x.AddArchives(archiveInfos, digest, Handler)).Returns("");

            BuildFetcher().Fetch(new[] {testImplementation});
        }

        private static RetrievalMethod GetRetrievalMethod(Archive[] archives)
        {
            if (archives.Length == 1) return archives[0];
            else
            {
                var recipe = new Recipe();
                recipe.Steps.AddRange(archives);
                return recipe;
            }
        }

        private void TestDownload(TestRoot expected, params RetrievalMethod[] retrievalMethod)
        {
            var digest = new ManifestDigest(sha256New: "test123");
            var testImplementation = new Implementation {ID = "test", ManifestDigest = digest};
            testImplementation.RetrievalMethods.AddRange(retrievalMethod);

            StoreMock.Setup(x => x.GetPath(digest)).Returns(() => null);
            StoreMock.Setup(x => x.AddDirectory(It.Is<string>(path => expected.Verify(path)), digest, Handler)).Returns("");

            BuildFetcher().Fetch(new[] {testImplementation});
        }
        #endregion

        [Fact]
        public void RunExternalConfirm()
        {
            bool installInvoked = false;
            Handler.AnswerQuestionWith = true;
            BuildFetcher().Fetch(new[]
            {
                new Implementation
                {
                    ID = ExternalImplementation.PackagePrefix + "123",
                    RetrievalMethods =
                    {
                        new ExternalRetrievalMethod
                        {
                            ConfirmationQuestion = "Install?",
                            Install = () => { installInvoked = true; }
                        }
                    }
                }
            });
            installInvoked.Should().BeTrue();
        }

        [Fact]
        public void RunExternalDeny()
        {
            bool installInvoked = false;
            Handler.AnswerQuestionWith = false;
            Assert.Throws<OperationCanceledException>(() => BuildFetcher().Fetch(new[]
            {
                new Implementation
                {
                    ID = ExternalImplementation.PackagePrefix + "123",
                    RetrievalMethods =
                    {
                        new ExternalRetrievalMethod
                        {
                            ConfirmationQuestion = "Install?",
                            Install = () => { installInvoked = true; }
                        }
                    }
                }
            }));
            installInvoked.Should().BeFalse();
        }

        [Fact]
        public void SkipExisting()
        {
            var digest = new ManifestDigest(sha256New: "test123");
            var testImplementation = new Implementation {ID = "test", ManifestDigest = digest, RetrievalMethods = {new Recipe()}};
            StoreMock.Setup(x => x.Flush());
            StoreMock.Setup(x => x.GetPath(digest)).Returns("dummy");

            BuildFetcher().Fetch(new[] {testImplementation});
        }

        [Fact]
        public void NoSuitableMethod()
        {
            var implementation = new Implementation {ID = "test", ManifestDigest = new ManifestDigest(sha256New: "test123")};
            StoreMock.Setup(x => x.Flush());
            StoreMock.Setup(x => x.GetPath(implementation.ManifestDigest)).Returns(() => null);

            Assert.Throws<NotSupportedException>(() => BuildFetcher().Fetch(new[] {implementation}));
        }

        [Fact]
        public void UnsupportedArchiveFormat()
        {
            var implementation = new Implementation
            {
                ID = "test",
                ManifestDigest = new ManifestDigest(sha256New: "test123"),
                RetrievalMethods = {new Archive {MimeType = "test/format"}}
            };
            StoreMock.Setup(x => x.Flush());
            StoreMock.Setup(x => x.GetPath(implementation.ManifestDigest)).Returns(() => null);

            Assert.Throws<NotSupportedException>(() => BuildFetcher().Fetch(new[] {implementation}));
        }

        [Fact]
        public void UnsupportedArchiveFormatInRecipe()
        {
            var implementation = new Implementation
            {
                ID = "test",
                ManifestDigest = new ManifestDigest(sha256New: "test123"),
                RetrievalMethods = {new Recipe {Steps = {new Archive {MimeType = Archive.MimeTypeZip}, new Archive {MimeType = "test/format"}}}}
            };
            StoreMock.Setup(x => x.Flush());
            StoreMock.Setup(x => x.GetPath(implementation.ManifestDigest)).Returns(() => null);

            Assert.Throws<NotSupportedException>(() => BuildFetcher().Fetch(new[] {implementation}));
        }
    }
}
