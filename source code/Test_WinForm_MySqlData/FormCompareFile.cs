using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace MySqlBackupTestApp
{
    public partial class FormCompareFile : Form
    {
        private bool _file1Opened = false;
        private bool _file2Opened = false;
        private string _hash1 = string.Empty;
        private string _hash2 = string.Empty;
        private string _file1 = string.Empty;
        private string _file2 = string.Empty;

        public FormCompareFile()
        {
            InitializeComponent();
        }

        private void button_OpenFile1_Click(object sender, EventArgs e)
        {
            _file1Opened = GetHash(ref _file1, ref _hash1);
            lbFilePath1.Text = "File: " + _file1;
            lbSHA1.Text = "SHA256 Checksum: " + _hash1;
            CompareFile();
        }

        private void button_OpenFile2_Click(object sender, EventArgs e)
        {
            _file2Opened = GetHash(ref _file2, ref _hash2);
            lbFilePath2.Text = "File: " + _file2;
            lbSHA2.Text = "SHA256 Checksum: " + _hash2;
            CompareFile();
        }

        private bool GetHash(ref string file, ref string hash)
        {
            try
            {
                OpenFileDialog f = new OpenFileDialog();
                if (DialogResult.OK == f.ShowDialog())
                {
                    file = f.FileName;
                    byte[] ba = System.IO.File.ReadAllBytes(f.FileName);
                    hash = System.CryptoExpress.Sha256Hash(ba);
                    
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("File not valid.\r\n\r\n" + ex.ToString());
                return false; 
            }
        }

        private void CompareFile()
        {
            if (_file1Opened && _file2Opened)
            {
                if (_hash1 == _hash2)
                {
                    lbResult.Text = "Match. 100% same content.";
                    lbResult.ForeColor = Color.DarkGreen;
                }
                else
                {
                    lbResult.Text = "Not match. Both files are not same.";
                    lbResult.ForeColor = Color.Red;
                }
            }
            else
            {
                lbResult.Text = string.Empty;
            }
        }

        private void btInfo_Click(object sender, EventArgs e)
        {
            string a =
@"This function can be used to find out both EXPORT and IMPORT are working as expected or not by comparing the results.

Instructions:

1. Build the database and fill some data.
2. Export into first dump file.
3. Drop the database.
4. Import from first dump file.
5. Export again into second dump file.
6. Compare the first and second dump by using this SHA256 checksum.
7. If both checksums are match, this will prove that both EXPORT and IMPORT are working good.

Remember to turn off ""Record Dump Time"", as this will create differences between the dump files";
            MessageBox.Show(a, "Info");
        }
    }
}
