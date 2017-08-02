using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;

namespace Raft {
   public class Entry {
        public int id { get; set; }
        public string message { get; set; }
        public bool comited { get; set; }
        public HashSet<Tuple<string, int>> votes; //BITMAP

        public Entry() {
            this.comited = false;
            this.votes = new HashSet<Tuple<string, int>>();
        }

        public void SetAck(Tuple<string, int> t, int N_Nodes) {
            if (t == null)
                Console.WriteLine("FUDEO1");
            if (this.votes == null)
                Console.WriteLine("FUDEO2");
            
            this.votes.Add(t);
            if (this.votes.Count() + 1 > (N_Nodes + 1) / 2.0) {
                this.comited = true;
            }
        }
    }

    public class Log {
        public int last_committed_index { get; private set; }
        List<Entry> entryes;

        public Log() {
            this.entryes = new List<Entry>();
            last_committed_index = -1;
        }

        public void append(Entry e) {
            this.entryes.Add(e);
        }

        public Entry append(string message) {
            var entry = new Entry();
            entry.id = this.entryes.Any() ? this.LastEntry().id + 1: 0;
            entry.message = message;

            this.entryes.Add(entry);

            return entry;
        }

        public Entry LastEntry() {
            if(this.entryes.Any())
                return this.entryes.Last();

            return null;
        }

        public Entry GetEntry(int id) {
            if (this.entryes.Count() > id && id >= 0) {
                return this.entryes[id];
            }
            return null;
        }

        public void SetAck(int id, Tuple<string, int> tuple, int N_Nodes) {
            this.GetEntry(id).SetAck(tuple, N_Nodes);
            if (this.GetEntry(id).comited && this.last_committed_index < id) {
                this.last_committed_index = id;
            }
        }

        public void Commit(IServer thriftServer) {
            if (this.entryes.Count() - 1 > this.last_committed_index)  {
                this.entryes[++last_committed_index].comited = true;
                thriftServer.Commit(this.entryes[this.last_committed_index].message);
            }
        }
    }

    public class Node {
        public enum State { Leader, Follower, Candidate }

        private string cluster_id;

        private string ip { get; set; }
        private List<int> port { get; set; }
        private System.Timers.Timer election_time { get; set; }
        private System.Timers.Timer heartbeat_time { get; set; }

        private int t { get; set; }
        public State current_state { get; private set; }

        private System.Timers.Timer clock { get; set; }

        private Dictionary<Tuple<string, int>, TCP.TCPConnection> nodes;
        private Dictionary<Tuple<string, int>, int> nodes_acks; //GUARDA O ID DA PROXIMA ENTRADA À SER ENVIADA A CADA NODO.

        private Log log;
       
        private int nVotes = 0;
        private Raft.IServer thriftServer; //INTERFACE IMPLEMENTARDA PELO SERVIDOR THRIFT, ATRAVEZ DESSA INTERFACE É POSSIVEL REALMENTE EXECUTAR AS FUNÇÕES THRIFT

        public Node(List<Tuple<string, List<int>>> nodes, int node_index, Raft.IServer thriftServer) {
            this.t = 0;

            this.ip = nodes[node_index].Item1;
            this.port = nodes[node_index].Item2;
            this.current_state = State.Follower;  

            this.nodes = new Dictionary<Tuple<string, int>, TCP.TCPConnection>();
            this.nodes_acks = new Dictionary<Tuple<string, int>, int>();

            this.cluster_id = "";

            Tuple<int,int>[,] topology = this.PortTopology(nodes);
            for(int i = 0; i < nodes.Count(); i++) {
                if (i == node_index) continue;
                var IPLocal = new Tuple<string, int>(this.ip, topology[node_index, i].Item1);
                var IPRemote = new Tuple<string, int>(nodes[i].Item1, topology[node_index, i].Item2);

                var connection = new TCP.TCPConnection(IPLocal, IPRemote);
                this.nodes.Add(IPRemote, connection);

                this.nodes_acks.Add(IPRemote, 0);  //TODOS NODOS COMEÇAM SEM NENHUM COMMIT REALIZADO
            }
            
            this.election_time = new System.Timers.Timer(Random.random.Next(1000, 2000));
            this.heartbeat_time = new System.Timers.Timer((int)(this.election_time.Interval / 3));

            this.election_time.Stop();
            this.heartbeat_time.Stop();

            this.election_time.AutoReset = true;
            this.heartbeat_time.AutoReset = true;

            this.election_time.Elapsed += Election;
            this.heartbeat_time.Elapsed += HeartBeat;

            this.log = new Log();

            this.thriftServer = thriftServer;

            Console.WriteLine("RAFT :: Create node with election_time:{0}ms", this.election_time.Interval);
        }

