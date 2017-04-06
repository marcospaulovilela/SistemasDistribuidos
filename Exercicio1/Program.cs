using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exercicio1
{
    class Program
    {
        static void InvalidArgs()
        {
            Console.WriteLine("parametros de inicializacao invalidos.");
            Console.WriteLine("Utilize -s [porta] para inicializar um servidor localhost na porta [porta]");
            Console.WriteLine("Utilize -c [ip]:[porta] para inicializar um cliente com alvo [ip]:[porta]");
        }

        static void Main(string[] args)
        {
            //args = new string[] { "-c", "127.0.0.1:12345" };
            if (args.Length != 2)
                InvalidArgs();

            else if (args[0].Equals("-s"))
            {
                int port;
                if (!int.TryParse(args[1], out port))
                {
                    InvalidArgs();
                    return;
                }

                Server servidor = new Server(port);
                servidor.Start(10);
                servidor.Close();
            }
            else if (args[0].Equals("-c"))
            {
                string ip;
                int port;

                if (args[1].IndexOf(':') == -1 || 
                    args[1].Split(':').Length != 2 ||
                    !int.TryParse(args[1].Split(':')[1], out port))
                {
                    InvalidArgs();
                    return;
                }
                ip = args[1].Split(':')[0];

                Client cliente = new Client(ip, port);
                cliente.Start();
                cliente.Close();

            }
            else
                InvalidArgs();


        }
    }
}
