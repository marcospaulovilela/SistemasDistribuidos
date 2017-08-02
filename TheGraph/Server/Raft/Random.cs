using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raft {
    public static class Random {
        private static  System.Random r = null;
        public static System.Random random {
            get {
                if (r == null)
                    r = new System.Random();
                return r;
            }
        }
    }
}
