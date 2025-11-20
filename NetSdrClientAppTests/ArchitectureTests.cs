using NetArchTest.Rules;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    public class ArchitectureTests
    {

        private const string MessagesNamespace = "NetSdrClientApp.Messages";
        private const string NetworkingNamespace = "NetSdrClientApp.Networking";
        private const string ClientNamespace = "NetSdrClientApp";
        private const string TestsNamespace = "NetSdrClientAppTests";

        [Test]
        public void Messages_Should_Not_Depend_On_Networking()
        {
            // Arrange
            var assembly = typeof(NetSdrClientApp.Messages.NetSdrMessageHelper).Assembly;

            // Act
            var result = Types
                .InAssembly(assembly)
                .That().ResideInNamespace(MessagesNamespace)
                .ShouldNot()
                .HaveDependencyOn(NetworkingNamespace)
                .GetResult();

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }


        [Test]
        public void Client_Can_Depend_On_Messages_And_Networking()
        {
            // Arrange
            var assembly = typeof(NetSdrClientApp.NetSdrClient).Assembly;

            // Act
            var result = Types
                .InAssembly(assembly)
                .That().ResideInNamespace(ClientNamespace)
                .And().HaveDependencyOnAny(MessagesNamespace, NetworkingNamespace)
                .GetTypes();

            // Assert
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void Tests_Should_Only_Depend_On_Client_Project()
        {
            // Arrange
            var assembly = typeof(ArchitectureTests).Assembly;

            // Act
            var result = Types
                .InAssembly(assembly)
                .That().ResideInNamespace(TestsNamespace)
                .Should()
                .HaveDependencyOn(ClientNamespace)
                .And()
                .NotHaveDependencyOn("EchoServer")
                .GetResult();

            // Assert
            Assert.IsTrue(result.IsSuccessful);
        }
    }
}


