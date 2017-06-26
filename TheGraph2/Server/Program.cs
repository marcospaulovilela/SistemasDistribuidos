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
        static Tuple<int, bool> CheckArgs(string[] args){
            string[] buffer;
            int port;
            
            try
            {
                string msgErro = "Os parametros devem obedecer o seguinte formato: -p:[PORTA] -inicial\nSendo o primeiro a porta do servidor\nO ultimo opcional e sua presença indica que este é o primeiro servidor do sistema.";
                if (args.Length == 1 || args.Length == 2)
                {
                    buffer = args[0].Split(':');
                    if (!buffer[0].Equals("-p") || 
                        !int.TryParse(buffer[1], out port) || 
                        (args.Length == 2 && !args[1].Equals("-inicial"))){
                        throw new Exception(msgErro);
                    }
                    return Tuple.Create<int, bool>(port, (args.Length == 2 && args[1].Equals("-inicial")));
                }
                throw new Exception(msgErro);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        static async void StartServers(Handler handler)
        {
            await Task.Run(() => {
                var processor = new TheGraph.Thrift.TheGraph.Processor(handler);
                TServerTransport serverTransport = new TServerSocket(handler.port);
                TServer server = new TThreadPoolServer(processor, serverTransport);

                Console.WriteLine("Iniciou");
                server.Serve();
            });
        }

        static void Main(string[] args)
        {   
            try
            {
                if(args.Length == 0)
                    args = new string[] { "-p:9091", "-inicial" };

                Tuple<int, bool> parameters = CheckArgs(args);
                if (parameters == null)
                    return;

                Handler handler = new TheGraph.Server.Handler(parameters.Item1);
                handler.StartServersControl(parameters.Item2);
                StartServers(handler);

                for (string buffer = Console.ReadLine(); ; buffer = Console.ReadLine()){
                    if (buffer.Equals("CLOSE")){
                        handler.DeadProtocol();
                        return;
                    }
                }

            }
            catch (Exception x)
            {
                Console.WriteLine(x.StackTrace);
            }
            Console.WriteLine("Terminou");
        }
    }
}