        Tuple<int, int>[,] PortTopology(List<Tuple<string, List<int>>> nodes) {
            var matrix = new Tuple<int, int>[nodes.Count(), nodes.Count()];

            int port_index = 0;
            for (int i = 0; i < nodes.Count(); i++) {
                for (int j = i + 1; j < nodes.Count(); j++) {
                    var nextPortIndex = (port_index + 1) % (nodes.Count() - 1);
                    matrix[i, j] = Tuple.Create<int, int>(nodes[i].Item2[port_index], nodes[j].Item2[nextPortIndex]);
                    matrix[j, i] = Tuple.Create<int, int>(matrix[i, j].Item2, matrix[i, j].Item1);

                    port_index = nextPortIndex;
                }
            }

            return matrix;
        }

        private void resetTimer() {
            if (this.current_state != State.Leader) {
                this.election_time.Stop();
                this.election_time.Start();
            }
        }

        public void send(Tuple<string, int> node, string msg) {
            this.nodes[node].SendMessage(msg);
        }

        public void run() {
            Console.WriteLine("RAFT :: SERVER <{0}:<{1}>>", this.ip, string.Join("-", this.port));

            foreach (var node in this.nodes.Values) {
                Console.WriteLine("RAFT :: ESTABELENCO CONEXAO COM {0}:{1} ATRAVEZ DE PORTA {2}:{3}", node.IPRemote.Address, node.IPRemote.Port, node.IPLocal.Address, node.IPLocal.Port);
                Thread thread = new Thread(delegate () {
                    Listen(node);
                });
                thread.Start();
            }

            this.resetTimer();
        }

        public Entry AppendEntry(string message) { //CHAMADA APARTIR DO THRIFT
            if (this.current_state != State.Leader)
                return null;

            return this.log.append(message);
        }

        private void Listen(TCP.TCPConnection connection) {
            for (string buffer = null; true; buffer = connection.ReceiveMessage()) {
                if (buffer == null || buffer == string.Empty) continue;

                foreach (var message in buffer.Split('\0').Where(str => str != string.Empty)) {
                    if (message.Split('|').Length != 6 || !message.Split('|')[3].Trim().Equals("null")) {
                        Console.WriteLine("RAFT :: RECEIVE FROM <{0}:{1}> AT TERM={2} :: {3}", connection.IPRemote.Address, connection.IPRemote.Port,  this.t, message.Trim());
                    }

                    if (message.Contains("request vote for me")) {
                        if (int.Parse(message.Split('|')[0]) > this.t) {
                            this.t = int.Parse(message.Split('|')[0]);
                            if (this.log.last_committed_index <= (int.Parse(message.Split('|')[2]))) { //APENAS VOTA SIM SE A ULTIMA ENTRADA DO CANTIDO FOR MAIOR QUE A SUA, OU SEJA ESTA MAIS ATUALIZADO
                                connection.SendMessage(string.Format("{0}|vote yes", t));
                            } else {
                                connection.SendMessage(string.Format("{0}|vote no", t));
                            }
                        
                        } else {
                            connection.SendMessage(string.Format("{0}|vote no", t));
                        }
                    } else if (message.Contains("vote yes")) {
                        if (int.Parse(message.Split('|')[0]) == this.t) {
                            this.nVotes++;
                        }
                    } else if (message.Contains("win")) {
                        this.election_time.Stop();
                        var t = int.Parse(message.Split('|')[3]);
                        if (this.t <= t) {
                            this.current_state = State.Follower;
                            this.heartbeat_time.Stop();
                            this.election_time.Start(); 
                            this.t = t;
                        }
                        //this.thriftServer.Commit(string.Format("FollowUpdateLeader({0}, {1});", this.cluster_id, this.cluster_id = message.Split('|')[5]));

                    } else if (message.Contains("AE")) {
                        this.resetTimer();
                        var t = int.Parse(message.Split('|')[0]);
                        if (this.t <= t) {
                            this.current_state = State.Follower;
                            this.heartbeat_time.Stop();
                            this.t = t;

                            this.cluster_id = message.Split('|')[5];
                        }

                        var entryId = int.Parse(message.Split('|')[2]);
                        var LastEntry = this.log.LastEntry();

                        if (LastEntry == null) {
                            LastEntry = new Entry() { id = -1 };
                        }

                        if (LastEntry.id <= entryId) { //CHEGOU UMA ENTRADA ORDENADA, COMITA A ULTIMA E CONCATENA A NOVA
                            this.log.Commit(this.thriftServer);

                            foreach (var server in this.nodes.Keys) { //TODO SEGUIDOR ASSUME QUE SE ELE RECEBEU TODA A REDE TAMBÉM RECEBEU, CASO NÃO SEJA VERDADE
                                                                      //QUANDO ESSE SEGUIDOR ASSUMIR A LIDERANÇA, UM OUTRO SEGUIDOR IRÁ SOLICITAR O REGISTRO QUE É NECESSARIO
                                this.nodes_acks[server] = this.log.last_committed_index + 1;
                            }

                            if (LastEntry.id < entryId - 1) {//CHEGOU UMA ENTRADA MUITO A FRENTE
                                connection.SendMessage(string.Format("NACK|{0}|{1}", this.t, LastEntry.id + 1)); //SOLICITA A ENTRADA DA SEQUENCIA
                                continue; //DESCARTA A ENTRADA RECEBIDA (MANTEM O LOG ORDENADO)
                            }

                            if (message.Split('|')[3].Equals("null")) { //APENAS SINAL DE HEARTBEAT NÃO É UM ENVIO DE LOG
                                continue;
                            }

                            LastEntry = new Entry() {
                                id = entryId,
                                message = message.Split('|')[3],
                            };

                            this.log.append(LastEntry);
                            connection.SendMessage(string.Format("ACK|{0}|{1}", this.t, LastEntry.id)); //ACEITA A NOVA MENSAGEM
                        }

                    } else if (message.Contains("NACK")) {
                        var requestedId = int.Parse(message.Split('|')[2]);
                        var tuple = Tuple.Create<string, int>(connection.IPRemote.Address.ToString(), connection.IPRemote.Port);

                        this.nodes_acks[tuple] = requestedId;

                    } else if (message.Contains("ACK")) {
                        var id = int.Parse(message.Split('|')[2]);
                        var tuple = Tuple.Create<string, int>(connection.IPRemote.Address.ToString(), connection.IPRemote.Port);

                        this.log.SetAck(id, tuple, this.nodes.Count());
                        this.nodes_acks[tuple] = id + 1;

                        //Console.WriteLine("NEXT FOR ME " + tuple.Item1 + ":" + tuple.Item2 + ">>>" + this.nodes_acks[tuple]);
                    }
                }
            }
        }
        
