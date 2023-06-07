using Mox;
using NUnit.Framework;
using System;
using System.Net.NetworkInformation;

namespace MoxTests
{
    class MockedNetworkInterface : NetworkInterface
    {
        private string desc;
        public string address;

        public MockedNetworkInterface(string desc, string address) : base()
        {
            this.desc = desc;
            this.address = address;
        }

        public override string Description
        {
            get
            {
                return desc;
            }
        }

        public override PhysicalAddress GetPhysicalAddress()
        {
            return PhysicalAddress.Parse(address);
        }
    }

    public class SimpleClass
    {
        public SimpleClass() => field = "OrignalField";

        private string field;

        public string Property { get; set; }

        public string Greet(string name)
        {
            return "Hello " + name;
        }

        public string GoodBye()
        {
            return "Goodbye";
        }

        public static string StaticMethod(string s)
        {
            return "String from StaticMethod " + s;
        }

        public static string GetMacAddress()
        {
            string text = "FFFFFFFFFFFF";
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            //Console.WriteLine($"===== total count {nics.Length}");
            foreach (NetworkInterface networkInterface in nics)
            {
                var desc = networkInterface.Description;
                var addr = networkInterface.GetPhysicalAddress().ToString();
                //Console.WriteLine($"- nic desc: {desc}, addr:{addr}");
                if (desc == "en0")
                {
                    text = addr;
                    break;
                }
            }
            return text;
        }

        public string CallOverloadedMethod()
        {
            return OverloadedMethod();
        }

        public string CallOverloadedMethod(string s, int a)
        {
            return OverloadedMethod(s, a);
        }

        private string OverloadedMethod()
        {
            var s = "OverloadedMethod()";
            Console.WriteLine(s);
            return s;
        }

        private string OverloadedMethod(string s, int a)
        {
            var r = "OverloadedMethod(string s, int a)";
            Console.WriteLine(r);
            return r;
        }
    }

    public class MoxTests
    {
        [SetUp]
        public void Setup()
        {
        }


        [Test]
        public void TestNewForm()
        {
            var mockedResult = "";
            var result = "";
            var isolate = new Isolate();
            isolate.WhenCalled(() => SimpleClass.StaticMethod(""))
                .ReplaceWith((string s) =>
                {
                    Console.WriteLine("Mocked: SimpleClass.StaticMethod()");
                    mockedResult = "String from mocked StaticMethod " + s;
                    return mockedResult;
                })
                .Run(() =>
                {
                    result = SimpleClass.StaticMethod("foo");
                });

            Assert.NotNull(result);
            Assert.That(result, Is.EqualTo(mockedResult));
        }

        [Test]
        public void TestMockStaticMethod()
        {
            var mockedResult = "";
            var result = "";
            var isolate = new Isolate();
            isolate.WhenCalled(() => SimpleClass.StaticMethod(""))
                .ReplaceWith((string s) =>
                {
                    Console.WriteLine("Mocked: SimpleClass.StaticMethod()");
                    mockedResult = "String from mocked StaticMethod " + s;
                    return mockedResult;
                });

            isolate.Run(() =>
            {
                result = SimpleClass.StaticMethod("foo");
            });

            Assert.NotNull(result);
            Assert.That(result, Is.EqualTo(mockedResult));
        }

        [Test]
        public void TestMockPrivateMethodOfType()
        {
            var result = "";
            var mockedResult = "Mocked: OverloadedMethod(string s, int a)";
            var isolate = new Isolate();
            isolate.WhenCalled(typeof(SimpleClass), "OverloadedMethod", typeof(string), typeof(int))
                .ReplaceWith((SimpleClass @this, string s, int a) =>
                {
                    Console.WriteLine(mockedResult);
                    return mockedResult;
                });

            isolate.Run(() =>
            {
                result = new SimpleClass().CallOverloadedMethod("s", 1);
            });

            Assert.That(result, Is.EqualTo(mockedResult));
        }

