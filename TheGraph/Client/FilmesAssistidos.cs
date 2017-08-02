using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TheGraph.Client {
    public partial class FilmesAssistidos : Form {
        public FilmesAssistidos() {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            var avaliacoes = connectionServer.c.getAdjacentes(int.Parse(txtIDfilme.Text));
            button1.Text = connectionServer.c.getV(int.Parse(txtIDfilme.Text)).Description; //NOME DA PESSOA

            foreach (var node in avaliacoes) {
                var filme = connectionServer.c.getV(node.V2);
                dataGridView1.Rows.Add(filme.Name.ToString(), filme.Description.Split('&')[0], node.Weight.ToString(), node.Description);
            }
        }
    }
}
