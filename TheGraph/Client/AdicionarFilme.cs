﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TheGraph.Client {
    public partial class AdicionarFilme : Form {
        public AdicionarFilme() {
            InitializeComponent();
        }

        private void AdicionarFilme_Load(object sender, EventArgs e) {

        }

        private void button1_Click(object sender, EventArgs e) {
            connectionServer.c.AddV(int.Parse(txtID.Text), 2, 0, txtNome.Text + '&' + txtDetalhes.Text);
        }

        private void button3_Click(object sender, EventArgs e) {
            connectionServer.c.RemoveV(int.Parse(txtID.Text));
        }

        private void button2_Click(object sender, EventArgs e) {
            var vertex = connectionServer.c.getV(int.Parse(txtID.Text));
            txtNome.Text = vertex.Description.Split('&')[0];
            txtDetalhes.Text = vertex.Description.Split('&')[1];
        }
    }
}
