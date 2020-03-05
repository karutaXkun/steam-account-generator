using System;
using System.Windows.Forms;

namespace SteamAccCreator.Gui
{
    public partial class AddGroupDialog : Form
    {
        public AddGroupDialog()
        {
            InitializeComponent();
        }

        private void BtnAccept_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
