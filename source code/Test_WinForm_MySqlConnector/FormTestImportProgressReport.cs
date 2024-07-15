using MySqlConnector;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace MySqlBackupTestApp
{
    public partial class FormTestImportProgressReport : Form
    {
        private MySqlConnection _conn;
        private MySqlCommand _cmd;
        private readonly MySqlBackup _mb;
        private readonly Timer _timer1;
        private readonly BackgroundWorker _bwImport;

        private int _curBytes;
        private int _totalBytes;

        private bool _cancel = false;

        private DateTime _timeStart = DateTime.MinValue;
        private DateTime _timeEnd = DateTime.MinValue;

        public FormTestImportProgressReport()
        {
            InitializeComponent();

            _mb = new MySqlBackup();
            _mb.ImportInfo.IntervalForProgressReport = (int)nmImInterval.Value;
            _mb.ImportProgressChanged += mb_ImportProgressChanged;

            _timer1 = new Timer();
            _timer1.Interval = 50;
            _timer1.Tick += timer1_Tick;

            _bwImport = new BackgroundWorker();
            _bwImport.DoWork += bwImport_DoWork;
            _bwImport.RunWorkerCompleted += bwImport_RunWorkerCompleted;
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            _cancel = true;
        }

        private void btImport_Click(object sender, EventArgs e)
        {
            if (!Program.SourceFileExists())
                return;

            progressBar1.Value = 0;
            lbStatus.Text = "0 of 0 bytes";
            this.Refresh();

            _cancel = false;
            _curBytes = 0;
            _totalBytes = 0;

            if (_conn != null)
            {
                _conn.Dispose();
                _conn = null;
            }

            _conn = new MySqlConnection(Program.ConnectionString);
            _cmd = new MySqlCommand();
            _cmd.Connection = _conn;
            _conn.Open();

            _timer1.Start();

            _mb.ImportInfo.IntervalForProgressReport = (int)nmImInterval.Value;
            _mb.Command = _cmd;

            _timeStart = DateTime.Now;
            lbTotalTime.Text = string.Empty;

            _bwImport.RunWorkerAsync();
        }

        private void bwImport_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                _mb.ImportFromFile(Program.TargetFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_cancel)
            {
                _timer1.Stop();
                return;
            }

            progressBar1.Maximum = _totalBytes;

            if (_curBytes < progressBar1.Maximum)
                progressBar1.Value = _curBytes;

            lbStatus.Text = progressBar1.Value + " of " + progressBar1.Maximum;
        }

        private void mb_ImportProgressChanged(object sender, ImportProgressArgs e)
        {
            if (_cancel)
                _mb.StopAllProcess();

            _totalBytes = (int)e.TotalBytes;
            _curBytes = (int)e.CurrentBytes;
        }

        private void bwImport_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _timer1.Stop();

            CloseConnection();

            _timeEnd = DateTime.Now;
            var ts = _timeEnd - _timeStart;
            lbTotalTime.Text = $"{ts.Hours} h {ts.Minutes} m {ts.Seconds} s {ts.Milliseconds} ms";

            if (_cancel)
            {
                MessageBox.Show("Cancel by user.");
            }
            else
            {
                if (_mb.LastError == null)
                {
                    progressBar1.Value = progressBar1.Maximum;
                    lbStatus.Text = progressBar1.Value + " of " + progressBar1.Maximum;
                    this.Refresh();

                    MessageBox.Show("Completed.");
                }
                else
                    MessageBox.Show("Completed with error(s)." + Environment.NewLine + Environment.NewLine + _mb.LastError.ToString());
            }
        }

        private void CloseConnection()
        {
            if (_conn != null)
                _conn.Close();

            if (_conn != null)
                _conn.Dispose();

            if (_cmd != null)
                _cmd.Dispose();
        }
    }
}
