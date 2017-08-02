using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Thrift;
using Thrift.Protocol;
using Thrift.Server;
using Thrift.Transport;

namespace TheGraph.Client
{
    public partial class Graph_Handler : Form
    {
        Thrift.graph graph;

        TTransport transport; 
        TProtocol protocol;
        Thrift.TheGraph.Client client;

        public Graph_Handler()
        {
            this.graph = null;
            /*this.transport = new TSocket("localhost", 9090);
            this.protocol = new TBinaryProtocol(transport);
            this.client = new Thrift.TheGraph.Client(this.protocol);    

            transport.Open();*/
            
            InitializeComponent();
        }

        private void pnl_Grafo_Paint(object sender, PaintEventArgs e)
        {
            if (this.graph == null)
                return;

            Dictionary<int, Point> positions = new Dictionary<int, Point>();
            List<Tuple<int, int, bool>> edges = new List<Tuple<int, int, bool>>();

            double radius = 200d;
            Point center = new Point(pnl_Grafo.Width / 2, pnl_Grafo.Height / 2); //UM PONTO NO MEIO DO PAINEL
            
            Pen p = new Pen(Color.DarkOrange, 1);
            Graphics g = e.Graphics;

            double slice = 2 * Math.PI / this.graph.V.Count;
            for (int i = 0; i < this.graph.V.Count; i++) 
            {
                double angle = slice * i;
                int newX = (int)(center.X + radius * Math.Cos(angle));
                int newY = (int)(center.Y + radius * Math.Sin(angle));

                var point = new Point(newX, newY);
                positions.Add(this.graph.V[i].Name, point);

                var rectangle = new Rectangle(point, new Size(15, 15));
                g.DrawRectangle(p, rectangle);
                g.DrawString(this.graph.V[i].Name.ToString(), new Font("Arial", 11), new SolidBrush(Color.WhiteSmoke), rectangle);
            }


            foreach (Thrift.edge a in this.graph.E)
            {
                if (edges.Any(t => t.Item1 == a.V2 && t.Item2 == a.V1 && a.Directed == t.Item3))
                    continue;

                edges.Add(Tuple.Create<int, int, bool>(a.V1, a.V2, a.Directed));

                p = new Pen(Color.DarkOrange, 1);
                Point pt1 = positions[a.V1];   
                Point pt2 = positions[a.V2];   

                if(pt1.X > pt2.X) //PT2 -------- PT1
                    pt2.X += 15;
                
                if (pt2.X > pt1.X) //PT1 -------- PT2
                    pt1.X += 15;

                if (pt1.Y > pt2.Y) 
                    pt2.Y += 15;

                if (pt2.Y > pt1.Y)
                    pt1.Y += 15;

                //Ponto mediano na aresta que ira mostrar seu peso
                g.DrawString(a.Weight.ToString(), new Font("Arial", 11), new SolidBrush(Color.WhiteSmoke), new PointF(((pt1.X + pt2.X) / 2) + 2, ((pt1.Y + pt2.Y) / 2) + 2));

                if (a.Directed)
                {
                    p = new Pen(Color.DarkOrange, 1);
                    var bigArrow = new System.Drawing.Drawing2D.AdjustableArrowCap(5, 5);
                    p.CustomStartCap = bigArrow;
                }

                g.DrawLine(p, pt2, pt1);
            }
        }
        
        private void txt_Command_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                string command = txt_Command.Text.ToLower();

                txt_Command.Text = "";
                txt_Console.Text += command + "\n";

                if (command.Contains("draw"))
                {
                    this.graph = client.G(true);
                    pnl_Grafo.Refresh();
                }

