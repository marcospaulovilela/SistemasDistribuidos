using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Thrift.Transport;
using Thrift.Server;

namespace TheGraph.Server {
    class Program {
        static Tuple<Tuple<string, int, bool,int>, List<Tuple<string, List<int>>>>  CheckArgs(string[] args) {
            string msgErro = "Os parametros devem obedecer o seguinte formato: -ip:[ip] -p:[PORTA] -raft{<ip:[port1;port2;... ;portN>+} -node:[index] -inicial \nSendo o primeiro o ip atual que o servidor será iniciado. \nO segundo a porta do servidor\n O terceiro a lista de IP  e portas de cada servidor que serão usadas pelo RAFT. \nO ultimo opcional e sua presença indica que este é o primeiro CLUSTER RAFT do sistema, para todo servidor em um mesmo cluster deve ser informado igual.";
            string[] buffer;
            string ip;
            int port;

            try {
                if (args.Length == 4 || args.Length == 5) {
                    buffer = args[0].Split(':');
                    if (!buffer[0].Equals("-ip")) {
                        throw new Exception("1");
                    }
                    ip = buffer[1];

                    buffer = args[1].Split(':');
                    if (!buffer[0].Equals("-p") || !int.TryParse(buffer[1], out port)) {
                        throw new Exception("2");
                    }

                    var nodesAddress = new List<Tuple<string, List<int>>>();
                    buffer = args[2].Replace("\"", "").Split('{');
                    if (buffer[0] != "-raft")
                        throw new Exception("3");

                    buffer[1] = buffer[1].Replace("{", "").Replace("}", ""); //<192.168.0.1:[1010;1011;1012]>,  <192.168.0.2:[2020;3030;4040]>
                    foreach (var address in buffer[1].Split(',')) {
                        var node_ip = address.Split(':')[0].Replace("<", "");
                        var ports = address.Split(':')[1].Replace(">", ""); //1010;1011;1012
                        
                        nodesAddress.Add(new Tuple<string, List<int>>(node_ip, ports.Split(';').Select(p => int.Parse(p)).ToList()));
                    }

                    buffer = args[3].Split(':');
                    if (buffer[0] != "-node")
                        throw new Exception("4");
               
                    return new Tuple<Tuple<string, int, bool, int>, List<Tuple<string, List<int>>>>(new Tuple<string, int, bool, int>(ip, port, args.Length == 5 && args[4] == "-inicial", int.Parse(buffer[1])),  nodesAddress);
                }
                throw new Exception("5");
                
            } catch (Exception ex) {
                Console.WriteLine(msgErro);
                return null;
            }
        }

        static async void StartServers(Handler handler) {
            await Task.Run(() => {
                var processor = new TheGraph.Thrift.TheGraph.Processor(handler);
                TServerTransport serverTransport = new TServerSocket(handler.port);
                TServer server = new TThreadPoolServer(processor, serverTransport);

                Console.WriteLine("Thrift Iniciou");
                server.Serve();
            });
        }

        static void Main(string[] args) {
            try {
                if (args.Length == 0) {
                    args = new string[] { "-ip:127.0.0.1", "-p:9090", "\"-raft{<127.0.0.1:5050;5051>,<127.0.0.1:5150;5151>,<127.0.0.1:5250;5251>\"}", "-node:0", "-inicial" };
                    //args = new string[] { "-ip:127.0.0.1", "-p:9090", "\"-raft{<127.0.0.1:5050>,<127.0.0.1:5150>\"}","-node:0", "-inicial"}; 
                }

                var parameters = CheckArgs(args);
                if (parameters == null)
                    return;

                Handler handler = new TheGraph.Server.Handler(parameters.Item1.Item1, parameters.Item1.Item2, parameters.Item1.Item3, parameters.Item1.Item4, parameters.Item2);
                StartServers(handler);
                
                /**********/
                
                /**********/

                for (string buffer = Console.ReadLine(); ; buffer = Console.ReadLine()) {
                    if (buffer.Equals("CLOSE")) {
                        handler.DeadProtocol();
                        return;
                    } else {
                        //handler.SendMessage(buffer);
                        var response = handler.raftNode.AppendEntry(buffer);
                        if (response == null) {
                            Console.WriteLine("APENAS LIDERES PODEM COMITAR");
                            continue;
                        }
                        while (!response.comited) ;
                        Console.WriteLine("ENTRADA ACEITA");
                    }
                }

            } catch (Exception x) {
                Console.WriteLine(x.StackTrace);
            }
            Console.WriteLine("Terminou");
        }
    }
}