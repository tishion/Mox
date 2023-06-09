using Mox;
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace MoxDebugHost
{
    public class MockedNetworkInterface : NetworkInterface
    {
        private string mDescription;
        private string mAddress;
        public MockedNetworkInterface(string desc, string addr)
        {
            mAddress = addr;
            mDescription = desc;
        }

        public override string Description
        {
            get
            {
                return mDescription;
            }
        }

        public override PhysicalAddress GetPhysicalAddress()
        {
            return PhysicalAddress.Parse(mAddress);
        }
    }

    public class SimpleClass
    {
        const string DEFAULT_MAC_ADDRESS = "FFFFFFFFFFFF";

        public SimpleClass() => Field = "OrignalField";

        public string Field { get; set; }

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
            Debug.WriteLine("====== in GetMacAddress!");
            var nics = NetworkInterface.GetAllNetworkInterfaces();

            Debug.WriteLine($"===== total count {nics.Length}");
            foreach (NetworkInterface networkInterface in nics)
            {
                var desc = networkInterface.Description;
                var addr = networkInterface.GetPhysicalAddress().ToString();
                Console.WriteLine($"- nic desc: {desc}, addr:{addr}");
                if (desc == "en0")
                {
                    return addr;
                }
            }
            return DEFAULT_MAC_ADDRESS;
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
//#if NETFRAMEWORK
//            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
//#elif NETCOREAPP
//            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
//#endif
            Console.WriteLine("Hello, World!");

            var result = "";
            var mockedAddress = "FF1122334455";

            var isolate = new Isolate();
            isolate
                .WhenCalled(() => NetworkInterface.GetAllNetworkInterfaces())
                .ReplaceWith(() =>
                {
                    Console.WriteLine("====== in mocked NetworkInterface.GetAllNetworkInterfaces!");

                    return new NetworkInterface[] {
                            //new MockedNetworkInterface("es2", "001122334455"),
                            //new MockedNetworkInterface("es1", "101122334455"),
                            //new MockedNetworkInterface("en1", "201122334455"),
                            //new MockedNetworkInterface("en0", mockedAddress),
                    };
                });

            isolate
                .Run(() =>
                {
                    Console.WriteLine("====== in Run Body!");
                    result = SimpleClass.GetMacAddress();
                });

            Console.WriteLine(result);
            Console.WriteLine("Goodbye, World!");
        }
    }
}