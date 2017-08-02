using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TheGraph.Thrift;

using Thrift.Transport;
using Thrift.Protocol;
using System.Threading.Tasks;

namespace TheGraph.Server {
    class Handler : TheGraph.Thrift.TheGraph.Iface, Raft.IServer {
        private const int LIMIT_BITS_MD5 = 5; //COMO O HASH MD5 POSSUI 128BITS É UM NUMERO EXTREMAMENTE GRANDE COM UM RANGE DE IPS EXTRAMAMENTE GRANDE
                                              //NO CENARIO DE TESTES, POSSUIMOS POUCOS SERVIDORES LOGO CADA UM CUIDA DE UM RANGE TAMBEM EXTREMAMENTE GRANDE 
                                              //E COMO UTILIZMAOS APENAS INTEIROS COMO ID, O HASH DE TODOS NODOS DO GRAFO É PERTO , LOGO TODOS OS NODOS TEM ALTA PROBABILIDADE DE CAIR NO MESMO SERIVDOR
                                              //PARA CONTORNAR ISSO NO AMBIENTE DE TESTE, UTILIZAMOS O HASH MD5 LIMITADO POR ESSA CONSTANTE. => HASH(ID) = HASH_MD5(ID) % (2^LIMIT_BITS_MD5).
                                              //SE FOR COLOCADO ESSA CONSTANTE COM VALOR 128, TEREMOS O RESULTADO DE UM CENARIO REAL SEM PRECISAR ALTERAR NADA EM CÓDIGO.


        private static BigInteger BigIntegerOfNBits(int N_Bits) {
            BigInteger value = 0;
            for (int i = 1; i <= N_Bits; i++) {
                value <<= 1;
                value += 1;
            }

            return value;
        }

        private static BigInteger HASH_MD5(string value) {
            using (var MD5Hash = System.Security.Cryptography.MD5.Create()) {
                var buffer = Encoding.ASCII.GetBytes(value);
                buffer = MD5Hash.ComputeHash(buffer);

                StringBuilder hex = new StringBuilder("0");
                for (int i = 0; i < buffer.Length; i++)
                    hex.Append(buffer[i].ToString("x2"));

                BigInteger result = BigInteger.Parse(hex.ToString(), System.Globalization.NumberStyles.HexNumber);
                Console.WriteLine("MD5({0}) = {1} = {2}", value, hex, result);

                result = result % BigIntegerOfNBits(LIMIT_BITS_MD5);
                return result;
            }
        }

        public string MyAddress { get; set; }
        public string ip { get; set; }
        public int port { get; set; }
        public bool first { get; set; }

        public string clusterId => MyAddress;

        private UdpClient UDPClient;
        private Dictionary<string, Tuple<int, int>> servers;
        
        private IPAddress MulticastAddress;
        private IPEndPoint SenderEndPoint, ReceiverEndPoint;

        private System.Threading.Mutex MUTEX_E; //MUTEX PARA CONTROLAR AS EGDES
        private System.Threading.Mutex MUTEX_V; //MUTEX PARA CONTROLAR OS VERTICES
        private System.Threading.Mutex MUTEX_S; //MUTEX PARA CONTROLAR OS SERVIDORES

        private graph g; //ESTRUTURA ONDE OS VERTICES E ARESTAS IRÃO SER MANTIDOS

        public Raft.Node raftNode;

        public Handler(string ip, int port, bool first, int serverIndex, List<Tuple<string, List<int>>> nodes) {
            #region VARIAVEIS DE CONTROLE DA COMUNICAÇÃO MULTICAST PARA CONTROLE DE SERVIDOR
            this.ip = ip;
            this.port = port;
            this.MyAddress = string.Format("{0}:{1}", ip, this.port);
            this.first = first;
            
            this.UDPClient = new System.Net.Sockets.UdpClient();

            this.MulticastAddress = IPAddress.Parse("224.5.6.7");
            this.SenderEndPoint = new IPEndPoint(MulticastAddress, 2222);
            this.ReceiverEndPoint = new IPEndPoint(IPAddress.Any, 2222);

            this.UDPClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); //PERMITE MULTIPLOS SERVIDORES RODAREM NO MESMO COMPUTADOR
            this.UDPClient.JoinMulticastGroup(MulticastAddress);
            this.UDPClient.Client.Bind(ReceiverEndPoint);
            #endregion

            #region VARIAVEL DE CONTROLE DO RAFT
            var nodeServer = nodes.Where(n => n.Item1 == ip);
            if (!nodeServer.Any())
                throw new Exception("CONFIGURAÇÃ DE NODOS DO RAFT INVALIDA, O SERVIDOR ATUAL NÃO ESTÁ LISTADO");

            if(nodeServer.Count() > 1) {
                var ports = nodeServer.SelectMany(node => node.Item2);
                var portsDistinct = ports.Distinct();

                if(ports.Count() != portsDistinct.Count())
                    throw new Exception("CONFIGURAÇÃ DE NODOS DO RAFT INVALIDA, UMA PORTA ESTÁ SENDO USADA MAIS DE UMA VEZ PARA UM MESMO SERVIDOR");

                if(ports.Any(p => p == this.port))
                    throw new Exception("CONFIGURAÇÃO DE NODOS DO RAFT INVALIDA A MESMA PORTA FOI DIRECIONADA TANTO PARA O RAFT QUANTO PARA O SERVIDOR THRIFT!");
            }


