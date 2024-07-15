using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace MySqlBackupTestApp
{
    public partial class FormQueryBrowser2 : Form
    {
        private DataTable _dt = new DataTable();

        public FormQueryBrowser2()
        {
            InitializeComponent();

            dataGridView1.VirtualMode = true;
            dataGridView1.CellValueNeeded += dataGridView1_CellValueNeeded;
        }

        private void dataGridView1_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            try
            {
                if (e.RowIndex >= _dt.Rows.Count)
                    return;
                if (e.ColumnIndex >= _dt.Columns.Count)
                    return;

                if (_dt.Rows[e.RowIndex][e.ColumnIndex] == null || _dt.Rows[e.RowIndex][e.ColumnIndex] is DBNull)
                {
                    e.Value = "null";
                    return;
                }

                Type dtype = _dt.Columns[e.ColumnIndex].DataType;

                if (dtype == typeof(byte[]))
                {
                    e.Value = "blob/byte[]";
                }
                else if (dtype == typeof(DateTime))
                {
                    e.Value = ((DateTime)_dt.Rows[e.RowIndex][e.ColumnIndex]).ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    e.Value = _dt.Rows[e.RowIndex][e.ColumnIndex] + "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error has occured.\r\n\r\n" + ex.ToString());
                this.Close();
            }
        }

        private void btSQL_Click(object sender, EventArgs e)
        {
            ExecuteSql();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.A))
            {
                textBox1.SelectAll();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.Enter)
            {
                ExecuteSql();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                textBox1.Clear();
                e.SuppressKeyPress = true;
            }
        }

        private void ExecuteSql()
        {
            try
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Columns.Clear();

                _dt = new DataTable();

                string sql = textBox1.Text;

                string sqllower = sql.ToLower();

                bool isExecution = false;

                using (MySqlConnection conn = new MySqlConnection(Program.ConnectionString))
                {
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        conn.Open();
                        cmd.Connection = conn;

                        if (sqllower.StartsWith("select") || sqllower.StartsWith("show"))
                        {
                            cmd.CommandText = sql;
                            MySqlDataAdapter da = new MySqlDataAdapter(cmd);
                            da.Fill(_dt);
                        }
                        else
                        {
                            isExecution = true;

                            cmd.CommandText = sql;
                            int rowsAffected = cmd.ExecuteNonQuery();

                            _dt.Columns.Add("Result");

                            if (rowsAffected < 2)
                            {
                                _dt.Rows.Add(rowsAffected + " row affected by the last command, no resultset returned.");
                            }
                            else
                            {
                                _dt.Rows.Add(rowsAffected + " rows affected by the last command, no resultset returned.");
                            }
                        }

                        conn.Close();
                    }
                }

                foreach (DataColumn dc in _dt.Columns)
                {
                    DataGridViewTextBoxColumn dgvTb = new DataGridViewTextBoxColumn();
                    dgvTb.HeaderText = dc.ColumnName;
                    dataGridView1.Columns.Add(dgvTb);
                    if (isExecution)
                    {
                        dgvTb.Width = 700;
                    }
                    else
                    {
                        dgvTb.Width = (int)numericUpDown1.Value;
                    }
                }

                dataGridView1.RowTemplate.Height = 25;

                if (_dt.Rows.Count > 0)
                {
                    dataGridView1.Rows.Add(_dt.Rows.Count);
                }
                dataGridView1.ClearSelection();
            }
            catch (Exception ex)
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Columns.Clear();

                string err = ex.ToString();
                _dt = new DataTable();
                _dt.Columns.Add("Error");
                _dt.Rows.Add(err);

                dataGridView1.RowTemplate.Height = 300;

                DataGridViewTextBoxColumn dgvTb = new DataGridViewTextBoxColumn();
                dgvTb.Width = 750;
                dgvTb.HeaderText = "Error";
                dataGridView1.Columns.Add(dgvTb);
                dataGridView1.Rows.Add(1);

                dataGridView1.ClearSelection();
            }
        }

        private void FormQueryBrowser2_Load(object sender, EventArgs e)
        {
            ExecuteSql();
        }
    }
}
