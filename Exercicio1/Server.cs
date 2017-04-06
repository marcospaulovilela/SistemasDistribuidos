using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Exercicio1
{
    class Server
    {
        private IPAddress ip { get; set; }
        private int port { get; set; }
        private IPEndPoint serverPoint { get; set; }
        private Socket SocketServer { get; set; }

        public Server(int port)
        {
            this.port = port;
            this.ip = IPAddress.Parse("127.0.0.1");

            this.serverPoint = new IPEndPoint(this.ip, this.port);
            this.SocketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start(int MaxConnections)
        {
            this.SocketServer.Bind(this.serverPoint);
            this.SocketServer.Listen(MaxConnections);
            while (this.SocketServer.IsBound)
            {
                string str;
                byte[] buffer;

                Console.WriteLine("Esperando solicitação de conexão");
                Socket handler = this.SocketServer.Accept();  //Espera por uma requisição

                Console.WriteLine(string.Format("Conectado com {0}", handler.RemoteEndPoint.ToString()));
                while (handler.Connected)
                {
                    #region RecebeMensagem
                    buffer = new byte[1024];                      //Recebe a requisicao em byte array, e decodifica numa string.
                    int nBytesReceived = handler.Receive(buffer);
                    str = Encoding.ASCII.GetString(buffer, 0, nBytesReceived);
                    Console.WriteLine(string.Format("Mensagem recebida: {0}", str));
                    #endregion

                    #region EncerraConexao
                    if (str.Equals("poweroff")) //Desconeta e fecha o socket temporario criado.
                    {
                        Console.WriteLine(string.Format("Encerrando conexão com {0}", handler.RemoteEndPoint.ToString()));
                        handler.Disconnect(false);
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();

                        break;
                    }
                    #endregion

                    #region EnviaResposta
                    Console.Write("Resposta a ser enviada: ");
                    str = Console.ReadLine();
                    buffer = Encoding.ASCII.GetBytes(str);
                    handler.Send(buffer);
                    #endregion
                }              
            }
        }

        public void Close()
        {
            if (this.SocketServer.IsBound)
            {
                this.SocketServer.Shutdown(SocketShutdown.Both);
                this.SocketServer.Close();
            }
        }

    }
}