        [Test]
        public void TestMockPrivateMethodOfInstance()
        {
            var mockedResult = "Mocked: OverloadedMethod(string s, int a)";
            var simpleClassA = new SimpleClass();
            var simpleClassB = new SimpleClass();
            var resultOfA = "";

            var originalResult = simpleClassB.CallOverloadedMethod("s", 1);
            var resultOfB = "";

            var isolate = new Isolate();
            isolate.WhenCalled(simpleClassA, "OverloadedMethod", typeof(string), typeof(int))
                .ReplaceWith((SimpleClass @this, string s, int a) =>
                {
                    Console.WriteLine(mockedResult);
                    return mockedResult;
                });

            isolate.Run(() =>
            {
                resultOfA = simpleClassA.CallOverloadedMethod("s", 1);
                resultOfB = simpleClassB.CallOverloadedMethod("s", 1);
            });

            Assert.That(resultOfA, Is.EqualTo(mockedResult));
            Assert.That(resultOfB, Is.EqualTo(originalResult));
        }

        [Test]
        public void TestMockPublicMethodOfType()
        {
            var mockedResult = "Mocked Goodbye";
            var result = "";

            var isolate = new Isolate();
            isolate.WhenCalled(() => On.Class<SimpleClass>().GoodBye())
                .ReplaceWith((SimpleClass @this) =>
                {
                    Console.WriteLine("Mocked: SimpleClass.GoodBye()");
                    return mockedResult;
                });

            isolate.Run(() =>
            {
                result = new SimpleClass().GoodBye();
            });

            Assert.NotNull(result);
            Assert.That(result, Is.EqualTo(mockedResult));
        }

        [Test]
        public void TestMockPublicMethodOfInstance()
        {
            var mockedResult = "Mocked Goodbye";
            var result = "";
            var simpleClass = new SimpleClass();

            var isolate = new Isolate();
            isolate.WhenCalled(() => simpleClass.GoodBye())
                .ReplaceWith((SimpleClass @this) =>
                {
                    Console.WriteLine("Mocked: simpleClass.GoodBye()");
                    return mockedResult;
                });

            isolate.Run(() =>
            {
                result = simpleClass.GoodBye();
            });

            Assert.NotNull(result);
            Assert.That(result, Is.EqualTo(mockedResult));
        }

        [Test]
        public void TestMockEnvironmentGetCommandLineArgs()
        {
            // arrange
            var mockedRestult = new string[] { "1", "2" };
            var isolate = new Isolate();
            isolate.WhenCalled(() => Environment.GetCommandLineArgs())
                .ReplaceWith(() => mockedRestult);

            // act
            string[] result = null;
            isolate.Run(() =>
            {
                result = Environment.GetCommandLineArgs();
            });

            // assert
            Assert.IsNotEmpty(result);
            Assert.That(result.Length, Is.EqualTo(mockedRestult.Length));
            for (int i = 0; i < mockedRestult.Length; i++)
            {
                Assert.That(result[i], Is.EqualTo(mockedRestult[i]));
            }
        }

        [Test()]
        public void GetMacAddressTest()
        {
            var result = "";
            var mockedAddress = "0018103AB839";

            var isolate = new Isolate();
            isolate.WhenCalled(() => NetworkInterface.GetAllNetworkInterfaces())
                .ReplaceWith(() =>
                {
                    var r = new NetworkInterface[] {
                        new MockedNetworkInterface("es2", "0018103AB801"),
                        new MockedNetworkInterface("es1", "0018103AB800"),
                        new MockedNetworkInterface("en1", "0018103AB802"),
                        new MockedNetworkInterface("en0", mockedAddress),
                    };

                    return r;
                });

            isolate.Run(() =>
            {
                result = SimpleClass.GetMacAddress();
            });

            Assert.That(mockedAddress, Is.EqualTo(result));
        }
    }
}