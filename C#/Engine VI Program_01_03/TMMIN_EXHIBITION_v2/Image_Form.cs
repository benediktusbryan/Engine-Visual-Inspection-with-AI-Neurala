using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TMMIN_EXHIBITION_v2
{
    public partial class Image_Form : Form
    {
        public Image_Form()
        {
            InitializeComponent();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        public void show_image(string img_filename)
        {
            pictureBox1.Image = new Bitmap(Image.FromFile(@img_filename));

        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void Image_Form_Load(object sender, EventArgs e)
        {

        }
    }
}
