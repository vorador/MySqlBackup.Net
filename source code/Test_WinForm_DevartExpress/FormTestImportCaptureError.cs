﻿using Devart.Data.MySql;
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MySqlBackupTestApp
{
    public partial class FormTestImportCaptureError : Form
    {
        private string CacheLogFilePath
        {
            get
            {
                return Path.Combine(Application.StartupPath, "errorLogFilePath.txt");
            }
        }

        public FormTestImportCaptureError()
        {
            InitializeComponent();

            try
            {
                string oldlocation = File.ReadAllText(CacheLogFilePath);
                txtLogFilePath.Text = oldlocation;
            }
            catch
            {
                txtLogFilePath.Text = Path.Combine(Application.StartupPath, "ErrorLog.txt");
            }
        }

        private void RunTest(string sql)
        {
            txtError.Text = string.Empty;
            txtLastErrorSqlSyntax.Text = string.Empty;
            this.Refresh();

            try
            {
                string oldlocation = File.ReadAllText(CacheLogFilePath);

                if (oldlocation != txtLogFilePath.Text)
                    File.WriteAllText(CacheLogFilePath, txtLogFilePath.Text);
            }
            catch
            {
                File.WriteAllText(CacheLogFilePath, txtLogFilePath.Text);
            }

            try
            {
                File.Delete(txtLogFilePath.Text);
            }
            catch { }

            using (MySqlConnection conn = new MySqlConnection(Program.ConnectionString))
            {
                using (MySqlCommand cmd = new MySqlCommand())
                {
                    using (MySqlBackup mb = new MySqlBackup(cmd))
                    {
                        conn.Open();

                        try
                        {
                            cmd.Connection = conn;

                            mb.ImportInfo.IgnoreSqlError = cbIgnoreSqlError.Checked;
                            mb.ImportInfo.ErrorLogFile = txtLogFilePath.Text;
                            mb.ImportFromString(sql);
                        }
                        catch
                        { }

                        conn.Close();

                        try
                        {
                            if (txtLogFilePath.Text.Length > 0)
                                txtError.Text = File.ReadAllText(txtLogFilePath.Text);
                            else
                                txtError.Text = mb.LastError.ToString();
                        }
                        catch { }

                        txtLastErrorSqlSyntax.Text = mb.LastErrorSql;
                    }
                }
            }

            MessageBox.Show("Task ended.");
        }

        private string GetDumpContent()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(@"DROP TABLE IF EXISTS `sample_error_table`;
CREATE TABLE `sample_error_table` (
  `id` INTEGER UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(45),
  PRIMARY KEY (`id`)
)
ENGINE = InnoDB;

");
            int rowcount = 0;
            for (int i = 0; i < (int)nmErrorCount.Value; i++)
            {
                if (rowcount == 0)
                {
                    rowcount++;
                    sb.AppendFormat("INSERT INTO `sample_error_table`(name) VALUES('normal data {0}');", rowcount);
                    sb.AppendLine();
                }
                sb.AppendFormat("generate error {0};", i + 1);
                sb.AppendLine();
                rowcount++;
                sb.AppendFormat("INSERT INTO `sample_error_table`(name) VALUES('normal data {0}');", rowcount);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void btStart_Click(object sender, EventArgs e)
        {
            string sql = GetDumpContent();

            RunTest(sql);
        }

        private void btSetLogFilePath_Click(object sender, EventArgs e)
        {
            SaveFileDialog sf = new SaveFileDialog();
            sf.Filter = "*.txt|*.txt";
            sf.FileName = "Error.txt";
            if (sf.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            txtLogFilePath.Text = sf.FileName;
        }

        private void btResetLogFilePath_Click(object sender, EventArgs e)
        {
            txtLogFilePath.Text = Path.Combine(Application.StartupPath, "ErrorLog.txt");
        }

        private void btClearLogFilePath_Click(object sender, EventArgs e)
        {
            txtLogFilePath.Text = string.Empty;
        }

        private void btViewDump_Click(object sender, EventArgs e)
        {
            string data = GetDumpContent();
            txtError.Text = data;
        }

        private void btExport_Click(object sender, EventArgs e)
        {
            using (MySqlConnection conn = new MySqlConnection(Program.ConnectionString))
            {
                using (MySqlCommand cmd = new MySqlCommand())
                {
                    using (MySqlBackup mb = new MySqlBackup(cmd))
                    {
                        conn.Open();
                        cmd.Connection = conn;

                        txtError.Text = mb.ExportToString();

                        conn.Close();
                    }
                }
            }
        }

        private void btViewErrorLog_Click(object sender, EventArgs e)
        {
            try
            {
                txtError.Text = File.ReadAllText(txtLogFilePath.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("File not exists. Cannot view content");
                txtError.Text = "File not exists. Cannot view content\r\n\r\n" + ex.Message;
            }
        }

        private void BtStartFile_Click(object sender, EventArgs e)
        {
            string sql = File.ReadAllText(Program.TargetFile);
            RunTest(sql);
        }
    }
}
