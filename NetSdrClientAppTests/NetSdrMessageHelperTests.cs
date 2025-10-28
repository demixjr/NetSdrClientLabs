using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        //TODO: add more NetSdrMessageHelper tests
        [Test]
        public void TranslateDataItemMessage_WorksCorrectly()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            var parameters = new byte[10];
            var msg = NetSdrMessageHelper.GetDataItemMessage(type, parameters);

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var actualType, out var itemCode, out var sequenceNumber, out var body);

            // Assert
            Assert.IsTrue(success);
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(body.Length, Is.EqualTo(parameters.Length  - 2));
        }
      
        [Test]
        public void GetSamples_ReturnsCorrectIntValues()
        {
            // Arrange
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            ushort sampleSize = 16;

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x01, 0x02, 0x00, 0x00 }, 0)));
            Assert.That(samples[1], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x03, 0x04, 0x00, 0x00 }, 0)));
        }

        [Test]
        public void GetSamples_Throws_WhenSampleSizeTooLarge()
        {
            // Arrange
            var body = new byte[] { 0x01, 0x02 };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => NetSdrMessageHelper.GetSamples(40, body).ToArray());
        }

    }
}