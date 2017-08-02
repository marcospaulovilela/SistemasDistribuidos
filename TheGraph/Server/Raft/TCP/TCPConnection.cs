using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Raft.TCP {
    class TCPConnection {
        TcpClient connection;
        NetworkStream stream;
        public IPEndPoint IPLocal, IPRemote;

        public TCPConnection(Tuple<string, int> local, Tuple<string, int> remote) {
            this.IPLocal = new IPEndPoint(IPAddress.Parse(local.Item1), local.Item2);
            this.IPRemote = new IPEndPoint(IPAddress.Parse(remote.Item1), remote.Item2);
            this.connection = new TcpClient(this.IPLocal);
        }

        private void Connect() {
            if (!this.connection.Connected) {
                this.connection.Connect(this.IPRemote);
                this.stream = this.connection.GetStream();
            }
        }

        private void Disconnect() {
            this.connection.Close();
            this.connection = new TcpClient(this.IPLocal);
        }

        public void SendMessage(string message) {
            try {
                message += '\0';

                this.Connect();

                byte[] buffer = ASCIIEncoding.ASCII.GetBytes(message);
                this.stream.Write(buffer, 0, buffer.Length);
            } catch (Exception e) {
                if (e.Message.Contains("Após a desconexão do soquete")) {
                    this.Disconnect();
                };
            }
        }

        public string ReceiveMessage() {
            try {
                this.Connect();

                byte[] buffer = new byte[1024];
                this.stream.Read(buffer, 0, buffer.Length);

                return ASCIIEncoding.ASCII.GetString(buffer).Trim();

            } catch (Exception e) {
                if (e.Message.Contains("Após a desconexão do soquete")) {
                    this.Disconnect();
                };
                return null;
            }
        }
    }
}