        private void HeartBeat(object sender, EventArgs e) {
            if (this.current_state != State.Leader) {
                return;
            }
                
            foreach (var node in this.nodes) {
                var nextId = Math.Min(this.nodes_acks[node.Key], this.log.last_committed_index + 1);
                var entry = this.log.GetEntry(nextId);
                
                if (entry == null)
                    entry = new Entry() { id = this.nodes_acks[node.Key], message = "null" };

                node.Value.SendMessage(string.Format("{0}|AE|{1}|{2}|CLUSTER_ID|{3}", this.t, entry.id, entry.message, this.cluster_id));
            };
        }

        private void Election(object sender, EventArgs e) {
            if (this.current_state == State.Leader) {
                return;
            }
            
            //==========================new
            int current_t = ++this.t;
            Thread.Sleep(1);

            Console.WriteLine(string.Format("RAFT :: Node candidate at term {0}", current_t));
            this.current_state = State.Candidate;
            Thread.Sleep(10);
            this.nVotes = 1;
            //==========================

            foreach (var node in this.nodes.Values) {
                Thread thread = new Thread(delegate () {
                    node.SendMessage(string.Format("{0}|request vote for me|{1}", t, this.log.last_committed_index));
                });
                thread.Start();
            };
            //========================== 
            while (this.nVotes <= (this.nodes.Count() + 1) / 2 && this.t == current_t) ; //WAIT FOR MAJORITY 
            if (this.t != current_t) {
                Console.WriteLine(string.Format("RAFT :: Unsuccessful candidacy at term {0}", current_t));
                this.current_state = State.Follower;
                return; //THE VOTES DON'T MATTER, IS ANOTHER TURN.
            }

            //========================== 
            this.current_state = State.Leader;

            foreach (var node in this.nodes.Values) {
                node.SendMessage(string.Format("win |<{0}:{1}>|at t|{2}|cluster_id|{3}", node.IPLocal.Address, node.IPLocal.Port, current_t, this.thriftServer.clusterId));
            };

            this.election_time.Stop();
            this.heartbeat_time.Start();

            //Console.WriteLine(string.Format("OLD {0},  NEW {1}", this.cluster_id, this.cluster_id = this.thriftServer.clusterId));
            this.thriftServer.Commit(string.Format("newLeader({0}, {1});", this.cluster_id, this.cluster_id = this.thriftServer.clusterId));
        }
    }
}

