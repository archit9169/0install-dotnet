// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Moq;
using Xunit;
using ZeroInstall.Model;
using ZeroInstall.Store.Trust;

namespace ZeroInstall.Store.Feeds
{
    /// <summary>
    /// Contains test methods for <see cref="FeedCacheExtensions"/>.
    /// </summary>
    public class FeedCacheExtensionsTest : TestWithMocks
    {
        /// <summary>
        /// Ensures <see cref="FeedCacheExtensions.GetAll"/> correctly loads <see cref="Feed"/>s from an <see cref="IFeedCache"/>, skipping any exceptions.
        /// </summary>
        [Fact]
        public void TestGetFeeds()
        {
            var feed1 = FeedTest.CreateTestFeed();
            var feed3 = FeedTest.CreateTestFeed();
            feed3.Uri = FeedTest.Test3Uri;

            var cacheMock = CreateMock<IFeedCache>();
            cacheMock.Setup(x => x.ListAll()).Returns(new[] {FeedTest.Test1Uri, FeedTest.Test2Uri, FeedTest.Test3Uri});
            cacheMock.Setup(x => x.GetFeed(FeedTest.Test1Uri)).Returns(feed1);
            cacheMock.Setup(x => x.GetFeed(FeedTest.Test2Uri)).Throws(new InvalidDataException("Fake exception for testing"));
            cacheMock.Setup(x => x.GetFeed(FeedTest.Test3Uri)).Returns(feed3);

            cacheMock.Object.GetAll().Should().Equal(feed1, feed3);
        }

        private const string FeedText = "Feed data\n";
        private readonly byte[] _feedBytes = Encoding.UTF8.GetBytes(FeedText);
        private static readonly byte[] _signatureBytes = Encoding.UTF8.GetBytes("Signature data");
        private static readonly string _signatureBase64 = Convert.ToBase64String(_signatureBytes).Insert(10, "\n");

        /// <summary>
        /// Ensures that <see cref="FeedUtils.GetSignatures"/> correctly separates an XML signature block from a signed feed.
        /// </summary>
        [Fact]
        public void TestGetSignatures()
        {
            var openPgpMock = CreateMock<IOpenPgp>();
            var result = new OpenPgpSignature[] {OpenPgpUtilsTest.TestSignature};
            openPgpMock.Setup(x => x.Verify(_feedBytes, _signatureBytes)).Returns(result);

            string input = FeedText + FeedUtils.SignatureBlockStart + _signatureBase64 + FeedUtils.SignatureBlockEnd;
            FeedUtils.GetSignatures(openPgpMock.Object, Encoding.UTF8.GetBytes(input)).Should().Equal(result);
        }

        /// <summary>
        /// Ensures that <see cref="FeedUtils.GetSignatures"/> throws a <see cref="SignatureException"/> if the signature block does not start in a new line.
        /// </summary>
        [Fact]
        public void TestGetSignaturesMissingNewLine()
        {
            string input = "Feed without newline" + FeedUtils.SignatureBlockStart + _signatureBase64 + FeedUtils.SignatureBlockEnd;
            Assert.Throws<SignatureException>(() => FeedUtils.GetSignatures(new Mock<IOpenPgp>().Object, Encoding.UTF8.GetBytes(input)));
        }

        /// <summary>
        /// Ensures that <see cref="FeedUtils.GetSignatures" /> throws a <see cref="SignatureException"/> if the signature contains non-base 64 characters.
        /// </summary>
        [Fact]
        public void TestGetSignaturesInvalidChars()
        {
            const string input = FeedText + FeedUtils.SignatureBlockStart + "*!?#" + FeedUtils.SignatureBlockEnd;
            Assert.Throws<SignatureException>(() => FeedUtils.GetSignatures(new Mock<IOpenPgp>().Object, Encoding.UTF8.GetBytes(input)));
        }

        /// <summary>
        /// Ensures that <see cref="FeedUtils.GetSignatures" /> throws a <see cref="SignatureException"/> if the correct signature end is missing.
        /// </summary>
        [Fact]
        public void TestGetSignaturesMissingEnd()
        {
            string input = FeedText + FeedUtils.SignatureBlockStart + _signatureBase64;
            Assert.Throws<SignatureException>(() => FeedUtils.GetSignatures(new Mock<IOpenPgp>().Object, Encoding.UTF8.GetBytes(input)));
        }

        /// <summary>
        /// Ensures that <see cref="FeedUtils.GetSignatures" /> throws a <see cref="SignatureException"/> if there is additional data after the signature block.
        /// </summary>
        [Fact]
        public void TestGetSignaturesDataAfterSignature()
        {
            string input = FeedText + FeedUtils.SignatureBlockStart + _signatureBase64 + FeedUtils.SignatureBlockEnd + "more data";
            Assert.Throws<SignatureException>(() => FeedUtils.GetSignatures(new Mock<IOpenPgp>().Object, Encoding.UTF8.GetBytes(input)));
        }
    }
}
