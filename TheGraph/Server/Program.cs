using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Thrift.Transport;
using Thrift.Server;

namespace TheGraph.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var handler = new TheGraph.Server.Handler();
                var processor = new TheGraph.Thrift.TheGraph.Processor(handler);
                TServerTransport serverTransport = new TServerSocket(9090);
                //TServer server = new TSimpleServer(processor, serverTransport);
                TServer server = new TThreadPoolServer(processor, serverTransport);

                Console.WriteLine("Iniciou");
                server.Serve();
            }
            catch (Exception x)
            {
                Console.WriteLine(x.StackTrace);
            }
            Console.WriteLine("Terminou");
        }
    }
}
