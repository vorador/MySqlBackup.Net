﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Web.UI;
using MySqlConnector;

namespace MySqlBackupASPNET
{
    public partial class Index : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            phVersion.Controls.Add(new LiteralControl(MySqlBackup.Version));
        }

        protected void btExport_Click(object sender, EventArgs e)
        {
            try
            {
                string output = string.Empty;

                using (MySqlConnection conn = new MySqlConnection(txtConnString.Text))
                {
                    MySqlCommand cmd = new MySqlCommand();
                    MySqlBackup mb = new MySqlBackup(cmd);
                    cmd.Connection = conn;
                    conn.Open();

                    output = mb.ExportToString();
                }

                if (output.Length > (1024 * 100))
                {
                    lbError.Text = "The length of the output file has exceeded 100KB maximum length limit.<br />Try to use a smaller size of MySQL database sample.";
                    lbError.Visible = true;
                }
                else
                {
                    StoreFile.StoreSqlText(output);
                    Server.Transfer("~/Result.aspx", true);
                }
            }
            catch (Exception ex)
            {
                lbError.Text = ex.Message;
                lbError.Visible = true;
            }
        }

        protected void BtImport(object sender, EventArgs e)
        {
            try
            {
                if (!FileUpload1.HasFile)
                {
                    return;
                }

                byte[] ba = FileUpload1.FileBytes;

                if (FileUpload1.FileName.ToLower().EndsWith(".zip"))
                {
                    MemoryStream ms1 = new MemoryStream(ba);
                    ZipStorer zip = ZipStorer.Open(ms1, FileAccess.Read);
                    List<ZipStorer.ZipFileEntry> dir = zip.ReadCentralDir();
                    MemoryStream ms2 = new MemoryStream();
                    zip.ExtractFile(dir[0], ms2);
                    zip.Close();
                    ba = ms2.ToArray();
                }

                MemoryStream ms3 = new MemoryStream(ba);
                using (MySqlConnection conn = new MySqlConnection(txtConnString.Text))
                {
                    MySqlCommand cmd = new MySqlCommand();
                    MySqlBackup mb = new MySqlBackup(cmd);
                    cmd.Connection = conn;
                    conn.Open();
                    mb.ImportFromMemoryStream(ms3);
                }

                Header.Controls.Add(new LiteralControl("<script type=\"text/javascript\">alert('Import completed.');</script>"));
            }
            catch (Exception ex)
            {
                lbError.Text = ex.Message;
                lbError.Visible = true;
            }
        }
    }
}