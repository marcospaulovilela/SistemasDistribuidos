using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raft {
    public interface IServer {
        string clusterId { get; }
        bool Commit(string message);
    }
}
