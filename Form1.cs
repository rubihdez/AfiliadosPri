using ClosedXML.Excel;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AfiliadosPri
{
    public partial class Form1 : Form
    {
        private DataTable datosExcel = new DataTable();
        private DataView datosFiltrados;

        public Form1()
        {
            InitializeComponent();

            // Formato de fechas    
            dtpFechaInicio.Format = DateTimePickerFormat.Custom;
            dtpFechaInicio.CustomFormat = "dd/MM/yyyy";
            dtpFechaFin.Format = DateTimePickerFormat.Custom;
            dtpFechaFin.CustomFormat = "dd/MM/yyyy";

            dtpFechaInicio.Enabled = false;
            dtpFechaFin.Enabled = false;
        }

        private async void btnCargar_Click_1(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Excel|*.xlsx;*.xls" };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            string rutaArchivo = ofd.FileName;
            txtNombreArchivo.Text = Path.GetFileName(rutaArchivo);

            btnCargar.Enabled = false;
            btnCargar.Text = "Cargando...";

            try
            {
                var filas = new System.Collections.Generic.List<DataRow>();

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using (var stream = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);

                        datosExcel.Clear();
                        datosExcel.Columns.Clear();

                        var headerRow = worksheet.Row(1);
                        foreach (var cell in headerRow.CellsUsed())
                        {
                            string colName = cell.GetValue<string>().Trim();
                            if (!datosExcel.Columns.Contains(colName))
                            {
                                if (colName.Equals("FechaAfiliacion", StringComparison.OrdinalIgnoreCase))
                                    datosExcel.Columns.Add(colName, typeof(DateTime));
                                else
                                    datosExcel.Columns.Add(colName);
                            }
                            // Optionally, handle duplicates here (e.g., log, rename, or notify)
                        }

                        foreach (var row in worksheet.RowsUsed().Skip(1))
                        {
                            var dr = datosExcel.NewRow();
                            for (int i = 0; i < datosExcel.Columns.Count; i++)
                            {
                                string valor = row.Cell(i + 1).GetValue<string>().Trim();
                                if (datosExcel.Columns[i].ColumnName.Equals("FechaAfiliacion", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (DateTime.TryParse(valor, out DateTime fecha))
                                        dr[i] = fecha;
                                    else
                                        dr[i] = DBNull.Value;
                                }
                                else
                                {
                                    dr[i] = valor;
                                }
                            }
                            filas.Add(dr);
                        }
                    }
                });

                foreach (var dr in filas)
                    datosExcel.Rows.Add(dr);

                datosFiltrados = new DataView(datosExcel);
                dataGridView1.DataSource = datosFiltrados;

                // Llenar combo municipios
                if (datosExcel.Columns.Contains("Municipio"))
                {
                    cmbBoxMunicipio.Items.Clear();
                    cmbBoxMunicipio.Items.Add("Ninguno");
                    foreach (var municipio in datosExcel.AsEnumerable()
                        .Select(r => r.Field<string>("Municipio"))
                        .Where(m => !string.IsNullOrEmpty(m))
                        .Distinct()
                        .OrderBy(m => m))
                    {
                        cmbBoxMunicipio.Items.Add(municipio);
                    }
                    cmbBoxMunicipio.SelectedIndex = 0;
                }

                txtEstado.Text = datosExcel.Columns.Contains("Entidad") && datosExcel.Rows.Count > 0
                    ? datosExcel.Rows[0]["Entidad"].ToString()
                    : "Sin datos";

                txtNumeroAfiliaciones.Text = datosExcel.Rows.Count.ToString();

                if (datosExcel.Columns.Contains("FechaAfiliacion") && datosExcel.Rows.Count > 0)
                {
                    DateTime minFecha = DateTime.MaxValue;
                    DateTime maxFecha = DateTime.MinValue;
                    foreach (DataRow row in datosExcel.Rows)
                    {
                        var valor = row["FechaAfiliacion"];
                        // Verifica el tipo y valor aquí si lo necesitas
                    }
                    if (minFecha != DateTime.MaxValue && maxFecha != DateTime.MinValue)
                    {
                        dtpFechaInicio.Value = minFecha;
                        dtpFechaFin.Value = maxFecha;
                        dtpFechaInicio.MinDate = minFecha;
                        dtpFechaFin.MinDate = minFecha;
                        dtpFechaInicio.MaxDate = maxFecha;
                        dtpFechaFin.MaxDate = maxFecha;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar Excel: " + ex.Message);
            }
            finally
            {
                btnCargar.Enabled = true;
                btnCargar.Text = "Cargar Excel";
            }
        }

        // Método central para filtrar por municipio y fechas
        private void AplicarFiltros()
        {
            if (datosFiltrados == null) return;

            string filtro = "";

            // Filtro por municipio
            if (cmbBoxMunicipio.SelectedItem != null && cmbBoxMunicipio.SelectedItem.ToString() != "Ninguno")
            {
                filtro = $"MUNICIPIO = '{cmbBoxMunicipio.SelectedItem.ToString().Replace("'", "''")}'";
            }

            // Filtro por fecha
            if (checkBoxFecha.Checked && datosExcel.Columns.Contains("FECHA_AFILIACION"))
            {
                DateTime inicio = dtpFechaInicio.Value;
                DateTime fin = dtpFechaFin.Value;

                // Solo filtra si el rango es válido
                if (inicio <= fin)
                {
                    string filtroFecha = $"FECHA_AFILIACION >= #{inicio:MM/dd/yyyy}# AND FECHA_AFILIACION <= #{fin:MM/dd/yyyy}#";
                    if (filtro != "") filtro += " AND ";
                    filtro += filtroFecha;
                }
            }

            datosFiltrados.RowFilter = filtro;
            dataGridView1.DataSource = datosFiltrados;
            txtNumeroAfiliaciones.Text = datosFiltrados.Count.ToString();
        }

        private void BtnResetear_Click(object sender, EventArgs e)
        {
            txtNombreArchivo.Text = "";
            txtEstado.Text = "";
            txtNumeroAfiliaciones.Text = "0";
            cmbBoxMunicipio.Items.Clear();
            cmbBoxMunicipio.Items.Add("Ninguno");
            cmbBoxMunicipio.SelectedIndex = 0;
            dataGridView1.DataSource = null;
            datosExcel.Clear();
            datosExcel.Columns.Clear();
            datosFiltrados = null;
            checkBoxFecha.Checked = false;
            dtpFechaInicio.Enabled = false;
            dtpFechaFin.Enabled = false;
            dtpFechaInicio.Value = DateTime.Today;
            dtpFechaFin.Value = DateTime.Today;
        }

        
        // Evento cuando cambia la fecha de inicio
        private void dtpFechaInicio_ValueChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        // Evento cuando se marca/desmarca el filtro de fecha
        private void checkBoxFecha_CheckedChanged(object sender, EventArgs e)
        {
            dtpFechaInicio.Enabled = checkBoxFecha.Checked; // Habilita/deshabilita el campo
            dtpFechaFin.Enabled = checkBoxFecha.Checked;
            AplicarFiltros();
        }

        private void cmbBoxMunicipio_SelectedIndexChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void dtpFechaFin_ValueChanged_1(object sender, EventArgs e)
        {
            AplicarFiltros();
        }
    }
}
