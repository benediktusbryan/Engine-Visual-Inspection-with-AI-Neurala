using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TMMIN_EXHIBITION_v2
{
    public partial class Scan_Barcode_Form : Form
    {
        private Inspection_Form _inspectionForm;

        public Scan_Barcode_Form(Inspection_Form inspectionForm)
        {
            InitializeComponent();
            _inspectionForm = inspectionForm;
        }
        private void Scan_Barcode_Load(object sender, EventArgs e)
        {
            textBox4.Select();
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {

            _inspectionForm.barcodeEngineNumber = "x";
            this.Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            textBox4.Select();
        }

        private void textBox4_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && textBox4.Text != "")
            {
                _inspectionForm.barcodeEngineNumber = textBox4.Text;
                this.Close();
            }
        }
    }
}
