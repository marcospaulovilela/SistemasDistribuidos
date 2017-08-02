using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TheGraph.Client  {
    public partial class MenorCaminho : Form {
        public MenorCaminho() {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e) {
            var vertex = connectionServer.c.getV(int.Parse(txtID.Text));
            button2.Text = vertex.Description;
        }

        private void button1_Click(object sender, EventArgs e) {
            var vertex = connectionServer.c.getV(int.Parse(maskedTextBox1.Text));
            button1.Text = vertex.Description;
        }

        private void button3_Click(object sender, EventArgs e) {
            var caminho = connectionServer.c.bfs(int.Parse(txtID.Text), int.Parse(maskedTextBox1.Text));
            if (!caminho.Any())
                MessageBox.Show("Nao existe caminho");

            foreach(var id in caminho) {
                listView1.Items.Add(connectionServer.c.getV(id).Description);
            }
        }
    }
}
