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
    public partial class AvaliacoesFilme : Form {
        public AvaliacoesFilme() {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            var avaliacoes = connectionServer.c.getAdjacentes(int.Parse(txtIDfilme.Text));
            button1.Text = connectionServer.c.getV(int.Parse(txtIDfilme.Text)).Description.Split('&')[0];
            foreach(var node in avaliacoes) {
                var pessoa = connectionServer.c.getV(node.V2);
                dataGridView1.Rows.Add(pessoa.Name.ToString(), pessoa.Description, node.Weight.ToString(), node.Description) ;
            }
        }
    }
}
