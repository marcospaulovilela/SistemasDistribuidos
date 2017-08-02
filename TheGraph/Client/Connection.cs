using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Thrift;
using Thrift.Protocol;
using Thrift.Server;
using Thrift.Transport;

namespace TheGraph.Client {
    public class Connection {
        private TheGraph.Thrift.graph graph;

        private TTransport transport;
        private TProtocol protocol;
        private TheGraph.Thrift.TheGraph.Client client;
        
        public void Connect(string ip, int port) {
            if (this.transport != null && this.transport.IsOpen)
                this.transport.Close();

            this.transport = new TSocket(ip, port);
            this.protocol = new TBinaryProtocol(transport);
            this.client = new Thrift.TheGraph.Client(this.protocol);

            transport.Open();
        }

        public bool AddV(int ID, int Color, double Weight, string Description) {
            var result = client.createVertex(new Thrift.vertex() {
                Name = ID,
                Color = Color,
                Weight = Weight,
                Description = Description
            });

            return result;
        }

        public bool RemoveV(int ID) {
            var result = client.deleteVertex(new Thrift.vertex() { Name = ID });
            return result;
        }

        public TheGraph.Thrift.vertex getV(int ID) {
            return client.readV(ID);
        }

        public List<TheGraph.Thrift.vertex> getV(int V1, int V2, bool directed) {
            return client.getVertex(new Thrift.edge() {
                V1 = V1,
                V2 = V2,
                Directed = directed
            });
        }

        public List<TheGraph.Thrift.edge> getAdjacentes(int ID) {
            return client.getEdges(new Thrift.vertex() { Name = ID });
        }

        public List<TheGraph.Thrift.vertex> getNeighborhood(int ID) {
            return client.getNeighborhood(new Thrift.vertex() {
                Name = ID
            });
        }
        
        public bool AddE(int V1, int V2, double p, bool directed, string description) {
            try {
                return client.createEdge(new Thrift.edge() {
                    V1 = V1,
                    V2 = V2,
                    Weight = p,
                    Directed = directed,
                    Description = description
                });
            } catch (Exception exception) {
                MessageBox.Show(exception.ToString());
                return false;
            }
        }

        public bool RemoveE(int V1, int V2, bool directed) {
            return client.deleteEdge(new Thrift.edge() {
                V1 = V1,
                V2 = V2,
                Directed = directed
            });
        }

        public TheGraph.Thrift.edge getE(int V1, int V2, bool directed) {
            return client.readE(V1, V2, directed);
        }

        public List<int> bfs(int V1, int V2) {
            try {
                return client.bfs(V2, new List<List<int>>() { new List<int>() { V1 } }, new List<int>());
            } catch (Exception ex) {
                return new List<int>();
                MessageBox.Show("NÃO EXISTE CAMINHO\n");
            }

        }
    }

    public static class connectionServer {
        public static Connection c;
    }
}
