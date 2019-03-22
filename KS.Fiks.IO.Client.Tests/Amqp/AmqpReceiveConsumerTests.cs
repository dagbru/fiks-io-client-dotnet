using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using KS.Fiks.IO.Client.Exceptions;
using KS.Fiks.IO.Client.Models;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace KS.Fiks.IO.Client.Tests.Amqp
{
    public class AmqpReceiveConsumerTests
    {
        private AmqpReceiveConsumerFixture _fixture;

        public AmqpReceiveConsumerTests()
        {
            _fixture = new AmqpReceiveConsumerFixture();
        }

        [Fact]
        public void ReceivedHandler()
        {
            var sut = _fixture.CreateSut();

            var hasBeenCalled = false;
            var handler = new EventHandler<MessageReceivedArgs>((a, _) => { hasBeenCalled = true; });

            sut.Received += handler;

            sut.HandleBasicDeliver(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                Array.Empty<byte>());

            hasBeenCalled.Should().BeTrue();
        }

        [Fact]
        public void ReceivesExpectedMessageMetadata()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", expectedMessageMetadata.SenderAccountId},
                {"melding-id", expectedMessageMetadata.MessageId},
                {"type", expectedMessageMetadata.MessageType},
                {"svar-til", expectedMessageMetadata.SvarPaMelding}
            };

            var propertiesMock = new Mock<IBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns(headers);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            var actualMessage = new ReceivedMessage();
            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) =>
            {
                actualMessage = messageArgs.Message;
            });

            sut.Received += handler;

            sut.HandleBasicDeliver(
                "tag",
                34,
                false,
                "exchange",
                expectedMessageMetadata.ReceiverAccountId.ToString(),
                propertiesMock.Object,
                Array.Empty<byte>());

            actualMessage.MessageId.Should().Be(expectedMessageMetadata.MessageId);
            actualMessage.MessageType.Should().Be(expectedMessageMetadata.MessageType);
            actualMessage.ReceiverAccountId.Should().Be(expectedMessageMetadata.ReceiverAccountId);
            actualMessage.SenderAccountId.Should().Be(expectedMessageMetadata.SenderAccountId);
            actualMessage.SvarPaMelding.Should().Be(expectedMessageMetadata.SvarPaMelding);
            actualMessage.Ttl.Should().Be(expectedMessageMetadata.Ttl);
        }

        [Fact]
        public void ThrowsParseExceptionIfMessageIdIsNotValidGuid()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", expectedMessageMetadata.SenderAccountId},
                {"melding-id", "NotAGuid"},
                {"type", expectedMessageMetadata.MessageType},
                {"svar-til", expectedMessageMetadata.SvarPaMelding}
            };

            var propertiesMock = new Mock<IBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns(headers);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) => { });

            sut.Received += handler;
            Assert.Throws<FiksIOParseException>(() =>
            {
                sut.HandleBasicDeliver(
                    "tag",
                    34,
                    false,
                    "exchange",
                    expectedMessageMetadata.ReceiverAccountId.ToString(),
                    propertiesMock.Object,
                    Array.Empty<byte>());
            });
        }

        [Fact]
        public void ThrowsMissingHeaderExceptionExceptionIfHeaderIsNull()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", expectedMessageMetadata.SenderAccountId},
                {"melding-id", "NotAGuid"},
                {"type", expectedMessageMetadata.MessageType},
                {"svar-til", expectedMessageMetadata.SvarPaMelding}
            };

            var propertiesMock = new Mock<IBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns((IDictionary<string, object>)null);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) => { });

            sut.Received += handler;
            Assert.Throws<FiksIOMissingHeaderException>(() =>
            {
                sut.HandleBasicDeliver(
                    "tag",
                    34,
                    false,
                    "exchange",
                    expectedMessageMetadata.ReceiverAccountId.ToString(),
                    propertiesMock.Object,
                    Array.Empty<byte>());
            });
        }

        [Fact]
        public void ThrowsMissingHeaderExceptionExceptionIfMessageIdIsMissing()
        {
            var expectedMessageMetadata = _fixture.DefaultMetadata;

            var headers = new Dictionary<string, object>
            {
                {"avsender-id", expectedMessageMetadata.SenderAccountId},
                {"type", expectedMessageMetadata.MessageType},
                {"svar-til", expectedMessageMetadata.SvarPaMelding}
            };

            var propertiesMock = new Mock<IBasicProperties>();
            propertiesMock.Setup(_ => _.Headers).Returns((IDictionary<string, object>)null);
            propertiesMock.Setup(_ => _.Expiration)
                          .Returns(
                              expectedMessageMetadata.Ttl.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));

            var sut = _fixture.CreateSut();
            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) => { });

            sut.Received += handler;
            Assert.Throws<FiksIOMissingHeaderException>(() =>
            {
                sut.HandleBasicDeliver(
                    "tag",
                    34,
                    false,
                    "exchange",
                    expectedMessageMetadata.ReceiverAccountId.ToString(),
                    propertiesMock.Object,
                    Array.Empty<byte>());
            });
        }

        [Fact]
        public void FileWriterWriteIsCalledWhenWriteEncryptedZip()
        {
            var sut = _fixture.CreateSut();

            var filePath = "/my/path/something.zip";
            var data = new[] {default(byte) };

            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) =>
            {
                messageArgs.Message.WriteEncryptedZip(filePath);
            });

            sut.Received += handler;

            sut.HandleBasicDeliver(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.FileWriterMock.Verify(_ => _.Write(filePath, data));
        }

        [Fact]
        public async Task DataAsStreamIsReturnedWhenGettingEncryptedStream()
        {
            var sut = _fixture.CreateSut();

            var data = new[] {default(byte), byte.MaxValue};

            Stream actualDataStream = new MemoryStream();
            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) =>
            {
                actualDataStream = messageArgs.Message.EncryptedStream;
            });

            sut.Received += handler;

            sut.HandleBasicDeliver(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            var actualData = new byte[2];
            await actualDataStream.ReadAsync(actualData, 0, 2).ConfigureAwait(false);

            actualData[0].Should().Be(data[0]);
            actualData[1].Should().Be(data[1]);
        }

        [Fact]
        public async Task PayloadDecrypterDecryptIsCalledWhenGettingDecryptedStream()
        {
            var sut = _fixture.CreateSut();

            var data = new[] {default(byte), byte.MaxValue};

            Stream actualDataStream = new MemoryStream();
            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) =>
            {
                actualDataStream = messageArgs.Message.DecryptedStream;
            });

            sut.Received += handler;

            sut.HandleBasicDeliver(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.PayloadDecrypterMock.Verify(_ => _.Decrypt(data));

            var actualData = new byte[2];
            await actualDataStream.ReadAsync(actualData, 0, 2).ConfigureAwait(false);
            actualData[0].Should().Be(data[0]);
            actualData[1].Should().Be(data[1]);
        }

        [Fact]
        public void PayloadDecrypterAndFileWriterIsCalledWhenWriteDecryptedFile()
        {
            var sut = _fixture.CreateSut();

            var data = new[] {default(byte), byte.MaxValue};
            var filePath = "/my/path/something.zip";

            var handler = new EventHandler<MessageReceivedArgs>((a, messageArgs) =>
            {
                messageArgs.Message.WriteDecryptedZip(filePath);
            });

            sut.Received += handler;

            sut.HandleBasicDeliver(
                "tag",
                34,
                false,
                "exchange",
                Guid.NewGuid().ToString(),
                _fixture.DefaultProperties,
                data);

            _fixture.PayloadDecrypterMock.Verify(_ => _.Decrypt(data));

            _fixture.FileWriterMock.Verify(_ => _.Write(filePath, It.IsAny<Stream>()));
        }
    }
}