                else if (command.Contains("connect"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    if (this.transport != null && this.transport.IsOpen)
                        this.transport.Close();

                    this.transport = new TSocket(values[0], int.Parse(values[1]));
                    this.protocol = new TBinaryProtocol(transport);
                    this.client = new Thrift.TheGraph.Client(this.protocol);

                    transport.Open();
                }

                else if (command.Contains("add-v"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    //
                    int Name = int.Parse(values[0]);
                    int Color = int.Parse(values[1]);
                    //
                    double P = double.Parse(values[2].Replace(".", ","));
                    //
                    string Description = values[3];

                    try
                    {
                       var result = client.createVertex(new Thrift.vertex()
                        {
                            Name = Name,
                            Color = Color,
                            Weight = P,
                            Description = Description
                        });
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }

                else if (command.Contains("get-v"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    try
                    {
                        if (values.Length == 1)
                        {
                            int name = int.Parse(values[0]);
                            var V = client.readV(name);
                            txt_Console.Text += string.Format("Name = {0}\nColor={1}\nWeight={2}\nDescription={3}\n", V.Name, V.Color, V.Weight, V.Description);
                        }
                        if (values.Length == 3)
                        {
                            int V1 = int.Parse(values[0]);
                            int V2 = int.Parse(values[1]);
                            bool Directed = bool.Parse(values[2]);

                            var LV = client.getVertex(new Thrift.edge()
                            {
                                V1 = V1,
                                V2 = V2,
                                Directed = Directed
                            });

                            foreach(var V in LV)
                                txt_Console.Text += string.Format("Name = {0}\nColor={1}\nWeight={2}\nDescription={3}\n", V.Name, V.Color, V.Weight, V.Description);
                        }

                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }

                else if (command.Contains("rem-v"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    //
                    int Name = int.Parse(values[0]);

                    try
                    {
                        client.deleteVertex(new Thrift.vertex() { Name = Name });
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }

                else if (command.Contains("update-v"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    //
                    int Name = int.Parse(values[0]);
                    int Color = int.Parse(values[1]);
                    //
                    double P = double.Parse(values[2].Replace(".", ","));
                    //
                    string Description = values[3];

                    try
                    {
                        client.updateVertex(new Thrift.vertex()
                        {
                            Name = Name,
                            Color = Color,
                            Weight = P,
                            Description = Description
                        });
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }

                else if (command.Contains("add-e"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');
                    
                    //
                    int V1 = int.Parse(values[0]);
                    int V2 = int.Parse(values[1]);
                    //
                    double P = double.Parse(values[2].Replace(".", ","));
                    //
                    bool Directed = bool.Parse(values[3]);
                    //
                    string Description = values[4];

                    try { 
                        client.createEdge(new Thrift.edge()
                        {
                            V1 = V1,
                            V2 = V2,
                            Weight = P,
                            Directed = Directed,
                            Description = Description
                        });
                    }
                    catch(Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }

                else if (command.Contains("get-e"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    try
                    {
                        if (values.Length == 3)
                        {
                            //
                            int V1 = int.Parse(values[0]);
                            int V2 = int.Parse(values[1]);
                            //
                            bool Directed = bool.Parse(values[2]);

                            var E = client.readE(V1, V2, Directed);
                            txt_Console.Text += string.Format("V1={0}\nV2={1}\nDirected={2}\nWeight={3}\nDescription={4}\n", E.V1, E.V2, E.Directed, E.Weight, E.Description);
                        }
                        else if (values.Length == 1)
                        {
                            int Name = int.Parse(values[0]);
                            var LE = client.getEdges(new Thrift.vertex() { Name = Name });

                            foreach(var E in LE)
                                txt_Console.Text += string.Format("V1={0}\nV2={1}\nDirected={2}\nWeight={3}\nDescription={4}\n", E.V1, E.V2, E.Directed, E.Weight, E.Description);
                        }
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }
                
                else if (command.Contains("rem-e"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    //  
                    int V1 = int.Parse(values[0]);
                    int V2 = int.Parse(values[1]);

                    bool Directed = bool.Parse(values[2]);

                    try
                    {
                        client.deleteEdge(new Thrift.edge()
                        {
                            V1 = V1,
                            V2 = V2,
                            Directed = Directed
                        });
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }
                
                else if (command.Contains("update-e"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    //
                    int V1 = int.Parse(values[0]);
                    int V2 = int.Parse(values[1]);
                    //
                    double P = double.Parse(values[2].Replace(".", ","));
                    //
                    bool Directed = bool.Parse(values[3]);
                    //
                    string Description = values[4];

                    try
                    {
                        client.updateEdge(new Thrift.edge()
                        {
                            V1 = V1,
                            V2 = V2,
                            Weight = P,
                            Directed = Directed,
                            Description = Description
                        });
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }

                else if (command.Contains("bfs"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    //
                    int V1 = int.Parse(values[0]);
                    int V2 = int.Parse(values[1]);
                    //

                    try {
                        try{
                            var result = client.bfs(V2, new List<List<int>>() { new List<int>() { V1 } }, new List<int>());
                            txt_Console.Text += string.Join("-", result) + "\n";
                        } catch(Exception ex) {
                            txt_Console.Text += "NÃO EXISTE CAMINHO\n";
                        }
                    } catch (Exception exception) {
                        MessageBox.Show(exception.ToString());
                    }
                }


                else if (command.Contains("neighborhood"))
                {
                    command = command.Remove(0, command.IndexOf("("));
                    command = command.Replace("(", "").Replace(")", "");
                    var values = command.Split(',');

                    try {
                        int Name = int.Parse(values[0]);
                        var LV = client.getNeighborhood(new Thrift.vertex() { Name = Name});

                        foreach (var V in LV)
                            txt_Console.Text += string.Format("Name = {0}\nColor={1}\nWeight={3}\nDescription={2}\n", V.Name, V.Color, V.Weight, V.Description);
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.ToString());
                    }
                }
            }
        }
    }
}