            this.raftNode = new Raft.Node(nodes, serverIndex, this);
            raftNode.run(); //COLOCA O RAFT PRA RODAR EM THREAD SEPARADA
            #endregion  

            //DICIONARIOS DO TIPO <IP:PORT> => BIT DE INICIO DO INTERVALO E BIT DE FIM. Ex: {127.0.0.1:8080, <0, 10>}
            //ONDE O SERVIDOR 127.0.0.1:8080, IRA RECEBER OS NODOS ONDE HASH(ID) >= 2^0  && HASH(ID) < 2^10
            this.servers = new Dictionary<string, Tuple<int, int>>();

            //INSTANCIA DO GRAFO, ESTRUTURA QUE IRA MANTER OS REGISTROS NA MEMORIA
            this.g = new graph();

            //MUTEX UTILIZADOS PARA CONTROLE DE CONCORRENCIA ENTRE AS MUITAS THREADS
            this.MUTEX_E = new System.Threading.Mutex();
            this.MUTEX_V = new System.Threading.Mutex();
            this.MUTEX_S = new System.Threading.Mutex();
        }

        #region IMPLEMENTAÇÃO DAS FUNÇÕES DO THRIFT
        public graph G(bool scan) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) { //CONSULTAS NÃO FAZEM COMITIS APENAS VALIDAM SE O NODO É O LIDER, APENAS UM LIDER PODE CONVERSAR COM O CLIENTE.
                if (!scan || this.servers.Count() == 1)
                    return this.g;

                graph G = new graph();
                foreach (string server in this.servers.Keys) {
                    TTransport transport = new TSocket(server.Split(':')[0], int.Parse(server.Split(':')[1]));
                    TProtocol protocol = new TBinaryProtocol(transport);
                    Thrift.TheGraph.Client client = new Thrift.TheGraph.Client(protocol);
                    transport.Open();

                    G.V.AddRange(client.G(false).V);
                    G.E.AddRange(client.G(false).E);

                    transport.Close();
                }

                return G;
            } else {
                Console.WriteLine("APENAS LIDERES PODEM CONSULTAR");
                return null;
            }


        }

        private void requestRaftServer(string message) {
            Console.WriteLine(message);
            var entry = this.raftNode.AppendEntry(message);
            while (!entry.comited);
        }

        private Thrift.TheGraph.Client getServerOf(string value) {
            var _MD5 = Handler.HASH_MD5(value);

            if (this.servers.Keys.Count() == 0)
                return null;

            foreach (string server in this.servers.Keys) {
                BigInteger start = Handler.BigIntegerOfNBits(this.servers[server].Item1);
                BigInteger end = Handler.BigIntegerOfNBits(this.servers[server].Item2);

                if (_MD5 >= start && _MD5 < end) {
                    if (server.Equals(this.MyAddress))
                        return null;

                    Console.WriteLine(String.Format("Solicitacao redirecionada ao servidor {0}", server));

                    TSocket transport = new TSocket(server.Split(':')[0], int.Parse(server.Split(':')[1]));
                    TBinaryProtocol protocol = new TBinaryProtocol(transport);
                    Thrift.TheGraph.Client client = new Thrift.TheGraph.Client(protocol);
                    transport.Open();

                    return client;
                }
            }

            throw new Exception("Hash fora do range de servidores, O banco de dados foi comprometido.");
        }

        public bool copyVertex(vertex V) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                MUTEX_V.WaitOne();

                this.requestRaftServer(string.Format("createVertex({0}, {1}, {2}, {3});", V.Name, V.Color, V.Description, V.Weight));
               
                this.g.V.Add(V);
                MUTEX_V.ReleaseMutex();

                return true;
            }
            return false;
        }

        public bool createVertex(vertex V) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao de criacao do vertice {0}", V.Name));
                Console.WriteLine(string.Join("--", this.servers.Keys));
                using (var client = getServerOf(V.Name.ToString())) {
                    if (client != null) {
                        return client.createVertex(V);
                    }
                }

                MUTEX_V.WaitOne();
                if (this.g.V.Exists(v => v.Name == V.Name))
                    throw new Thrift.VertexAlreadyExists();

                this.requestRaftServer(string.Format("createVertex({0}, {1}, {2}, {3});", V.Name, V.Color, V.Description, V.Weight));
               
                this.g.V.Add(V);
                bool result = this.g.V.Exists(v => v.Name == V.Name);

                MUTEX_V.ReleaseMutex();
                return result;
            }

            return false;
        }

        public bool deleteVertex(vertex V) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao de exclusao do vertice {0}", V.Name));
                using (var client = getServerOf(V.Name.ToString())) {
                    if (client != null)
                        return client.deleteVertex(V);
                }

                MUTEX_V.WaitOne();
                if (!this.g.V.Exists(v => v.Name == V.Name)) {
                    MUTEX_V.ReleaseMutex();
                    throw new Thrift.VertexDontExists();
                }

                this.requestRaftServer(string.Format("deleteVertex({0});", V.Name));
                

                this.g.V.RemoveAll(v => v.Name == V.Name);
                bool result = this.g.E.RemoveAll(edge => edge.V1 == V.Name || edge.V2 == V.Name) == 1;

                MUTEX_V.ReleaseMutex();
                return result;
            }
            return false;
        }

        public bool copyEdge(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {

                MUTEX_E.WaitOne();
                this.requestRaftServer(string.Format("createEdge({0}, {1}, {2}, {3}, {4});", E.V1, E.V2, E.Directed, E.Description, E.Weight));
                

                this.g.E.Add(E);
                MUTEX_E.ReleaseMutex();

                return true;
            }
            return false;
        }

        public bool createDuplicatedEdge(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                MUTEX_E.WaitOne();

                edge E_aux = new edge() {
                    V1 = E.V2,
                    V2 = E.V1,
                    Weight = E.Weight,
                    Directed = E.Directed,
                    Description = E.Description
                };

                this.requestRaftServer(string.Format("createEdge({0}, {1}, {2}, {3}, {4});", E.V2, E.V1, E.Directed, E.Description, E.Weight));
                

                this.g.E.Add(E_aux);
                bool result = this.g.E.Exists(e => e.V1 == E_aux.V1 && e.V2 == E_aux.V2);
                MUTEX_E.ReleaseMutex();

                return result;
            }
            return false;
        }

        public bool createEdge(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao de criacao da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));
                using (var client = getServerOf(E.V1.ToString())) {
                    if (client != null)
                        return client.createEdge(E);
                }

                MUTEX_V.WaitOne();
                MUTEX_E.WaitOne();

                if (!this.g.V.Contains(new vertex() { Name = E.V1 })) {
                    MUTEX_V.ReleaseMutex();
                    MUTEX_E.ReleaseMutex();
                    throw new Thrift.VertexDontExists();
                }

                MUTEX_V.ReleaseMutex();

                if (this.g.E.Contains(E)) {
                    MUTEX_E.ReleaseMutex();
                    throw new Thrift.EdgeAlreadyExists();
                }

                this.requestRaftServer(string.Format("createEdge({0}, {1}, {2}, {3}, {4});", E.V1, E.V2, E.Directed, E.Description, E.Weight));
                

                this.g.E.Add(E);
                bool result = this.g.E.Contains(E);
                MUTEX_E.ReleaseMutex();

                if (result && !E.Directed) {
                    using (var client = getServerOf(E.V2.ToString())) {
                        if (client != null)
                            return client.createDuplicatedEdge(E);
                        else
                            return this.createDuplicatedEdge(E);
                    }
                }

                return result;
            }
            return false;
        }

        public bool deleteDuplicatedEdge(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                MUTEX_E.WaitOne();

                edge E_aux = new edge() {
                    V1 = E.V2,
                    V2 = E.V1,
                    Weight = E.Weight,
                    Directed = E.Directed,
                    Description = E.Description
                };

                this.requestRaftServer(string.Format("deleteEdge({0}, {1}, {2}, {3}, {4});", E.V2, E.V1, E.Directed, E.Description, E.Weight));
                

                bool result = this.g.E.RemoveAll(e => e.Directed == E_aux.Directed && e.V1 == E_aux.V1 && e.V2 == E_aux.V2) == 1;

                MUTEX_E.ReleaseMutex();
                return result;
            }
            return false;
        }

        public bool deleteEdge(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao de exclusao da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));
                using (var client = getServerOf(E.V1.ToString())) {
                    if (client != null)
                        return client.deleteEdge(E);
                }

                MUTEX_E.WaitOne();
                if (!this.g.E.Contains(E)) {
                    MUTEX_E.ReleaseMutex();
                    throw new Thrift.EdgeDontExists();
                }

                this.requestRaftServer(string.Format("deleteEdge({0}, {1}, {2}, {3}, {4});", E.V1, E.V2, E.Directed, E.Description, E.Weight));
                

                bool result = this.g.E.RemoveAll(e => e.Directed == E.Directed && e.V1 == E.V1 && e.V2 == E.V2) == 1;
                MUTEX_E.ReleaseMutex();

                if (result && !E.Directed) {
                    using (var client = getServerOf(E.V2.ToString())) {
                        if (client != null)
                            return client.deleteDuplicatedEdge(E);
                        else
                            return this.deleteDuplicatedEdge(E);
                    }
                }
                return result;
            }
            return false;
        }

        public vertex readV(int VertexName) {
            Console.WriteLine(String.Format("Solitacao de leitura do vertice {0}", VertexName));
            using (var client = getServerOf(VertexName.ToString())) {
                if (client != null)
                    return client.readV(VertexName);
            }

            MUTEX_V.WaitOne();

            var result = this.g.V.Where(v => v.Name == VertexName);
            if (result.Any()) {
                MUTEX_V.ReleaseMutex();
                return result.First();
            }

            MUTEX_V.ReleaseMutex();
            throw new VertexDontExists();
        }

        public edge readE(int Vertex1_Name, int Vertex2_Name, bool directed) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao de leitura da aresta {0}-{1}-{2}", Vertex1_Name, Vertex2_Name, directed));
                using (var client = getServerOf(Vertex1_Name.ToString())) {
                    if (client != null)
                        return client.readE(Vertex1_Name, Vertex2_Name, directed);
                }

                MUTEX_E.WaitOne();

                var result = this.g.E.Where(e => e.V1 == Vertex1_Name && e.V2 == Vertex2_Name && e.Directed == directed);
                if (result.Any()) {
                    MUTEX_V.ReleaseMutex();
                    return result.First();
                }

                MUTEX_E.ReleaseMutex();
                throw new EdgeDontExists();
            }
            return null;
        }

        public bool updateVertex(vertex V) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                
                Console.WriteLine(String.Format("Solitacao de atualização do vertice {0}", V.Name));
                using (var client = getServerOf(V.Name.ToString())) {
                    if (client != null)
                        return client.updateVertex(V);
                }

                MUTEX_V.WaitOne();
                foreach (vertex v in this.g.V) {
                    if (v.Equals(V)) {

                        this.requestRaftServer(string.Format("updateVertex({0}, {1}, {2}, {3});", V.Name, V.Color, V.Description, V.Weight));
                        

                        v.Color = V.Color;
                        v.Description = V.Description;
                        v.Weight = V.Weight;

                        MUTEX_V.ReleaseMutex();
                        return true;
                    }
                }

                MUTEX_V.ReleaseMutex();
                throw new VertexDontExists();
            }

            return false;
        }

        public bool updateDuplicatedEdge(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                
                edge E_aux = new edge() {
                    V1 = E.V2,
                    V2 = E.V1,
                    Weight = E.Weight,
                    Directed = E.Directed,
                    Description = E.Description
                };

                MUTEX_E.WaitOne();
                foreach (edge e in this.g.E) {
                    if (e.V1 == E_aux.V1 && e.V2 == E_aux.V2 && e.Directed == E_aux.Directed) {
                        this.requestRaftServer(string.Format("updateEdge({0}, {1}, {2}, {3}, {4});", E.V2, E.V1, E.Directed, E.Description, E.Weight));
                        
                        e.Weight = E.Weight;
                        e.Description = E.Description;

                        MUTEX_E.ReleaseMutex();
                        return true;
                    }
                }

                MUTEX_E.ReleaseMutex();
                throw new EdgeDontExists();
            }
            return false;
        }

        public bool updateEdge(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao de atualização da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));
                using (var client = getServerOf(E.V1.ToString())) {
                    if (client != null)
                        return client.updateEdge(E);
                }

                MUTEX_E.WaitOne();
                foreach (edge e in this.g.E) {
                    if (e.V1 == E.V1 && e.V2 == E.V2 && e.Directed == E.Directed) {
                        this.requestRaftServer(string.Format("updateEdge({0}, {1}, {2}, {3}, {4});", E.V1, E.V2, E.Directed, E.Description, E.Weight));
                       
                        e.Weight = E.Weight;
                        e.Description = E.Description;

                        MUTEX_E.ReleaseMutex();

                        if (!E.Directed) {
                            using (var client = getServerOf(E.V2.ToString())) {
                                if (client != null)
                                    return client.updateDuplicatedEdge(E);
                                else
                                    return this.updateDuplicatedEdge(E);
                            }
                        }
                    }
                }

                MUTEX_E.ReleaseMutex();
                throw new EdgeDontExists();
            }
            return false;
        }

        public List<vertex> getVertex(edge E) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao dos vertices da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));
                using (var client = getServerOf(E.V1.ToString())) {
                    if (client != null)
                        return client.getVertex(E);
                }

                try {
                    readE(E.V1, E.V2, E.Directed);

                    var V = new List<vertex>();
                    V.Add(readV(E.V1));
                    V.Add(readV(E.V2));

                    return V;
                } catch (Exception e) {
                    throw e;
                }
            }
            return null;
        }

        public List<edge> getEdges(vertex V) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao das arestas do vertice {0}", V.Name));
                using (var client = getServerOf(V.Name.ToString())) {
                    if (client != null)
                        return client.getEdges(V);
                }
                try {
                    readV(V.Name);

                    MUTEX_E.WaitOne();
                    var E = this.g.E.Where(edge => edge.V1 == V.Name).ToList();

                    MUTEX_E.ReleaseMutex();
                    return E;
                } catch (Exception e) {
                    throw e;
                }
            }
            return null;
        }

        public List<vertex> getNeighborhood(vertex V) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                Console.WriteLine(String.Format("Solitacao da vizinhanca do vertice {0}", V.Name));
                using (var client = getServerOf(V.Name.ToString())) {
                    if (client != null)
                        return client.getNeighborhood(V);
                }

                try {
                    var edges = this.getEdges(V).ToList();
                    if (!edges.Any())
                        return new List<vertex>();

                    var neighborhood = new List<vertex>();
                    foreach (edge e in edges)
                        neighborhood.Add(this.readV(e.V2));

                    return neighborhood;
                } catch (Exception e) {
                    throw e;
                }
            }
            return null;
        }

        public List<int> bfs(int end, List<List<int>> open, List<int> visited) {
            if (this.raftNode.current_state == Raft.Node.State.Leader) {
                while (open.Any()) {
                    var current = open.First();
                    int currentNode = current.Last();

                    using (var client = getServerOf(currentNode.ToString())) {
                        if (client != null)
                            return client.bfs(end, open, visited);
                    }

                    open.RemoveAt(0);
                    if (currentNode == end)
                        return current;

                    if (!visited.Contains(currentNode))
                        visited.Add(currentNode);

                    var next = this.getEdges(new vertex() { Name = currentNode }).Where(e => e.V1 == currentNode).Select(e => e.V2).Distinct().ToList();
                    next.RemoveAll(i => visited.Contains(i));

                    foreach (int i in next) {
                        var newWay = current.ToList();
                        newWay.Add(i);

                        open.Add(newWay);
                    }
                }
            }

            return null;
        }
        #endregion

        #region MULTICAST CONTROL - CONTROLE DE SERVIDORES ATIVOS, E LISTA DE RANGE DOS SERVIDORES
        public void SendMessage(string message) {
            byte[] buffer = Encoding.ASCII.GetBytes(message);
            UDPClient.Send(buffer, buffer.Length, SenderEndPoint);
        }

        private string ReceiveMessage() {
            return Encoding.ASCII.GetString(UDPClient.Receive(ref this.ReceiverEndPoint));
        }

        private void ClearSocketReceiver() {
            for (; UDPClient.Available > 0; UDPClient.Receive(ref this.ReceiverEndPoint));
        }

        /*PROTOCOLO DE INICIALIZAÇÃO, RESPONSAVEL POR FAZER OS OUTROS SERVIDORES VEREM UM NOVO
          O PROCOLO FOI DESENHADO PARA O CASO SIMPLES, ONDE APENAS UM SERVIDOR "NASCE POR VEZ" */
        private void BornProtocol() {
            Console.WriteLine("Enviando sinal 'BORN'");
            this.SendMessage(string.Format("BORN|{0}", MyAddress)); //ENVIA A MENSAGEM NO CANAL MULTICAST INDICANDO QUE O SERVER ATUAL "NASCEU"

            /*APOS ENVIAR A MENSAGEM DE NASCIMENTO ESPERA A RESPOSTA DO PRIMEIRO SERVIDOR ESSE
             *IRA UMA LISTA COM TODOS OS SERVIDORES E SEUS RANGES */
            string buffer = this.ReceiveMessage();
            for (; !buffer.Contains("SERVERS|"); buffer = this.ReceiveMessage());

            Console.Write("RECEBIDA LISTA DE SERVIDORES ATIVOS: ");
            
            var dict = buffer.Replace("SERVERS|", "").Split('|');
            this.servers = new Dictionary<string, Tuple<int, int>>();
            for(int i = 0; i < (dict.Length - 1) && dict.Any(); i++) {
                var server = dict[i];

                var address = server.Remove(server.IndexOf('<'));
                var start = server.Substring(server.IndexOf('<')).Replace("<", "").Replace(">", "").Split(',')[0];
                var end = server.Substring(server.IndexOf('<')).Replace("<", "").Replace(">", "").Split(',')[1];

                Console.Write(string.Format("[{0} <{1}, {2}>]", address, start, end));
                this.servers.Add(address, Tuple.Create<int, int>(int.Parse(start), int.Parse(end)));
            }
            Console.WriteLine("[--]");
            Console.Write("\n");
            Console.WriteLine("[--]1");
            
            Console.WriteLine("[--]2");
            string randomServer = this.servers.Keys.ElementAt(new Random().Next(dict.Length));
            Console.WriteLine("[--]3");
            int BitRange = this.servers[randomServer].Item2 - this.servers[randomServer].Item1 + 1;
            Console.WriteLine("[--]4");
            BitRange /= 2;
            Console.WriteLine("[--]5");
            this.servers.Add(MyAddress, Tuple.Create<int, int>(this.servers[randomServer].Item1 + BitRange, this.servers[randomServer].Item2));
            this.servers[randomServer] = Tuple.Create<int, int>(this.servers[randomServer].Item1, this.servers[randomServer].Item1 + BitRange);

            Console.WriteLine(string.Format("Escolhido servidor {0} para sincronização", randomServer));

            string message = string.Format("SYNC_TO|{0}|FROM|{1}|NEW_RANGE|<{2}, {3}>|MY_RANGE|<{4}, {5}>",
                randomServer,
                MyAddress,
                this.servers[randomServer].Item1,
                this.servers[randomServer].Item2,
                this.servers[MyAddress].Item1,
                this.servers[MyAddress].Item2);

            ClearSocketReceiver();
            this.SendMessage(message);
        }

        private void SyncProtocol(string TargetAddress, Tuple<int, int> MyRange, Tuple<int, int> TargetRange) {
            this.MUTEX_V.WaitOne();
            this.MUTEX_E.WaitOne();
            this.MUTEX_S.WaitOne();

            TTransport transport = new TSocket(TargetAddress.Split(':')[0], int.Parse(TargetAddress.Split(':')[1]));
            TProtocol protocol = new TBinaryProtocol(transport);
            Thrift.TheGraph.Client client = new Thrift.TheGraph.Client(protocol);
            transport.Open();

            BigInteger floor = Handler.BigIntegerOfNBits(MyRange.Item2); //MAIOR NUMERO POSSIVEL NO RANGE ATUAL, ACIMA DISSO DEVE IR PARA O NOVO SERVER

            int stop = this.g.V.Count();
            for (int i = 0; i < stop; i++) {
                var v = this.g.V[i];

                if (Handler.HASH_MD5(v.Name.ToString()) > floor) {
                    Console.WriteLine(string.Format("Enviando vertice {0} e suas arestas para o servidor {1}", v.Name, TargetAddress));

                    client.copyVertex(v);
                    foreach (var e in this.g.E.Where(edge => edge.V1 == v.Name || (!edge.Directed && edge.V2 == v.Name)))
                        client.copyEdge(e);

                    this.g.E.RemoveAll(edge => edge.V1 == v.Name || (!edge.Directed && edge.V2 == v.Name));

                    this.g.V.RemoveAt(i);
                    stop--;
                    i--;
                }
            }

            this.servers[MyAddress] = MyRange;
            this.servers.Add(TargetAddress, TargetRange);

            string message = string.Format("REFRESH|{0}|<{1}, {2}>", MyAddress, MyRange.Item1, MyRange.Item2);
            this.SendMessage(message);

            message = string.Format("REFRESH|{0}|<{1}, {2}>", TargetAddress, TargetRange.Item1, TargetRange.Item2);
            this.SendMessage(message);

            this.MUTEX_V.ReleaseMutex();
            this.MUTEX_E.ReleaseMutex();
            this.MUTEX_S.ReleaseMutex();
        }

        private void RefleshProtocol(string TargetAddress, Tuple<int, int> NewRange) {
            this.MUTEX_V.WaitOne();
            this.MUTEX_E.WaitOne();
            this.MUTEX_S.WaitOne();

            if (NewRange.Item1 != -1 && NewRange.Item2 != -1) {

                var msg = String.Format("REFRESH({0}, {1}, {2}, {3});", TargetAddress, NewRange.Item1, NewRange.Item2, this.MyAddress);
                this.requestRaftServer(msg);

                if (this.servers.ContainsKey(TargetAddress)) {
                    this.servers[TargetAddress] = NewRange;
                } else {
                    this.servers.Add(TargetAddress, NewRange);
                }

                Console.WriteLine(msg);
            } else {
                Console.WriteLine(string.Format("Servidor {0} removido", TargetAddress));
                this.servers.Remove(TargetAddress);
            }

            this.MUTEX_V.ReleaseMutex();
            this.MUTEX_E.ReleaseMutex();
            this.MUTEX_S.ReleaseMutex();
        }

        public void DeadProtocol() {
            this.MUTEX_V.WaitOne();
            this.MUTEX_E.WaitOne();
            this.MUTEX_S.WaitOne();

            var MyRange = this.servers[MyAddress];

            var TargetAddress = "";
            Tuple<int, int> newRange = Tuple.Create<int, int>(-1, -1);

            foreach (string server in this.servers.Keys) {
                if (MyRange.Item2 != LIMIT_BITS_MD5) { //RIGHT SHIFT
                    if (this.servers[server].Item1 == MyRange.Item2 + 1) {
                        TargetAddress = server;
                        newRange = Tuple.Create<int, int>(MyRange.Item1, this.servers[server].Item2);
                        break;
                    }
                } else { //LEFT SHIFT
                    if (this.servers[server].Item2 == MyRange.Item1 - 1) {
                        TargetAddress = server;
                        newRange = Tuple.Create<int, int>(this.servers[server].Item1, LIMIT_BITS_MD5);
                        break;
                    }

                }
            }

            if (TargetAddress == string.Empty) {
                Console.WriteLine("Não existe outro servidor disponivel, os dados deste serão perdidos");
                return;
            } else {
                Console.WriteLine(string.Format("Novo servidor responsavel: {0}", TargetAddress));
            }

            string buffer = string.Format("DEAD|{0}|{1}|<{2}, {3}>", TargetAddress, MyAddress, newRange.Item1, newRange.Item2);
            SendMessage(buffer);

            for (; !buffer.Contains("DEAD_OK|") && buffer.Split('|')[1].Equals(MyAddress); buffer = this.ReceiveMessage());

            TTransport transport = new TSocket(TargetAddress.Split(':')[0], int.Parse(TargetAddress.Split(':')[1]));
            TProtocol protocol = new TBinaryProtocol(transport);
            Thrift.TheGraph.Client client = new Thrift.TheGraph.Client(protocol);
            transport.Open();

            foreach (var v in this.g.V) {
                Console.WriteLine(string.Format("Enviando vertice {0} para o servidor {1}", v.Name, TargetAddress));
                client.copyVertex(v);
            }

            foreach (var e in this.g.E) {
                Console.WriteLine(string.Format("Enviando aresta {0}-{1}-{2} para o servidor {3}", e.V1, e.V2, e.Directed, TargetAddress));
                client.copyEdge(e);
            }

            buffer = string.Format("REFRESH|{0}|<{1}, {2}>", TargetAddress, newRange.Item1, newRange.Item2);
            this.SendMessage(buffer);
            buffer = string.Format("REFRESH|{0}|<{1}, {2}>", MyAddress, -1, -1);
            this.SendMessage(buffer);
        }


        public void StartListennerMulticast() {
            var thread = new Thread(new ThreadStart(this.ListennerMulticast));
            thread.Start();
        }

        private void ListennerMulticast() {
            for (string str = this.ReceiveMessage(); ; str = this.ReceiveMessage()) {
                if (raftNode.current_state != Raft.Node.State.Leader)
                    continue;

                Console.WriteLine(str);
                if (str.Contains("BORN|")) {
                    MUTEX_S.WaitOne();
                    if (str.Substring(5).Equals(this.MyAddress))
                        continue;

                    Console.WriteLine(string.Format("Servidor {0} ativado", str.Substring(5)));
                    str = "SERVERS|";
                    foreach (var address in this.servers.Keys) {
                        str += string.Format("{0}<{1},{2}>|", address, this.servers[address].Item1, this.servers[address].Item2);
                    }
                    str += 0;

                    MUTEX_S.ReleaseMutex();

                    Console.WriteLine(str);
                    this.SendMessage(str);

                } else if (str.Contains("DEAD|")) {
                    var parameters = str.Split('|');
                    if (parameters[1].Equals(MyAddress)) {
                        var NewRange = parameters[3].Replace("<", "").Replace(">", "").Split(',').Select(num => int.Parse(num)).ToArray();
                        this.servers[parameters[2]] = Tuple.Create<int, int>(-1, -1);
                        this.servers[MyAddress] = Tuple.Create<int, int>(NewRange[0], NewRange[1]);

                        Console.WriteLine(string.Format("Servidor {0} iniciando tranferencia", parameters[2]));

                        SendMessage(string.Format("DEAD_OK|{0}", parameters[2]));
                    }

                } else if (str.Contains("REFRESH|")) {
                    str = str.Substring(str.IndexOf("|") + 1);
                    var parameters = str.Split('|');

                    string TargetAddress = parameters[0];
                    var NewRange = parameters[1].Replace("<", "").Replace(">", "").Split(',').Select(num => int.Parse(num)).ToArray();

                    this.RefleshProtocol(TargetAddress, Tuple.Create<int, int>(NewRange[0], NewRange[1]));
                   

                } else if (str.Contains("ADD(")) {
                    var parameters = str.Replace("ADD(", "").Replace(");", "").Split(',');
                    var value = new Tuple<int, int>(int.Parse(parameters[1]), int.Parse(parameters[2]));

                    if (parameters[3].Equals(this.MyAddress))
                        continue;
                    
                    this.requestRaftServer(str);
                    this.servers.Add(parameters[0].Trim(), value);

                    Console.WriteLine("Adicionado o servidor {0} no range<{1}, {2}> via multicast", parameters[0].Trim(), value.Item1, value.Item2);
                    
                } else if (str.Contains("UPDATE(")) {
                    var parameters = str.Replace("UPDATE(", "").Replace(");", "").Split(',');
                    var oldServer = parameters[0];
                    var newServer = parameters[1];

                    if (parameters[2].Equals(this.MyAddress))
                        continue;

                    var value = this.servers[oldServer];
                    this.requestRaftServer(str);

                    Console.WriteLine("Atualizado o servidor {0} para {1} no mesmo range<{2}, {3}>", oldServer, newServer, value.Item1, value.Item2);
                    this.servers.Remove(oldServer);
                    this.servers.Add(newServer, value);

                } else if (str.Contains("SYNC_TO|")) {
                    str = str.Substring(str.IndexOf("|") + 1);
                    if (str.Remove(str.IndexOf("|")).Equals(MyAddress)) {
                        var parameters = str.Split('|');
                        string TargetAddress = parameters[2];
                        var NewRange = parameters[4].Replace("<", "").Replace(">", "").Split(',').Select(num => int.Parse(num)).ToArray();
                        var MyRange = parameters[6].Replace("<", "").Replace(">", "").Split(',').Select(num => int.Parse(num)).ToArray();

                        Console.WriteLine(string.Format("Iniciando sincronização com {0}", TargetAddress));
                        this.SyncProtocol(TargetAddress, Tuple.Create<int, int>(NewRange[0], NewRange[1]), Tuple.Create<int, int>(MyRange[0], MyRange[1]));
                    }
                }
            }
        }
        #endregion


        #region RAFT_COMMIT
        public bool Commit(string message) {
            if (message.Contains("newLeader(")) { //ESSE SERVIDOR É O NOVO LIDER, COMUNICAR AO MULTICAST.
                var buffer = message.Remove(message.LastIndexOf(')')).Substring(message.IndexOf('(') + 1);
                var oldIp = buffer.Split(',')[0].Trim();
                var newIp = buffer.Split(',')[1].Trim();

                if (oldIp.Equals(string.Empty)) {
                    if (this.first) {
                        var msg = string.Format("ADD({0},{1},{2},{3});", newIp, 0, LIMIT_BITS_MD5, this.MyAddress);
                        this.requestRaftServer(msg);
                        this.SendMessage(msg);

                        var value = new Tuple<int, int>(0, LIMIT_BITS_MD5);
                        this.servers.Add(newIp, value);

                    } else {
                        this.BornProtocol();
                    }
                    this.requestRaftServer("StartListennerMulticast()");
                    this.StartListennerMulticast();

                } else {
                    var value = this.servers[oldIp];
                    var msg = string.Format("UPDATE({0},{1},{2});", oldIp, newIp, this.MyAddress);
                    this.requestRaftServer(msg);

                    var valueOld = this.servers[oldIp];
                    this.servers.Remove(oldIp);
                    this.servers.Add(newIp, valueOld);
                    
                    this.SendMessage(msg);
                }

            } else {
                Console.WriteLine("RAFT :: MENSAGEM COMITADA: {0}", message);
                if (message.Contains("StartListennerMulticast(")) {
                    this.StartListennerMulticast();
                } else if (message.Contains("ADD(")) {
                    var parameters = message.Replace("ADD(", "").Replace(")", "").Replace(";", "").Split(',');
                    var value = new Tuple<int, int>(int.Parse(parameters[1]), int.Parse(parameters[2]));
                    this.servers.Add(parameters[0], value);

                    Console.WriteLine("RAFT :: Adicionado o servidor <{0}, {1}, {2}>", parameters[0], value.Item1, value.Item2);

                } else if (message.Contains("UPDATE(")) {
                    var parameters = message.Replace("UPDATE(", "").Replace(")", "").Replace(";", "").Split(',');
                    var value = this.servers[parameters[0]];
                    this.servers.Remove(parameters[0]);
                    this.servers.Add(parameters[1], value);

                    Console.WriteLine("RAFT :: MODADO O SERVIDOR <{0}>  PARA <{1}>", parameters[0], parameters[1]);

                } else if (message.Contains("REFRESH(")) {
                    var parameters = message.Replace("REFRESH(", "").Replace(")", "").Replace(";", "").Split(',');
                    var value = new Tuple<int, int>(int.Parse(parameters[1]), int.Parse(parameters[2]));
                    this.servers.Remove(parameters[0]);
                    this.servers.Add(parameters[0], value);

                    Console.WriteLine("RAFT :: Atualizado o servidor {0} para range<{1}, {2}>", parameters[0], value.Item1, value.Item2);

                } else if (message.Contains("createVertex(")) {
                    Console.WriteLine("RAFT :: VERTICE ADICIONADO");
                    var parameters = message.Remove(message.IndexOf(')')).Substring(message.IndexOf('(') + 1).Split(',');
                    this.g.V.Add(new vertex() {
                        Name = int.Parse(parameters[0]),
                        Color = int.Parse(parameters[1]),
                        Description = parameters[2],
                        Weight = float.Parse(parameters[3])
                    });

                } else if (message.Contains("updateVertex(")) {
                    Console.WriteLine("RAFT :: VERTICE ATUALIZADO");
                    var parameters = message.Remove(message.IndexOf(')')).Substring(message.IndexOf('(') + 1).Split(',');
                    var v = this.g.V.Where(V => V.Name == int.Parse(parameters[0]));
                    if (v.Any()) {
                        v.First().Color = int.Parse(parameters[2]);
                        v.First().Description = parameters[2];
                        v.First().Weight = float.Parse(parameters[3]);
                    }

                } else if (message.Contains("createVertex(")) {
                    Console.WriteLine("RAFT :: VERTICE ADICIONADO");
                    var parameters = message.Remove(message.IndexOf(')')).Substring(message.IndexOf('(') + 1).Split(',');
                    this.g.V.Add(new vertex() {
                        Name = int.Parse(parameters[0]),
                        Color = int.Parse(parameters[1]),
                        Description = parameters[2],
                        Weight = float.Parse(parameters[3])
                    });
                } else if (message.Contains("createEdge(")) {
                    Console.WriteLine("RAFT :: ARESTA CRIADA");
                    var parameters = message.Remove(message.IndexOf(')')).Substring(message.IndexOf('(') + 1).Split(',');
                    this.g.E.Add(new edge() {
                        V1 = int.Parse(parameters[0]),
                        V2 = int.Parse(parameters[1]),
                        Directed = bool.Parse(parameters[2]),
                        Description = parameters[3],
                        Weight = float.Parse(parameters[4])
                    });

                } else if (message.Contains("deleteVertex(")) {
                    Console.WriteLine("RAFT :: VERTICE DELETADO");
                    var parameters = message.Remove(message.IndexOf(')')).Substring(message.IndexOf('(') + 1).Split(',');
                    this.g.V.RemoveAll(v => v.Name == int.Parse(parameters[0]));

                } else if (message.Contains("deleteEdge(")) {
                    Console.WriteLine("RAFT :: ARESTA DELETADA");
                    var parameters = message.Remove(message.IndexOf(')')).Substring(message.IndexOf('(') + 1).Split(',');
                    this.g.E.RemoveAll(e => e.Directed == bool.Parse(parameters[2]) && e.V1 == int.Parse(parameters[0]) && e.V2 == int.Parse(parameters[1]));

                }
            }
            return true;
        }
        #endregion
    }
}