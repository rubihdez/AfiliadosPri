using ClosedXML.Excel;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Resources.ResXFileRef;

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
            //mostrar fechas en formato dd/MM/yyyy.
            dtpFechaFin.Format = DateTimePickerFormat.Custom;
            dtpFechaFin.CustomFormat = "dd/MM/yyyy";
            //desactivados hasta que se active el checkbox
            dtpFechaInicio.Enabled = false;
            dtpFechaFin.Enabled = false;
        }

        private async void btnCargar_Click_1(object sender, EventArgs e)
        {
            //OpenFileDialog para seleccionar un archivo .xlsx o .xls.
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Excel|*.xlsx;*.xls" };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            string rutaArchivo = ofd.FileName;
            txtNombreArchivo.Text = Path.GetFileName(rutaArchivo);
            //desactiva botón de carga y cambia su texto.
            btnCargar.Enabled = false;
            btnCargar.Text = "CARGANDO...";

            try
            {
               // una lista temporal de filas(DataRow) que se llenará mientras se lee el Excel
               var filas = new System.Collections.Generic.List<DataRow>();

                //wait  método q hace q espere a que termine esta tarea sin bloquear la interfaz de usuario.
                await System.Threading.Tasks.Task.Run(() => // Task.Run lee el Excel en un hilo separado para que la UI no se bloquee.
                {
                    //Abrir el archivo Excel con ClosedXML
                    using (var stream = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new XLWorkbook(stream))
                    {
                        //// obtener la primera hoja del Excel
                        var worksheet = workbook.Worksheet(1);

                        datosExcel.Clear();
                        datosExcel.Columns.Clear();

                        var headerRow = worksheet.Row(1);
                        foreach (var cell in headerRow.CellsUsed()) //recorre todas las celdas que tienen datos en esa fila de encabezados
                        {
                            string colName = cell.GetValue<string>().Trim();
                            if (!datosExcel.Columns.Contains(colName)) //verifica si el DataTable ya tiene esa columna.
                            {
                                //si la columna es FechaAfiliacion, la definimos como DateTime
                                if (colName.Equals("FechaAfiliacion", StringComparison.OrdinalIgnoreCase))
                                    datosExcel.Columns.Add(colName, typeof(DateTime));
                                else
                                    datosExcel.Columns.Add(colName);
                                //las demás columnas se agregan como string por defecto
                            }
                        }
                        //recorrer cada fila con datos, en la segunda fila
                        foreach (var row in worksheet.RowsUsed().Skip(1))
                        {
                            //crear un nuevo DataRow temporal
                            var dr = datosExcel.NewRow();
                            for (int i = 0; i < datosExcel.Columns.Count; i++)
                            {
                                string valor = row.Cell(i + 1).GetValue<string>().Trim();
                                //Si la columna es FechaAfiliacion, convertir a DateTime
                                if (datosExcel.Columns[i].ColumnName.Equals("FechaAfiliacion", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (DateTime.TryParse(valor, out DateTime fecha))
                                        dr[i] = fecha; //Guardar la fecha
                                    else
                                        dr[i] = DBNull.Value;
                                }
                                else
                                {
                                    dr[i] = valor; //guardar el valor como string para las demás columnas
                                }
                            }
                            filas.Add(dr);//GUARDAR TEMPORALMENRE
                        }
                    }
                });

                foreach (var dr in filas)
                    datosExcel.Rows.Add(dr); //Agregar todas las filas leídas al DataTable

                datosFiltrados = new DataView(datosExcel);
                dataGridView1.DataSource = datosFiltrados; // Mostrar los datos en el DataGridView

                // Llenar combo municipios
                if (datosExcel.Columns.Contains("Municipio"))
                {
                    cmbBoxMunicipio.Items.Clear();
                    cmbBoxMunicipio.Items.Add("Ninguno"); //// Opción por defecto
                    foreach (var municipio in datosExcel.AsEnumerable()
                        .Select(r => r.Field<string>("Municipio"))
                        .Where(m => !string.IsNullOrEmpty(m))
                        .Distinct()
                        .OrderBy(m => m)) // // Ordenar alfabéticamente
                    {
                        cmbBoxMunicipio.Items.Add(municipio);
                    }
                    cmbBoxMunicipio.SelectedIndex = 0;
                }

                txtEstado.Text = datosExcel.Columns.Contains("Entidad") && datosExcel.Rows.Count > 0 // Verificar si la columna "Entidad" existe y hay filas
                    ? datosExcel.Rows[0]["Entidad"].ToString() // Asumir que todas las filas tienen la misma entidad
                    : "Sin datos"; 


                //// Total de filas
                txtNumeroAfiliaciones.Text = datosExcel.Rows.Count.ToString();

                if (datosExcel.Columns.Contains("FechaAfiliacion") && datosExcel.Rows.Count > 0)
                {
                    DateTime minFecha = DateTime.MaxValue;
                    DateTime maxFecha = DateTime.MinValue;
                    foreach (DataRow row in datosExcel.Rows)
                    {
                        var valor = row["FechaAfiliacion"];
                        // Podrías calcular aquí minFecha y maxFecha si quieres.
                    }
                    if (minFecha != DateTime.MaxValue && maxFecha != DateTime.MinValue)
                    {
                        dtpFechaInicio.Value = minFecha;
                        dtpFechaFin.Value = maxFecha;
                        dtpFechaInicio.MinDate = minFecha;
                        dtpFechaFin.MinDate = minFecha;
                        dtpFechaInicio.MaxDate = maxFecha;
                        dtpFechaFin.MaxDate = maxFecha;
                        //minFecha queda en DateTime.MaxValue y maxFecha en DateTime.MinValue.
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




        //APLICAR FILTROS
        private void AplicarFiltros()
        {
            if (datosFiltrados == null) return; // si no hay datos, salir

            string filtro = "";

            //comprueba si el usuario seleccionó un municipio distinto de "Ninguno"
            // Filtro por municipio
            if (cmbBoxMunicipio.SelectedItem != null && cmbBoxMunicipio.SelectedItem.ToString() != "Ninguno")
            {
                filtro = $"MUNICIPIO = '{cmbBoxMunicipio.SelectedItem.ToString().Replace("'", "''")}'";
            }

            // Filtro por fecha
            if (checkBoxFecha.Checked && datosExcel.Columns.Contains("FECHA_AFILIACION"))
            {
                //evalua la primera y última fecha seleccionada
                DateTime inicio = dtpFechaInicio.Value;
                DateTime fin = dtpFechaFin.Value;

                if (inicio <= fin)
                {
                    string filtroFecha = $"FECHA_AFILIACION >= #{inicio:MM/dd/yyyy}# AND FECHA_AFILIACION <= #{fin:MM/dd/yyyy}#";
                    if (filtro != "") filtro += " AND ";
                    filtro += filtroFecha;
                }
            }

            datosFiltrados.RowFilter = filtro; //RowFilter aplica los filtros construidos al DataView.
            dataGridView1.DataSource = datosFiltrados; //DataGridView se refresca automáticamente con los datos filtrados.
            txtNumeroAfiliaciones.Text = datosFiltrados.Count.ToString(); //txtNumeroAfiliaciones se actualiza para mostrar cuántas filas quedan visibles.
        }

        // botón Resetear 
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

        // Eventos de filtros
        private void dtpFechaInicio_ValueChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void checkBoxFecha_CheckedChanged(object sender, EventArgs e)
        {
            dtpFechaInicio.Enabled = checkBoxFecha.Checked;
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