using Devart.Data.MySql;
using System;
using System.ComponentModel;
using System.Data;
using System.Windows.Forms;

namespace MySqlBackupTestApp
{
    public partial class FormTestExportProgresBar : Form
    {
        private MySqlConnection _conn;
        private MySqlCommand _cmd;
        private readonly MySqlBackup _mb;
        private readonly Timer _timer1;
        private readonly BackgroundWorker _bwExport;

        private string _currentTableName = string.Empty;
        private int _totalRowsInCurrentTable = 0;
        private int _totalRowsInAllTables = 0;
        private int _currentRowIndexInCurrentTable = 0;
        private int _currentRowIndexInAllTable = 0;
        private int _totalTables = 0;
        private int _currentTableIndex = 0;

        private RowsDataExportMode _exportMode;

        private bool _cancel = false;

        private DateTime _timeStart = DateTime.MinValue;
        private DateTime _timeEnd = DateTime.MinValue;

        public FormTestExportProgresBar()
        {
            InitializeComponent();

            cbGetTotalRowsMode.SelectedIndex = 0;

            DataTable dt = new DataTable();
            dt.Columns.Add("id", typeof(RowsDataExportMode));
            dt.Columns.Add("name");
            foreach (RowsDataExportMode mode in Enum.GetValues(typeof(RowsDataExportMode)))
            {
                dt.Rows.Add(mode, mode.ToString());
            }

            comboBox_RowsExportMode.DataSource = dt;
            comboBox_RowsExportMode.DisplayMember = "name";
            comboBox_RowsExportMode.ValueMember = "id";
            comboBox_RowsExportMode.SelectedIndex = 0;

            _mb = new MySqlBackup();
            _mb.ExportProgressChanged += mb_ExportProgressChanged;

            _timer1 = new Timer();
            _timer1.Interval = 50;
            _timer1.Tick += timer1_Tick;

            _bwExport = new BackgroundWorker();
            _bwExport.DoWork += bwExport_DoWork;
            _bwExport.RunWorkerCompleted += bwExport_RunWorkerCompleted;
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            _cancel = true;
        }

        private void btExport_Click(object sender, EventArgs e)
        {
            if (!Program.TargetDirectoryIsValid())
                return;

            txtProgress.Text = string.Empty;
            _currentTableName = string.Empty;
            _totalRowsInCurrentTable = 0;
            _totalRowsInAllTables = 0;
            _currentRowIndexInCurrentTable = 0;
            _currentRowIndexInAllTable = 0;
            _totalTables = 0;
            _currentTableIndex = 0;
            _exportMode = (RowsDataExportMode)comboBox_RowsExportMode.SelectedValue;

            _conn = new MySqlConnection(Program.ConnectionString);
            _cmd = new MySqlCommand();
            _cmd.Connection = _conn;
            _conn.Open();

            _timer1.Interval = (int)nmExInterval.Value;
            _timer1.Start();

            _mb.ExportInfo.IntervalForProgressReport = (int)nmExInterval.Value;

            if (cbGetTotalRowsMode.SelectedIndex < 1)
                _mb.ExportInfo.GetTotalRowsMode = GetTotalRowsMethod.InformationSchema;
            else if (cbGetTotalRowsMode.SelectedIndex == 1)
                _mb.ExportInfo.GetTotalRowsMode = GetTotalRowsMethod.SelectCount;
            else
                _mb.ExportInfo.GetTotalRowsMode = GetTotalRowsMethod.Skip;

            _mb.Command = _cmd;

            _timeStart = DateTime.Now;
            lbTotalTime.Text = string.Empty;

            _bwExport.RunWorkerAsync();
        }

        private void bwExport_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                _mb.ExportInfo.RowsExportMode = _exportMode;
                _mb.ExportToFile(Program.TargetFile);
            }
            catch (Exception ex)
            {
                CloseConnection();
                MessageBox.Show(ex.ToString());
            }
        }

        private void bwExport_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CloseConnection();
            _timer1.Stop();

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
                    timer1_Tick(null, null);

                    //pbRowInAllTable.Value = pbRowInAllTable.Maximum;
                    //pbRowInCurTable.Value = pbRowInCurTable.Maximum;
                    //pbTable.Value = pbTable.Maximum;

                    //lbRowInCurTable.Text = pbRowInCurTable.Value + " of " + pbRowInCurTable.Maximum;
                    //lbRowInAllTable.Text = pbRowInAllTable.Value + " of " + pbRowInAllTable.Maximum;
                    //lbTableCount.Text = _currentTableIndex + " of " + _totalTables;

                    this.Refresh();
                    MessageBox.Show("Completed.");
                }
                else
                    MessageBox.Show("Completed with error(s)." + Environment.NewLine + Environment.NewLine + _mb.LastError.ToString());
            }
        }

        private void mb_ExportProgressChanged(object sender, ExportProgressArgs e)
        {
            if (_cancel)
            {
                _mb.StopAllProcess();
                return;
            }

            _currentRowIndexInAllTable = (int)e.CurrentRowIndexInAllTables;
            _currentRowIndexInCurrentTable = (int)e.CurrentRowIndexInCurrentTable;
            _currentTableIndex = e.CurrentTableIndex;
            _currentTableName = e.CurrentTableName;
            _totalRowsInAllTables = (int)e.TotalRowsInAllTables;
            _totalRowsInCurrentTable = (int)e.TotalRowsInCurrentTable;
            _totalTables = e.TotalTables;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_cancel)
            {
                _timer1.Stop();
                return;
            }

            txtProgress.Text += string.Format("Total: {0} - Current: {1}\r\n", _totalRowsInAllTables, _currentRowIndexInAllTable);
            txtProgress.Select(txtProgress.TextLength-1, 0);
            txtProgress.ScrollToCaret();

            pbTable.Maximum = _totalTables;
            if (_currentTableIndex <= pbTable.Maximum)
                pbTable.Value = _currentTableIndex;

            pbRowInCurTable.Maximum = _totalRowsInCurrentTable;
            if (_currentRowIndexInCurrentTable <= pbRowInCurTable.Maximum)
                pbRowInCurTable.Value = _currentRowIndexInCurrentTable;

            pbRowInAllTable.Maximum = _totalRowsInAllTables;
            if (_currentRowIndexInAllTable <= pbRowInAllTable.Maximum)
                pbRowInAllTable.Value = _currentRowIndexInAllTable;

            lbCurrentTableName.Text = "Current Processing Table = " + _currentTableName;
            lbRowInCurTable.Text = _currentRowIndexInCurrentTable + " of " + _totalRowsInCurrentTable;
            lbRowInAllTable.Text = _currentRowIndexInAllTable + " of " + _totalRowsInAllTables;
            lbTableCount.Text = _currentTableIndex + " of " + _totalTables;

            lbTotalRows_Tables.Text = _totalTables + "\r\n" + _totalRowsInAllTables;
        }

        private void CloseConnection()
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn.Dispose();
            }

            if (_cmd != null)
                _cmd.Dispose();
        }
    }
}