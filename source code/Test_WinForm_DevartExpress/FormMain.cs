using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MySqlBackupTestApp
{
    public partial class FormMain : Form
    {
        private readonly string _connectionSettingFile = System.IO.Path.Combine(Environment.CurrentDirectory, "ConnectionSettings.txt");
        private readonly List<Form> _lstForm = new List<Form>();
        private bool _stopWrite = true;

        public FormMain()
        {
            InitializeComponent();
            this.Text = "MySqlBackup.NET Testing Tool: " + Program.Version + ", Loaded MySqlBackup.DLL Version: " + Devart.Data.MySql.MySqlBackup.Version;
            LoadSettings();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestSimple));
            _stopWrite = false;
        }

        private void FormMain_SizeChanged(object sender, EventArgs e)
        {
            this.SuspendLayout();

            for (int i = 0; i < _lstForm.Count; i++)
            {
                _lstForm[i].WindowState = FormWindowState.Normal;
                _lstForm[i].WindowState = FormWindowState.Maximized;
            }

            this.ResumeLayout(true);
        }

        private void OpenForm(Type formType)
        {
            this.SuspendLayout();

            try
            {
                for (int i = 0; i < _lstForm.Count; i++)
                {
                    _lstForm[i].Close();
                    _lstForm[i].Dispose();
                    _lstForm[i] = null;
                }

                _lstForm.Clear();

                Form form = (Form)Activator.CreateInstance(formType);
                form.WindowState = FormWindowState.Maximized;
                form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                form.TopLevel = false;

                panel1.Controls.Add(form);
                form.Show();

                _lstForm.Add(form);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            this.ResumeLayout(true);
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void button_ExportAs_Click(object sender, EventArgs e)
        {
            SaveFileDialog f = new SaveFileDialog();
            if (Program.DefaultFolder != "")
                f.InitialDirectory = Program.DefaultFolder;
            f.Filter = "*.sql|*.sql|*.*|*.*";
            f.FileName = "TestDump " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".sql";
            if (DialogResult.OK == f.ShowDialog())
            {
                textBox_File.Text = f.FileName;
                Program.DefaultFolder = System.IO.Path.GetDirectoryName(textBox_File.Text);
            }
        }

        private void button_SelectFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog f = new OpenFileDialog();
            if (Program.DefaultFolder != "")
                f.InitialDirectory = Program.DefaultFolder;
            if (DialogResult.OK == f.ShowDialog())
            {
                textBox_File.Text = f.FileName;
                Program.DefaultFolder = System.IO.Path.GetDirectoryName(textBox_File.Text);
            }
        }

        private void button_View_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormDumpFileViewer));

            for (int i = 0; i < _lstForm.Count; i++)
            {
                if (_lstForm[i].GetType() == typeof(FormDumpFileViewer))
                {
                    if (Program.TargetFile == "")
                    { }
                    else 
                    {
                        ((FormDumpFileViewer)_lstForm[i]).OpenTargetFile();
                    }
                    break;
                }
            }
        }

        private void textBox_Connection_TextChanged(object sender, EventArgs e)
        {
            Program.ConnectionString = textBox_Connection.Text;
            WriteSettings(false);
        }

        private void textBox_File_TextChanged(object sender, EventArgs e)
        {
            Program.TargetFile = textBox_File.Text;
        }

        private void webReferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormReference));
        }

        private void compareDumpFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormCompareFile));
        }

        private void databaseInfoViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormDatabaseInfo));
        }

        private void dumpFileViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormDumpFileViewer));
        }

        private void testExportWithProgressBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestExportProgresBar));
        }

        private void createSampleDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormToolCreateSampleTable));
        }

        private void testExportImportWithOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormExImWithOptions));
        }

        private void testBasicWithDefaultSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestSimple));
        }

        private void queryBrowserToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormQueryBrowser));
        }

        private void testProgressReportImportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestImportProgressReport));
        }

        private void testEncryptDecryptDumpFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestEncryptDecrypt));
        }

        private void testCustomExportOfTablesAndRowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestCustomTablesExport));
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            FormAbout f = new FormAbout();
            f.ShowDialog();
        }

        private void testExportImportFromMemoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestExportImportMemory));
        }

        private void testExportImportWithZipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestZip));
        }

        private void decryptOldEncryptedDumpFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormDecryptOldDumpFile));
        }

        private void excludeTablesNewFeatureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestExcludeTables));
        }

        private void modifyHeadersAndFootersNewInV205ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestModifyHeadersFooters));
        }

        private void LoadSettings()
        {
            try
            {
                if (System.IO.File.Exists(_connectionSettingFile))
                {
                    textBox_Connection.Text = System.IO.File.ReadAllText(_connectionSettingFile);
                    cbAutosave.Checked = true;
                }
                else
                {
                    cbAutosave.Checked = false;
                }
                if(textBox_Connection.Text.Length==0)
                {
                    textBox_Connection.Text = "server=127.0.0.1;user=root;pwd=1234;";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private bool WriteSettings(bool forceSave)
        {
            try
            {
                if (_stopWrite)
                    return false;

                if (forceSave)
                {
                    System.IO.File.WriteAllText(_connectionSettingFile, textBox_Connection.Text);
                }
                else
                {
                    if (System.IO.File.Exists(_connectionSettingFile))
                        System.IO.File.WriteAllText(_connectionSettingFile, textBox_Connection.Text);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private void cbAutosave_CheckedChanged(object sender, EventArgs e)
        {
            if (_stopWrite)
                return;

            if (cbAutosave.Checked)
            {
                if (WriteSettings(true))
                {
                    MessageBox.Show("Automatic save enabled." + Environment.NewLine + "Connection String saved at" + Environment.NewLine + Environment.NewLine + _connectionSettingFile, "Saving Connection String");
                }
            }
            else
            {
                try
                {
                    System.IO.File.Delete(_connectionSettingFile);
                }
                catch { }
            }
        }

        private void exportImportToFromStringNewFeatureInV207ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestExportImportString));
        }

        private void queryBrowser2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormQueryBrowser2));
        }

        private void exportImportVIEWWithDependenciesWithinVIEWsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormTestViewDependencies f = new FormTestViewDependencies();
            f.ShowDialog();
        }

        private void exportImportBLOBIntoHexStringOrBinaryCharToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestBlob));
        }

        private void importCaptureErrorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenForm(typeof(FormTestImportCaptureError));
        }

        private void BtConnStrBuilder_Click(object sender, EventArgs e)
        {
            FormConnStringBuilder f = new FormConnStringBuilder(textBox_Connection.Text);
            if (f.ShowDialog() == DialogResult.OK)
            {
                textBox_Connection.Text = f.ConnStr;
            }
        }
    }
}