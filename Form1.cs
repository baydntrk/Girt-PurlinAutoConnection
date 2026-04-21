using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tekla.Structures.Model;
using TSMUI = Tekla.Structures.Model.UI;
using Tekla.Structures.Geometry3d;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Girt_PurlinAutoConnection
{

    public partial class Form1 : Form
    {
        public Model model = new Model();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!model.GetConnectionStatus())
            {
                MessageBox.Show("Tekla Structures is not running.");
                //this.Close();
                return;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DataTable dtSelected = GetSelectedObjectsAsDataTable();
            FilterInPlace(dtSelected);
            RemoveSlopedGirtsInPlace(dtSelected);

            DataTable dtLocalInfo = GetSelectedObjectsAsLocalInfoDataTable();
            FilterLocalInfoInPlace(dtLocalInfo);
            RemoveSlopedGirtsInPlace(dtLocalInfo);

            WriteAnyDataTableToConsole(dtLocalInfo, "RAW DATA 2 - LOCAL INFO");

            if (dtSelected.Rows.Count == 0)
            {
                MessageBox.Show("No valid objects selected. Please select beams, columns, purlins, or girts.");
                return;
            }

            ToleranceSettings tol = GetToleranceSettings();

            Form2 form2 = new Form2(dtSelected, dtLocalInfo, tol, this);
            form2.Show();
            this.Hide();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Seçili objeleri DataTable formatında döndürür. Beam objeleri için profil, malzeme ve koordinat bilgilerini içerir. Diğer objeler için temel bilgiler ve boş koordinatlar sağlanır.
        /// </summary>
        /// <returns></returns>
        private DataTable GetSelectedObjectsAsDataTable()
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("Identifier", typeof(string));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Profile", typeof(string));
            dt.Columns.Add("Material", typeof(string));
            dt.Columns.Add("StartX", typeof(double));
            dt.Columns.Add("StartY", typeof(double));
            dt.Columns.Add("StartZ", typeof(double));
            dt.Columns.Add("EndX", typeof(double));
            dt.Columns.Add("EndY", typeof(double));
            dt.Columns.Add("EndZ", typeof(double));

            TSMUI.ModelObjectSelector selector = new TSMUI.ModelObjectSelector();
            ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();

            while (selectedObjects.MoveNext())
            {
                ModelObject currentObject = selectedObjects.Current;

                if (currentObject == null)
                    continue;

                string identifier = currentObject.Identifier != null
                    ? currentObject.Identifier.ID.ToString()
                    : "";

                if (currentObject is Beam beam)
                {
                    string name = beam.Name ?? "";
                    string profile = beam.Profile != null ? beam.Profile.ProfileString : "";
                    string material = beam.Material != null ? beam.Material.MaterialString : "";

                    dt.Rows.Add(
                        identifier,
                        name,
                        profile,
                        material,
                        beam.StartPoint.X,
                        beam.StartPoint.Y,
                        beam.StartPoint.Z,
                        beam.EndPoint.X,
                        beam.EndPoint.Y,
                        beam.EndPoint.Z
                    );
                }
                else
                {
                    // Beam dışındaki objeler için boş koordinatla temel kayıt
                    dt.Rows.Add(
                        identifier,
                        "",
                        "",
                        "",
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0
                    );
                }
            }
            return dt;
        }

        /// <summary>
        /// Verilen DataTable içeriğini konsola yazdırır. Her sütun adı ve satır verisi tab formatında gösterilir.
        /// </summary>
        /// <param name="dt"></param>
        private void WriteDataTableToConsole (DataTable dt)
        {
            Console.WriteLine("====== SELECTED OBJECTS ======");

            foreach (DataColumn col in dt.Columns)
            {
                Console.WriteLine(col.ColumnName + "\t");
            }
            Console.WriteLine();

            foreach (DataRow row in dt.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    Console.Write(item.ToString() + "\t");
                }
                Console.WriteLine();
            }

            Console.WriteLine("====== END OF TABLE ======");
        }

        /// <summary>
        /// Verilen DataTable üzerinde tür filtresi uygular. Sadece "Beam", "Column", "Purlin" ve "Girt" türündeki satırlar kalır, diğerleri silinir. Filtreleme işlemi DataTable üzerinde doğrudan yapılır ve sonuç olarak sadece istenen türlerdeki kayıtlar kalır.
        /// </summary>
        /// <param name="dt"></param>
        private void FilterInPlace(DataTable dt)
        {
            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Beam",
                "Column",
                "Purlin",
                "Girt"
            };

            for (int i = dt.Rows.Count -1; i >= 0; i--)
            {
                string type = dt.Rows[i]["Name"]?.ToString();

                if (!allowedTypes.Contains(type))
                {
                    dt.Rows.RemoveAt(i);
                }
            }
        }
        #region SECOND RAW DATA (LOCAL AXIS / ORIENTATION)

        /// <summary>
        /// Seçili objeler için ikinci bir ham DataTable üretir.
        /// Bu tablo mevcut raw dataya dokunmaz.
        /// İçinde local coordinate system ve position/orientation bilgileri bulunur.
        /// </summary>
        private DataTable GetSelectedObjectsAsLocalInfoDataTable()
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("Identifier", typeof(string));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Profile", typeof(string));
            dt.Columns.Add("Material", typeof(string));

            dt.Columns.Add("StartX", typeof(double));
            dt.Columns.Add("StartY", typeof(double));
            dt.Columns.Add("StartZ", typeof(double));
            dt.Columns.Add("EndX", typeof(double));
            dt.Columns.Add("EndY", typeof(double));
            dt.Columns.Add("EndZ", typeof(double));

            dt.Columns.Add("LocalOriginX", typeof(double));
            dt.Columns.Add("LocalOriginY", typeof(double));
            dt.Columns.Add("LocalOriginZ", typeof(double));

            dt.Columns.Add("AxisX_X", typeof(double));
            dt.Columns.Add("AxisX_Y", typeof(double));
            dt.Columns.Add("AxisX_Z", typeof(double));

            dt.Columns.Add("AxisY_X", typeof(double));
            dt.Columns.Add("AxisY_Y", typeof(double));
            dt.Columns.Add("AxisY_Z", typeof(double));

            dt.Columns.Add("AxisZ_X", typeof(double));
            dt.Columns.Add("AxisZ_Y", typeof(double));
            dt.Columns.Add("AxisZ_Z", typeof(double));

            dt.Columns.Add("PositionPlane", typeof(string));
            dt.Columns.Add("PositionRotation", typeof(string));
            dt.Columns.Add("PositionDepth", typeof(string));

            TSMUI.ModelObjectSelector selector = new TSMUI.ModelObjectSelector();
            ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();

            while (selectedObjects.MoveNext())
            {
                ModelObject currentObject = selectedObjects.Current;

                if (currentObject == null)
                    continue;

                string identifier = currentObject.Identifier != null
                    ? currentObject.Identifier.ID.ToString()
                    : "";

                if (currentObject is Beam beam)
                {
                    string name = beam.Name ?? "";
                    string profile = beam.Profile != null ? beam.Profile.ProfileString : "";
                    string material = beam.Material != null ? beam.Material.MaterialString : "";

                    CoordinateSystem cs = beam.GetCoordinateSystem();

                    Vector axisZ = Cross(cs.AxisX, cs.AxisY);

                    string plane = beam.Position != null ? beam.Position.Plane.ToString() : "";
                    string rotation = beam.Position != null ? beam.Position.Rotation.ToString() : "";
                    string depth = beam.Position != null ? beam.Position.Depth.ToString() : "";

                    dt.Rows.Add(
                        identifier,
                        name,
                        profile,
                        material,

                        beam.StartPoint.X,
                        beam.StartPoint.Y,
                        beam.StartPoint.Z,
                        beam.EndPoint.X,
                        beam.EndPoint.Y,
                        beam.EndPoint.Z,

                        cs.Origin.X,
                        cs.Origin.Y,
                        cs.Origin.Z,

                        cs.AxisX.X,
                        cs.AxisX.Y,
                        cs.AxisX.Z,

                        cs.AxisY.X,
                        cs.AxisY.Y,
                        cs.AxisY.Z,

                        axisZ.X,
                        axisZ.Y,
                        axisZ.Z,

                        plane,
                        rotation,
                        depth
                    );
                }
                else
                {
                    dt.Rows.Add(
                        identifier,
                        "",
                        "",
                        "",

                        0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0,

                        0.0, 0.0, 0.0,

                        0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0,

                        "",
                        "",
                        ""
                    );
                }
            }

            return dt;
        }

        /// <summary>
        /// İki vektörün cross product'ını alır.
        /// </summary>
        private Vector Cross(Vector a, Vector b)
        {
            return new Vector(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        /// <summary>
        /// Verilen DataTable'ı konsola yazar.
        /// </summary>
        private void WriteAnyDataTableToConsole(DataTable dt, string title)
        {
            Console.WriteLine("====== " + title + " ======");

            foreach (DataColumn col in dt.Columns)
            {
                Console.Write(col.ColumnName + "\t");
            }
            Console.WriteLine();

            foreach (DataRow row in dt.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    Console.Write(item?.ToString() + "\t");
                }
                Console.WriteLine();
            }

            Console.WriteLine("====== END OF " + title + " ======");
        }
        private void FilterLocalInfoInPlace(DataTable dt)
        {
            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Beam",
                    "Column",
                    "Purlin",
                    "Girt"
                };

            for (int i = dt.Rows.Count - 1; i >= 0; i--)
            {
                string type = dt.Rows[i]["Name"]?.ToString();

                if (!allowedTypes.Contains(type))
                {
                    dt.Rows.RemoveAt(i);
                }
            }
        }
        #endregion
        private ToleranceSettings GetToleranceSettings()
        {
            double defaultGirt = 1000.0;
            double defaultPurlin = 200.0;
            double defaultPair = 100.0;

            double girtTol = defaultGirt;
            double purlinTol = defaultPurlin;
            double pairTol = defaultPair;

            // GIRT
            if (!string.IsNullOrWhiteSpace(textBox1.Text))
            {
                if (double.TryParse(textBox1.Text, out double val))
                    girtTol = val;
            }

            // PURLIN
            if (!string.IsNullOrWhiteSpace(textBox2.Text))
            {
                if (double.TryParse(textBox2.Text, out double val))
                    purlinTol = val;
            }

            // PAIR
            if (!string.IsNullOrWhiteSpace(textBox3.Text))
            {
                if (double.TryParse(textBox3.Text, out double val))
                    pairTol = val;
            }

            return new ToleranceSettings
            {
                GirtColumnTolerance = girtTol,
                PurlinBeamTolerance = purlinTol,
                PairTolerance = pairTol
            };
        }


        /// <summary>
        /// Girt türündeki elemanlar için, başlangıç ve bitiş Z koordinatları arasındaki fark belirli bir toleranstan büyükse, bu elemanların DataTable'dan silinmesini sağlar. 
        /// Bu yöntem, özellikle eğimli girtslerin otomatik bağlantı işlemlerinde sorun yaratabileceği durumlarda kullanışlıdır. 
        /// Z toleransı, kullanıcı tarafından belirlenebilir ve varsayılan olarak 1.0 olarak ayarlanmıştır.
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="zTolerance"></param>
        private void RemoveSlopedGirtsInPlace(DataTable dt, double zTolerance = 1.0)
        {
            for (int i = dt.Rows.Count - 1; i >= 0; i--)
            {
                string memberName = dt.Rows[i]["Name"]?.ToString()?.Trim();

                if (!string.Equals(memberName, "GIRT", StringComparison.OrdinalIgnoreCase))
                    continue;

                double startZ = Convert.ToDouble(dt.Rows[i]["StartZ"]);
                double endZ = Convert.ToDouble(dt.Rows[i]["EndZ"]);

                bool isSloped = Math.Abs(startZ - endZ) > zTolerance;

                if (isSloped)
                {
                    dt.Rows.RemoveAt(i);
                }
            }
        }
    }
}
