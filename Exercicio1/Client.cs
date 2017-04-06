using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Exercicio1
{
    class Client
    {
        private IPAddress ip { get; set; }
        private int port { get; set; }
        private IPEndPoint serverPoint { get; set; }
        private Socket SocketClient { get; set; }

        public Client(string ip, int port)
        {
            this.port = port;
            this.ip = IPAddress.Parse(ip);
            this.serverPoint = new IPEndPoint(this.ip, this.port);
            this.SocketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start()
        {
            try
            {
                this.SocketClient.Connect(this.serverPoint);
                Console.WriteLine(string.Format("Conectado com {0}", this.SocketClient.RemoteEndPoint.ToString()));

                while (true)
                {
                    string str;
                    byte[] buffer;

                    #region EnviaMensagem
                    Console.Write("Mensagem a ser enviada: ");
                    str = Console.ReadLine();
                    buffer = Encoding.ASCII.GetBytes(str);
                    this.SocketClient.Send(buffer);
                    #endregion

                    #region EncerraConexao
                    if (str.Equals("poweroff")) //Desconeta e fecha o socket temporario criado.
                    {
                        Console.WriteLine(string.Format("Encerrando conexão com {0}", this.SocketClient.RemoteEndPoint.ToString()));
                        this.Close();
                        break;
                    }
                    #endregion

                    #region RecebeResposta
                    buffer = new byte[1024];
                    int nBytesReceived = this.SocketClient.Receive(buffer);
                    str = Encoding.ASCII.GetString(buffer, 0, nBytesReceived);
                    Console.WriteLine(string.Format("Resposta recebida: {0}", str));
                    #endregion

                }

            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void Close()
        {
            if (this.SocketClient.IsBound)
            {
                this.SocketClient.Disconnect(false);
                this.SocketClient.Shutdown(SocketShutdown.Both);
                this.SocketClient.Close();
            }
        }
    }
}
