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
    public partial class Menu : Form {
        public Menu() {
            InitializeComponent();
        }

        private void button3_Click(object sender, EventArgs e) {
            connectionServer.c = new Connection();
            connectionServer.c.Connect(textBox1.Text, int.Parse(textBox2.Text));
        }

        private void button4_Click(object sender, EventArgs e) {
            new AdicionarFilme().ShowDialog(); 
        }

        private void button1_Click(object sender, EventArgs e) {
            new AdicionarPessoa().ShowDialog();
        }

        private void button2_Click(object sender, EventArgs e) {
            new AvaliarFilme().ShowDialog();
        }

        private void button5_Click(object sender, EventArgs e) {
            new FilmesAssistidos().ShowDialog();
        }

        private void button6_Click(object sender, EventArgs e) {
            new AvaliacoesFilme().ShowDialog();
        }

        private void button7_Click(object sender, EventArgs e) {
            new MenorCaminho().ShowDialog();
        }
    }
}
