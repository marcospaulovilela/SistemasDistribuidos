using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TheGraph.Thrift;


namespace TheGraph.Server
{
    class Handler : TheGraph.Thrift.TheGraph.Iface
    {
        private System.Threading.Mutex MUTEX_E; //MUTEX PARA CONTROLAR AS EGDES
        private System.Threading.Mutex MUTEX_V; //MUTEX PARA CONTROLAR OS VERTICES
     
        private graph g;
        public Handler()
        {
            this.g = new graph(); //INSTANCIA O GRAFO

            this.MUTEX_E = new System.Threading.Mutex();
            this.MUTEX_V = new System.Threading.Mutex();
        }

        public graph G() { return this.g; }
        
        public bool createVertex(vertex V)
        {
            MUTEX_V.WaitOne();

            Console.WriteLine(String.Format("Solitacao de criacao do vertice {0}", V.Name));

            if (this.g.V.Contains(V))
                throw new Thrift.VertexAlreadyExists();

            this.g.V.Add(V);
            bool result = this.g.V.Contains(V);

            MUTEX_V.ReleaseMutex();
            return result;
        }

        public bool deleteVertex(vertex V)
        {
            Console.WriteLine(String.Format("Solitacao de exclusao do vertice {0}", V.Name));

            MUTEX_V.WaitOne();

            if (!this.g.V.Contains(V))
            {
                MUTEX_V.ReleaseMutex();
                throw new Thrift.VertexDontExists();
            }

            this.g.V.Remove(V);
            bool result = this.g.E.RemoveAll(edge => edge.V1 == V.Name || edge.V2 == V.Name) == 1;
            
            MUTEX_V.ReleaseMutex();
            return result;   
        }

        public bool createEdge(edge E)
        {
            Console.WriteLine(String.Format("Solitacao de criacao da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));

            MUTEX_V.WaitOne();
            MUTEX_E.WaitOne();

            if (!this.g.V.Contains(new vertex() { Name = E.V1 }) || !this.g.V.Contains(new vertex() { Name = E.V2 }))
            {
                MUTEX_V.ReleaseMutex();
                MUTEX_E.ReleaseMutex();
                throw new Thrift.VertexDontExists();
            }

            MUTEX_V.ReleaseMutex();

            if (this.g.E.Contains(E))
            {
                MUTEX_E.ReleaseMutex();
                throw new Thrift.EdgeAlreadyExists();
            }

            this.g.E.Add(E);
            bool result = this.g.E.Contains(E);
                
            MUTEX_E.ReleaseMutex();
            return result;
        }

        public bool deleteEdge(edge E)
        {
            Console.WriteLine(String.Format("Solitacao de exclusao da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));

            MUTEX_E.WaitOne();

            if (!this.g.E.Contains(E))
            {
                MUTEX_E.ReleaseMutex();
                throw new Thrift.EdgeDontExists();
            }
                

            bool result = this.g.E.Remove(E);
            MUTEX_E.ReleaseMutex();

            return result;
        }

        public vertex readV(int VertexName)
        {
            Console.WriteLine(String.Format("Solitacao de leitura do vertice {0}", VertexName));

            MUTEX_V.WaitOne();

            foreach (vertex v in this.g.V)
            {
                if (v.Name == VertexName)
                {
                    MUTEX_V.ReleaseMutex();
                    return v;
                }
            }

            MUTEX_V.ReleaseMutex();
            throw new VertexDontExists();
        }

        public edge readE(int Vertex1_Name, int Vertex2_Name, bool Directed)
        {
            Console.WriteLine(String.Format("Solitacao de leitura da aresta {0}-{1}-{2}",Vertex1_Name, Vertex2_Name, Directed));

            MUTEX_E.WaitOne();

            foreach (edge e in this.g.E)
            {
                var E = new edge() { V1 = Vertex1_Name, V2 = Vertex2_Name, Directed = Directed };
                if (e.Equals(E))
                {
                    MUTEX_E.ReleaseMutex();
                    return e;
                }
            }

            MUTEX_E.ReleaseMutex();
            throw new EdgeDontExists();
        }   

        public bool updateVertex(vertex V)
        {
            Console.WriteLine(String.Format("Solitacao de atualização do vertice {0}", V.Name));

            MUTEX_V.WaitOne();
            
            foreach (vertex v in this.g.V)
            {
                if (v.Equals(V))
                {
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

        public bool updateEdge(edge E)
        {
            Console.WriteLine(String.Format("Solitacao de atualização da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));

            MUTEX_E.WaitOne();

            foreach (edge e in this.g.E)
            {
                if (e.Equals(E))
                {
                    e.Weight = E.Weight;
                    e.Description = E.Description;

                    MUTEX_E.ReleaseMutex();
                    return true;
                }
            }

            MUTEX_E.ReleaseMutex();
            throw new EdgeDontExists();
        }

        public List<vertex> getVertex(edge E)
        {
            Console.WriteLine(String.Format("Solitacao dos vertices da aresta {0}-{1}-{2}", E.V1, E.V2, E.Directed));
            try
            {
                readE(E.V1, E.V2, E.Directed);

                var V = new List<vertex>();
                V.Add(readV(E.V1));
                V.Add(readV(E.V2));

                return V;
            }
            catch(Exception e)
            {
                throw e;
            }
        }

        public List<edge> getEdges(vertex V)
        {
            Console.WriteLine(String.Format("Solitacao das arestas do vertice {0}", V.Name));
            try
            {
                readV(V.Name);

                MUTEX_E.WaitOne();
                var E = this.g.E.Where(edge => edge.V1 == V.Name || (!edge.Directed && edge.V2 == V.Name)).ToList();

                MUTEX_E.ReleaseMutex();
                return E;
            }
            catch(Exception e)
            {
                throw e;
            }
        }

        public List<vertex> getNeighborhood(vertex V)
        {
            Console.WriteLine(String.Format("Solitacao da vizinhanca do vertice {0}", V.Name));

            try
            {
                var edges = this.getEdges(V).ToList();
                if (!edges.Any())
                    return new List<vertex>();

                // O mutex deve ser solicitado apos a execução do methodo
                // getEdges, ou iremos ter um deathlock

                MUTEX_V.WaitOne();
                MUTEX_E.WaitOne();

                var neighborhood = new List<vertex>();
                foreach (edge e in edges)
                {
                    var neighbor = this.g.V.Where(v => v.Name != V.Name &&
                                                       ((e.Directed && v.Name == e.V2) || 
                                                       (!e.Directed && ((e.V1 == v.Name) || (e.V2 == v.Name)))));
                    if (!neighbor.Any() || neighbor.Count() > 1)
                        throw new Exception("MARCOS DO FUTURO, DEU RUIM PRA CARALHO, REFAZ ESSA MERDA, POR FAVOR ESTEJA SOBRIO");

                    neighborhood.Add(neighbor.First());
                }

                MUTEX_V.ReleaseMutex();
                MUTEX_E.ReleaseMutex();

                return neighborhood;
            }
            catch(Exception e)
            {
                throw e;
            }
        }
    }
}