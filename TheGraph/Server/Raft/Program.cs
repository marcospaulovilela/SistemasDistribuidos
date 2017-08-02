using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raft {
    class Program {
        static void Main(string[] args) {
            var nodes = new List<Tuple<string, List<int>>>() {
                Tuple.Create<string, List<int>>("127.0.0.1", new List<int>(){ 7070, 7071}),
                Tuple.Create<string, List<int>>("127.0.0.1", new List<int>(){ 8080, 8081}),
                Tuple.Create<string, List<int>>("127.0.0.1", new List<int>(){ 9090, 9091})
            };

            var node = new Raft.Node(nodes, int.Parse(args[0]), null);
            node.run();

            Console.ReadLine();
        }
    }
}
