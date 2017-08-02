using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TheGraph.Client{
    public partial class AvaliarFilme : Form {
        public AvaliarFilme() {
            InitializeComponent();
        }

        private void AvaliarFilme_Load(object sender, EventArgs e) {
            
        }

        private void button4_Click(object sender, EventArgs e) {
            connectionServer.c.AddE(int.Parse(txtID.Text), int.Parse(txtIDfilme.Text), double.Parse(txtNota.Text), false, txtDetalhes.Text);
        }

        private void button3_Click(object sender, EventArgs e) {
            connectionServer.c.RemoveE(int.Parse(txtID.Text), int.Parse(txtIDfilme.Text), false);
        }

        private void button2_Click(object sender, EventArgs e) {
            var vertex = connectionServer.c.getV(int.Parse(txtID.Text));
            button2.Text = vertex.Description;
        }

        private void button1_Click(object sender, EventArgs e) {
            var vertex = connectionServer.c.getV(int.Parse(txtIDfilme.Text));
            button1.Text = vertex.Description.Split('&')[0];
        }
    }
}
