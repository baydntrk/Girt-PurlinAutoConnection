using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Microsoft.WindowsAPICodePack.Dialogs;
using Tekla.Structures.Geometry3d;

namespace Girt_PurlinAutoConnection
{
    public partial class Form2 : Form
    {
        /// <summary>
        /// Global DataTable
        /// </summary>
        public DataTable _dt;

        public DataTable _locDT;

        public ToleranceSettings _tol;

        public Model _model = new Model();

        private Form1 _form1;

        public Form2(DataTable dt, DataTable locDT, ToleranceSettings tol, Form1 form1)
        {
            InitializeComponent();
            advancedDataGridView2.CellClick += advancedDataGridView2_CellClick;
            advancedDataGridView1.CellClick += AdvancedDataGridView1_CellClick;
            _dt = dt;
            _locDT = locDT;
            _tol = tol;
            _form1 = form1;

            advancedDataGridView1.CellValueChanged += advancedDataGridView1_CellValueChanged;
            advancedDataGridView1.CurrentCellDirtyStateChanged += advancedDataGridView1_CurrentCellDirtyStateChanged;

            advancedDataGridView2.CellValueChanged += advancedDataGridView2_CellValueChanged;
            advancedDataGridView2.CurrentCellDirtyStateChanged += advancedDataGridView2_CurrentCellDirtyStateChanged;
            _form1 = form1;
        }

        private void AdvancedDataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // Geçersiz satır/kolon kontrolü
                if (e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;

                // Sadece 1, 4 ve 7. kolonlar için çalışsın
                // DataGridView indexleri 0'dan başladığı için:
                // 1. kolon = 0
                // 4. kolon = 3
                // 7. kolon = 6
                if (e.ColumnIndex != 0 && e.ColumnIndex != 3 && e.ColumnIndex != 6)
                    return;

                if (!_model.GetConnectionStatus())
                {
                    MessageBox.Show("Tekla model bağlantısı kurulamadı.");
                    return;
                }

                object cellValue = advancedDataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

                if (cellValue == null || string.IsNullOrWhiteSpace(cellValue.ToString()))
                    return;

                // ID parse et
                if (!int.TryParse(cellValue.ToString(), out int teklaId))
                {
                    MessageBox.Show("Geçerli bir Tekla ID değeri bulunamadı.");
                    return;
                }

                // Identifier oluştur
                Identifier identifier = new Identifier(teklaId);

                // Model objesini al
                ModelObject modelObject = _model.SelectModelObject(identifier);

                if (modelObject == null)
                {
                    MessageBox.Show($"ID={teklaId} için model objesi bulunamadı.");
                    return;
                }

                // Modelde seç
                ArrayList objectList = new ArrayList();
                objectList.Add(modelObject);

                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                selector.Select(objectList);

                // İstersen görünümde yaklaştırma için bunu da ekleyebilirsin:
                // Tekla.Structures.Model.Operations.Operation.ShowOnlySelected();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            // -------- GIRT FLOW --------
            DataTable dtConnection = FindColumnGirtConnections(_dt, _tol.GirtColumnTolerance);
            DataTable dtGirtPairs = FindAdjacentGirtPairs(_dt, _tol.PairTolerance, 50.0);
            DataTable dtMergedGirtConnections = MergePairedGirtsIntoConnectionsSafe(dtConnection, dtGirtPairs);

            // -------- PURLIN FLOW --------
            DataTable dtBeamPurlin = FindBeamPurlinConnections(_dt, _tol.PurlinBeamTolerance);
            DataTable dtPurlinPairs = FindAdjacentPurlinPairs(_dt, _tol.PairTolerance, 50.0);
            DataTable dtMergedPurlinConnections = MergePairedPurlinsIntoConnectionsSafe(dtBeamPurlin, dtPurlinPairs);

            // -------- MACRO NUMBER ADD --------
            AddMacroNumberColumn(dtMergedGirtConnections, 74);
            AddMacroNumberColumn(dtMergedPurlinConnections, 70);

            // -------- GIRT - COLUMN RELATION --------
            AddGirtColumnRelationColumn(dtMergedGirtConnections, _locDT);
            AddGirtColumnFinalValueColumn(dtMergedGirtConnections, _locDT);
            AddGirtAttributeNameColumn(dtMergedGirtConnections);

            // -------- PURLIN ATTRIBUTE NAME --------
            AddPurlinAttributeNameColumn(dtMergedPurlinConnections);

            // Relation kolonunu kaldır
            if (dtMergedGirtConnections.Columns.Contains("GirtToColumnAxisY"))
            {
                 dtMergedGirtConnections.Columns.Remove("GirtToColumnAxisY");
            }

            // -------- GRID & PURLIN --------
            advancedDataGridView1.DataSource = dtMergedGirtConnections;   // GIRT
            advancedDataGridView2.DataSource = dtMergedPurlinConnections; // PURLIN

            //Yazıya göre kolon genişliği
            advancedDataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            advancedDataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            //Renklendirme
            ApplyExcelStyle(advancedDataGridView1);
            ApplyExcelStyle(advancedDataGridView2);
        }

        /// <summary>
        /// Bağlantı tablosu oluşturur. Bu tablo, her satırda üç farklı objenin (Pre, Sec, Third) ID, Name ve Profile bilgilerini içerir. 
        /// Bu yapı, kullanıcıların üç farklı objeyi karşılaştırarak bağlantı kurmalarını sağlar.
        /// </summary>
        /// <returns></returns>
        private DataTable CreateConnectionTable()
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("PreID", typeof(string));
            dt.Columns.Add("PreName", typeof(string));
            dt.Columns.Add("PreProfile", typeof(string));

            dt.Columns.Add("SecID", typeof(string));
            dt.Columns.Add("SecName", typeof(string));
            dt.Columns.Add("SecProfile", typeof(string));

            dt.Columns.Add("ThirdID", typeof(string));
            dt.Columns.Add("ThirdName", typeof(string));
            dt.Columns.Add("ThirdProfile", typeof(string));

            return dt;
        }

        private DataTable FindColumnGirtConnections (DataTable dtSelected, double toleranceXY)
        {
            DataTable dtResult = CreateConnectionTable();

            var columnRows = dtSelected.AsEnumerable()
               .Where(r => string.Equals(r["Name"]?.ToString()?.Trim(), "COLUMN", StringComparison.OrdinalIgnoreCase))
               .ToList();

            var girtRows = dtSelected.AsEnumerable()
                .Where(r => string.Equals(r["Name"]?.ToString()?.Trim(), "GIRT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (DataRow col in columnRows)
            {
                string colId = col["Identifier"]?.ToString() ?? "";
                string colName = col["Name"]?.ToString() ?? "";
                string colProfile = col["Profile"]?.ToString() ?? "";

                double colStartX = Convert.ToDouble(col["StartX"]);
                double colStartY = Convert.ToDouble(col["StartY"]);
                double colStartZ = Convert.ToDouble(col["StartZ"]);
                double colEndX = Convert.ToDouble(col["EndX"]);
                double colEndY = Convert.ToDouble(col["EndY"]);
                double colEndZ = Convert.ToDouble(col["EndZ"]);

                // Kolon için Z aralığı
                double minZ = Math.Min(colStartZ, colEndZ);
                double maxZ = Math.Max(colStartZ, colEndZ);

                // Kolon mantıken düşey; XY için tek eksen kabul ediyoruz
                // İki noktada çok küçük farklar olabilir diye ortalama alıyoruz
                double colX = (colStartX + colEndX) / 2.0;
                double colY = (colStartY + colEndY) / 2.0;


                // negatif değerler düşünülmeli
                double minX = colX - toleranceXY;
                double maxX = colX + toleranceXY;
                double minY = colY - toleranceXY;
                double maxY = colY + toleranceXY;

                foreach (DataRow girt in girtRows)
                {
                    string girtId = girt["Identifier"]?.ToString() ?? "";
                    string girtName = girt["Name"]?.ToString() ?? "";
                    string girtProfile = girt["Profile"]?.ToString() ?? "";

                    double girtStartX = Convert.ToDouble(girt["StartX"]);
                    double girtStartY = Convert.ToDouble(girt["StartY"]);
                    double girtStartZ = Convert.ToDouble(girt["StartZ"]);
                    double girtEndX = Convert.ToDouble(girt["EndX"]);
                    double girtEndY = Convert.ToDouble(girt["EndY"]);
                    double girtEndZ = Convert.ToDouble(girt["EndZ"]);

                    bool startPointMatches =
                        girtStartX >= minX && girtStartX <= maxX &&
                        girtStartY >= minY && girtStartY <= maxY &&
                        girtStartZ >= minZ && girtStartZ <= maxZ;

                    bool endPointMatches =
                        girtEndX >= minX && girtEndX <= maxX &&
                        girtEndY >= minY && girtEndY <= maxY &&
                        girtEndZ >= minZ && girtEndZ <= maxZ;

                    if (startPointMatches || endPointMatches)
                    {
                        dtResult.Rows.Add(
                            colId,       // PreID
                            colName,     // PreName
                            colProfile,  // PreProfile

                            girtId,      // SecID
                            girtName,    // SecName
                            girtProfile, // SecProfile

                            "",          // ThirdID
                            "",          // ThirdName
                            ""           // ThirdProfile
                        );
                    }
                }
            }

            return dtResult;
        }

        /// <summary>
        /// Bir girt'in yönünü belirler. 
        /// Eğer girt'in X eksenindeki hareketi Y eksenindeki hareketinden büyük veya eşitse, girt'in yönü "X" olarak kabul edilir. 
        /// Aksi takdirde, yön "Y" olarak kabul edilir. Bu yöntem, girt'lerin yatay mı yoksa dikey mi olduğunu anlamak için kullanılır.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private string GetMemberDirection(DataRow row)
        {
            double startX = Convert.ToDouble(row["StartX"]);
            double startY = Convert.ToDouble(row["StartY"]);
            double endX = Convert.ToDouble(row["EndX"]);
            double endY = Convert.ToDouble(row["EndY"]);

            double dx = Math.Abs(endX - startX);
            double dy = Math.Abs(endY - startY);

            return dx >= dy ? "X" : "Y";
        }
        /// <summary>
        /// İki sayının ortalamasını hesaplar. 
        /// Bu yöntem, girt'lerin başlangıç ve bitiş noktalarının ortalamasını alarak, girt'lerin genel konumunu belirlemek için kullanılabilir.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private double GetAvg(double a, double b)
        {
            return (a + b) / 2.0;
        }

        /// <summary>
        /// İki aralığın birbirine ne kadar yakın olduğunu hesaplar.
        /// </summary>
        /// <param name="min1"></param>
        /// <param name="max1"></param>
        /// <param name="min2"></param>
        /// <param name="max2"></param>
        /// <returns></returns>
        private double IntervalGap(double min1, double max1, double min2, double max2)
        {
            if (max1 < min2) return min2 - max1;
            if (max2 < min1) return min1 - max2;
            return 0.0; // çakışıyor veya değiyor
        }
        /// <summary>
        /// Girt çiftleri tablosu oluşturur. 
        /// Bu tablo, birbirine yakın ve aynı yönde olan girt'lerin ID'lerini ve yönlerini içerir.
        /// </summary>
        /// <returns></returns>
        private DataTable CreateGirtPairTable()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Girt1ID", typeof(string));
            dt.Columns.Add("Girt2ID", typeof(string));
            dt.Columns.Add("Direction", typeof(string));
            return dt;
        }

        /// <summary>
        /// Verilen ham veri tablosundan, birbirine yakın ve aynı yönde olan girt çiftlerini bulur.
        /// </summary>
        /// <param name="dtRaw"></param>
        /// <param name="toleranceXY"></param>
        /// <param name="toleranceZ"></param>
        /// <returns></returns>
        private DataTable FindAdjacentGirtPairs(DataTable dtRaw, double toleranceXY, double toleranceZ)
        {
            DataTable dtPairs = CreateGirtPairTable();

            var girtRows = dtRaw.AsEnumerable()
                .Where(r => string.Equals(r["Name"]?.ToString()?.Trim(), "GIRT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            HashSet<string> addedPairs = new HashSet<string>();

            for (int i = 0; i < girtRows.Count; i++)
            {
                DataRow g1 = girtRows[i];
                string g1Id = g1["Identifier"]?.ToString() ?? "";
                string dir1 = GetMemberDirection(g1);

                double g1sx = Convert.ToDouble(g1["StartX"]);
                double g1sy = Convert.ToDouble(g1["StartY"]);
                double g1sz = Convert.ToDouble(g1["StartZ"]);
                double g1ex = Convert.ToDouble(g1["EndX"]);
                double g1ey = Convert.ToDouble(g1["EndY"]);
                double g1ez = Convert.ToDouble(g1["EndZ"]);

                double g1AvgY = GetAvg(g1sy, g1ey);
                double g1AvgX = GetAvg(g1sx, g1ex);
                double g1AvgZ = GetAvg(g1sz, g1ez);

                double g1MinX = Math.Min(g1sx, g1ex);
                double g1MaxX = Math.Max(g1sx, g1ex);
                double g1MinY = Math.Min(g1sy, g1ey);
                double g1MaxY = Math.Max(g1sy, g1ey);

                for (int j = i + 1; j < girtRows.Count; j++)
                {
                    DataRow g2 = girtRows[j];
                    string g2Id = g2["Identifier"]?.ToString() ?? "";
                    string dir2 = GetMemberDirection(g2);

                    if (!string.Equals(dir1, dir2, StringComparison.OrdinalIgnoreCase))
                        continue;

                    double g2sx = Convert.ToDouble(g2["StartX"]);
                    double g2sy = Convert.ToDouble(g2["StartY"]);
                    double g2sz = Convert.ToDouble(g2["StartZ"]);
                    double g2ex = Convert.ToDouble(g2["EndX"]);
                    double g2ey = Convert.ToDouble(g2["EndY"]);
                    double g2ez = Convert.ToDouble(g2["EndZ"]);

                    double g2AvgY = GetAvg(g2sy, g2ey);
                    double g2AvgX = GetAvg(g2sx, g2ex);
                    double g2AvgZ = GetAvg(g2sz, g2ez);

                    double g2MinX = Math.Min(g2sx, g2ex);
                    double g2MaxX = Math.Max(g2sx, g2ex);
                    double g2MinY = Math.Min(g2sy, g2ey);
                    double g2MaxY = Math.Max(g2sy, g2ey);

                    bool isMatch = false;

                    if (dir1 == "X")
                    {
                        // Aynı hatta mı? (Y yakın)
                        bool sameLine = Math.Abs(g1AvgY - g2AvgY) <= toleranceXY;

                        // Aynı seviyede mi? (Z yakın)
                        bool sameLevel = Math.Abs(g1AvgZ - g2AvgZ) <= toleranceZ;

                        // Yana yana mı? X interval'ları arasında küçük bir gap olmalı
                        double gapX = IntervalGap(g1MinX, g1MaxX, g2MinX, g2MaxX);
                        bool adjacent = gapX <= toleranceXY;

                        // Çok üst üste binmişse istemiyorsan bunu daraltabilirsin
                        isMatch = sameLine && sameLevel && adjacent;
                    }
                    else // Y yönlü
                    {
                        bool sameLine = Math.Abs(g1AvgX - g2AvgX) <= toleranceXY;
                        bool sameLevel = Math.Abs(g1AvgZ - g2AvgZ) <= toleranceZ;

                        double gapY = IntervalGap(g1MinY, g1MaxY, g2MinY, g2MaxY);
                        bool adjacent = gapY <= toleranceXY;

                        isMatch = sameLine && sameLevel && adjacent;
                    }

                    if (isMatch)
                    {
                        string pairKey = string.Compare(g1Id, g2Id, StringComparison.Ordinal) < 0
                            ? $"{g1Id}|{g2Id}"
                            : $"{g2Id}|{g1Id}";

                        if (!addedPairs.Contains(pairKey))
                        {
                            addedPairs.Add(pairKey);
                            dtPairs.Rows.Add(g1Id, g2Id, dir1);
                        }
                    }
                }
            }

            return dtPairs;
        }
        /// <summary>
        /// Bağlantı tablosu ile girt çiftleri tablosunu birleştirir.
        /// </summary>
        /// <param name="dtConnections"></param>
        /// <param name="dtPairs"></param>
        /// <returns></returns>
        private DataTable MergePairedGirtsIntoConnectionsSafe(DataTable dtConnections, DataTable dtPairs)
        {
            DataTable result = dtConnections.Clone();

            HashSet<string> pairSet = new HashSet<string>();

            foreach (DataRow pairRow in dtPairs.Rows)
            {
                string g1 = pairRow["Girt1ID"]?.ToString() ?? "";
                string g2 = pairRow["Girt2ID"]?.ToString() ?? "";

                string key = string.Compare(g1, g2, StringComparison.Ordinal) < 0
                    ? $"{g1}|{g2}"
                    : $"{g2}|{g1}";

                pairSet.Add(key);
            }

            var groups = dtConnections.AsEnumerable()
                .GroupBy(r => r["PreID"]?.ToString() ?? "");

            foreach (var group in groups)
            {
                var rows = group.ToList();
                bool[] used = new bool[rows.Count];

                for (int i = 0; i < rows.Count; i++)
                {
                    if (used[i]) continue;

                    DataRow row1 = rows[i];
                    string secId1 = row1["SecID"]?.ToString() ?? "";

                    int matchedIndex = -1;

                    for (int j = i + 1; j < rows.Count; j++)
                    {
                        if (used[j]) continue;

                        DataRow row2 = rows[j];
                        string secId2 = row2["SecID"]?.ToString() ?? "";

                        string pairKey = string.Compare(secId1, secId2, StringComparison.Ordinal) < 0
                            ? $"{secId1}|{secId2}"
                            : $"{secId2}|{secId1}";

                        if (pairSet.Contains(pairKey))
                        {
                            matchedIndex = j;
                            break;
                        }
                    }

                    if (matchedIndex >= 0)
                    {
                        DataRow mergedRow = result.NewRow();

                        // row1'i kopyala
                        mergedRow["PreID"] = row1["PreID"];
                        mergedRow["PreName"] = row1["PreName"];
                        mergedRow["PreProfile"] = row1["PreProfile"];

                        mergedRow["SecID"] = row1["SecID"];
                        mergedRow["SecName"] = row1["SecName"];
                        mergedRow["SecProfile"] = row1["SecProfile"];

                        // row2'yi Third'e yaz
                        DataRow row2 = rows[matchedIndex];
                        mergedRow["ThirdID"] = row2["SecID"];
                        mergedRow["ThirdName"] = row2["SecName"];
                        mergedRow["ThirdProfile"] = row2["SecProfile"];

                        result.Rows.Add(mergedRow);

                        used[i] = true;
                        used[matchedIndex] = true;
                    }
                    else
                    {
                        // eşleşmeyen satırı AYNI HALİYLE koru
                        result.ImportRow(row1);
                        used[i] = true;
                    }
                }
            }

            return result;
        }
        #region PURLIN - BEAM HELPERS

        private struct PurlinPoint3D
        {
            public double X;
            public double Y;
            public double Z;

            public PurlinPoint3D(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private PurlinPoint3D PurlinSubtract(PurlinPoint3D a, PurlinPoint3D b)
        {
            return new PurlinPoint3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        private PurlinPoint3D PurlinAdd(PurlinPoint3D a, PurlinPoint3D b)
        {
            return new PurlinPoint3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        private PurlinPoint3D PurlinMultiply(PurlinPoint3D a, double t)
        {
            return new PurlinPoint3D(a.X * t, a.Y * t, a.Z * t);
        }

        private double PurlinDot(PurlinPoint3D a, PurlinPoint3D b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        private double PurlinDistance(PurlinPoint3D a, PurlinPoint3D b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private double PurlinClamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// İki 3D doğru parçası arasındaki minimum mesafeyi hesaplar.
        /// En kısa mesafe start-end arasında olmak zorunda değildir; member boyunca hesaplanır.
        /// </summary>
        private double GetSegmentToSegmentDistance(
            PurlinPoint3D p1, PurlinPoint3D q1,
            PurlinPoint3D p2, PurlinPoint3D q2)
        {
            const double EPS = 1e-9;

            PurlinPoint3D d1 = PurlinSubtract(q1, p1); // segment 1 direction
            PurlinPoint3D d2 = PurlinSubtract(q2, p2); // segment 2 direction
            PurlinPoint3D r = PurlinSubtract(p1, p2);

            double a = PurlinDot(d1, d1); // squared length of segment 1
            double e = PurlinDot(d2, d2); // squared length of segment 2
            double f = PurlinDot(d2, r);

            double s, t;

            if (a <= EPS && e <= EPS)
            {
                // ikisi de nokta
                return PurlinDistance(p1, p2);
            }

            if (a <= EPS)
            {
                // ilk segment nokta
                s = 0.0;
                t = PurlinClamp(f / e, 0.0, 1.0);
            }
            else
            {
                double c = PurlinDot(d1, r);

                if (e <= EPS)
                {
                    // ikinci segment nokta
                    t = 0.0;
                    s = PurlinClamp(-c / a, 0.0, 1.0);
                }
                else
                {
                    double b = PurlinDot(d1, d2);
                    double denom = a * e - b * b;

                    if (Math.Abs(denom) > EPS)
                    {
                        s = PurlinClamp((b * f - c * e) / denom, 0.0, 1.0);
                    }
                    else
                    {
                        // paralel veya neredeyse paralel
                        s = 0.0;
                    }

                    t = (b * s + f) / e;

                    if (t < 0.0)
                    {
                        t = 0.0;
                        s = PurlinClamp(-c / a, 0.0, 1.0);
                    }
                    else if (t > 1.0)
                    {
                        t = 1.0;
                        s = PurlinClamp((b - c) / a, 0.0, 1.0);
                    }
                }
            }

            PurlinPoint3D closestPointOnSeg1 = PurlinAdd(p1, PurlinMultiply(d1, s));
            PurlinPoint3D closestPointOnSeg2 = PurlinAdd(p2, PurlinMultiply(d2, t));

            return PurlinDistance(closestPointOnSeg1, closestPointOnSeg2);
        }

        /// <summary>
        /// Purlin-Beam connection result DataTable oluşturur.
        /// </summary>
        private DataTable CreateBeamPurlinConnectionTable()
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("PreID", typeof(string));
            dt.Columns.Add("PreName", typeof(string));
            dt.Columns.Add("PreProfile", typeof(string));

            dt.Columns.Add("SecID", typeof(string));
            dt.Columns.Add("SecName", typeof(string));
            dt.Columns.Add("SecProfile", typeof(string));

            dt.Columns.Add("ThirdID", typeof(string));
            dt.Columns.Add("ThirdName", typeof(string));
            dt.Columns.Add("ThirdProfile", typeof(string));

            return dt;
        }

        /// <summary>
        /// Raw DataTable içinden BEAM ve PURLIN elemanlarını alır.
        /// İki member arasındaki 3D minimum mesafe tolerance içindeyse eşleşme kabul edilir.
        /// Sonuçta Pre = BEAM, Sec = PURLIN olacak şekilde yeni DataTable döner.
        /// </summary>
        private DataTable FindBeamPurlinConnections(DataTable dtRaw, double toleranceMm = 200.0)
        {
            DataTable dtResult = CreateBeamPurlinConnectionTable();

            var beamRows = dtRaw.AsEnumerable()
                .Where(r => string.Equals(r["Name"]?.ToString()?.Trim(), "BEAM", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var purlinRows = dtRaw.AsEnumerable()
                .Where(r => string.Equals(r["Name"]?.ToString()?.Trim(), "PURLIN", StringComparison.OrdinalIgnoreCase))
                .ToList();

            HashSet<string> addedMatches = new HashSet<string>();

            foreach (DataRow beam in beamRows)
            {
                string beamId = beam["Identifier"]?.ToString() ?? "";
                string beamName = beam["Name"]?.ToString() ?? "";
                string beamProfile = beam["Profile"]?.ToString() ?? "";

                PurlinPoint3D beamStart = new PurlinPoint3D(
                    Convert.ToDouble(beam["StartX"]),
                    Convert.ToDouble(beam["StartY"]),
                    Convert.ToDouble(beam["StartZ"])
                );

                PurlinPoint3D beamEnd = new PurlinPoint3D(
                    Convert.ToDouble(beam["EndX"]),
                    Convert.ToDouble(beam["EndY"]),
                    Convert.ToDouble(beam["EndZ"])
                );

                foreach (DataRow purlin in purlinRows)
                {
                    string purlinId = purlin["Identifier"]?.ToString() ?? "";
                    string purlinName = purlin["Name"]?.ToString() ?? "";
                    string purlinProfile = purlin["Profile"]?.ToString() ?? "";

                    PurlinPoint3D purlinStart = new PurlinPoint3D(
                        Convert.ToDouble(purlin["StartX"]),
                        Convert.ToDouble(purlin["StartY"]),
                        Convert.ToDouble(purlin["StartZ"])
                    );

                    PurlinPoint3D purlinEnd = new PurlinPoint3D(
                        Convert.ToDouble(purlin["EndX"]),
                        Convert.ToDouble(purlin["EndY"]),
                        Convert.ToDouble(purlin["EndZ"])
                    );

                    double minDistance = GetSegmentToSegmentDistance(
                        beamStart, beamEnd,
                        purlinStart, purlinEnd
                    );

                    if (minDistance <= toleranceMm)
                    {
                        string key = beamId + "|" + purlinId;

                        if (!addedMatches.Contains(key))
                        {
                            addedMatches.Add(key);

                            dtResult.Rows.Add(
                                beamId,         // PreID
                                beamName,       // PreName
                                beamProfile,    // PreProfile

                                purlinId,       // SecID
                                purlinName,     // SecName
                                purlinProfile,  // SecProfile

                                "",             // ThirdID
                                "",             // ThirdName
                                ""              // ThirdProfile
                            );
                        }
                    }
                }
            }

            return dtResult;
        }

        #endregion

        #region 2 PURLIN - 1 BEAM

        /// <summary>
        /// PURLIN pair tablosu oluşturur.
        /// </summary>
        private DataTable CreatePurlinPairTable()
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("Purlin1ID", typeof(string));
            dt.Columns.Add("Purlin2ID", typeof(string));
            dt.Columns.Add("Direction", typeof(string));

            return dt;
        }

        /// <summary>
        /// Verilen member'ın baskın doğrultusunu döner. X veya Y.
        /// GIRT tarafındaki mantığın aynısı.
        /// </summary>
        private string GetMemberDirectionForPurlin(DataRow row)
        {
            double startX = Convert.ToDouble(row["StartX"]);
            double startY = Convert.ToDouble(row["StartY"]);
            double endX = Convert.ToDouble(row["EndX"]);
            double endY = Convert.ToDouble(row["EndY"]);

            double dx = Math.Abs(endX - startX);
            double dy = Math.Abs(endY - startY);

            return dx >= dy ? "X" : "Y";
        }

        private double GetAverageValue(double a, double b)
        {
            return (a + b) / 2.0;
        }

        private double GetIntervalGap(double min1, double max1, double min2, double max2)
        {
            if (max1 < min2) return min2 - max1;
            if (max2 < min1) return min1 - max2;
            return 0.0;
        }

        /// <summary>
        /// Raw datadaki PURLIN'leri, GIRT ile aynı mantıkta pair eder.
        /// Aynı doğrultuda, aynı hatta ve tolerance içinde yana yana olanları bulur.
        /// </summary>
        private DataTable FindAdjacentPurlinPairs(DataTable dtRaw, double toleranceXY, double toleranceZ)
        {
            DataTable dtPairs = CreatePurlinPairTable();

            var purlinRows = dtRaw.AsEnumerable()
                .Where(r => string.Equals(r["Name"]?.ToString()?.Trim(), "PURLIN", StringComparison.OrdinalIgnoreCase))
                .ToList();

            HashSet<string> addedPairs = new HashSet<string>();

            for (int i = 0; i < purlinRows.Count; i++)
            {
                DataRow p1 = purlinRows[i];
                string p1Id = p1["Identifier"]?.ToString() ?? "";
                string dir1 = GetMemberDirectionForPurlin(p1);

                double p1sx = Convert.ToDouble(p1["StartX"]);
                double p1sy = Convert.ToDouble(p1["StartY"]);
                double p1sz = Convert.ToDouble(p1["StartZ"]);
                double p1ex = Convert.ToDouble(p1["EndX"]);
                double p1ey = Convert.ToDouble(p1["EndY"]);
                double p1ez = Convert.ToDouble(p1["EndZ"]);

                double p1AvgY = GetAverageValue(p1sy, p1ey);
                double p1AvgX = GetAverageValue(p1sx, p1ex);
                double p1AvgZ = GetAverageValue(p1sz, p1ez);

                double p1MinX = Math.Min(p1sx, p1ex);
                double p1MaxX = Math.Max(p1sx, p1ex);
                double p1MinY = Math.Min(p1sy, p1ey);
                double p1MaxY = Math.Max(p1sy, p1ey);

                for (int j = i + 1; j < purlinRows.Count; j++)
                {
                    DataRow p2 = purlinRows[j];
                    string p2Id = p2["Identifier"]?.ToString() ?? "";
                    string dir2 = GetMemberDirectionForPurlin(p2);

                    if (!string.Equals(dir1, dir2, StringComparison.OrdinalIgnoreCase))
                        continue;

                    double p2sx = Convert.ToDouble(p2["StartX"]);
                    double p2sy = Convert.ToDouble(p2["StartY"]);
                    double p2sz = Convert.ToDouble(p2["StartZ"]);
                    double p2ex = Convert.ToDouble(p2["EndX"]);
                    double p2ey = Convert.ToDouble(p2["EndY"]);
                    double p2ez = Convert.ToDouble(p2["EndZ"]);

                    double p2AvgY = GetAverageValue(p2sy, p2ey);
                    double p2AvgX = GetAverageValue(p2sx, p2ex);
                    double p2AvgZ = GetAverageValue(p2sz, p2ez);

                    double p2MinX = Math.Min(p2sx, p2ex);
                    double p2MaxX = Math.Max(p2sx, p2ex);
                    double p2MinY = Math.Min(p2sy, p2ey);
                    double p2MaxY = Math.Max(p2sy, p2ey);

                    bool isMatch = false;

                    if (dir1 == "X")
                    {
                        bool sameLine = Math.Abs(p1AvgY - p2AvgY) <= toleranceXY;
                        bool sameLevel = Math.Abs(p1AvgZ - p2AvgZ) <= toleranceZ;

                        double gapX = GetIntervalGap(p1MinX, p1MaxX, p2MinX, p2MaxX);
                        bool adjacent = gapX <= toleranceXY;

                        isMatch = sameLine && sameLevel && adjacent;
                    }
                    else // Y
                    {
                        bool sameLine = Math.Abs(p1AvgX - p2AvgX) <= toleranceXY;
                        bool sameLevel = Math.Abs(p1AvgZ - p2AvgZ) <= toleranceZ;

                        double gapY = GetIntervalGap(p1MinY, p1MaxY, p2MinY, p2MaxY);
                        bool adjacent = gapY <= toleranceXY;

                        isMatch = sameLine && sameLevel && adjacent;
                    }

                    if (isMatch)
                    {
                        string pairKey = string.Compare(p1Id, p2Id, StringComparison.Ordinal) < 0
                            ? $"{p1Id}|{p2Id}"
                            : $"{p2Id}|{p1Id}";

                        if (!addedPairs.Contains(pairKey))
                        {
                            addedPairs.Add(pairKey);
                            dtPairs.Rows.Add(p1Id, p2Id, dir1);
                        }
                    }
                }
            }

            return dtPairs;
        }

        /// <summary>
        /// Aynı BEAM'e bağlı ve pair olan iki PURLIN'i tek satırda birleştirir.
        /// Eşleşmeyen satırlar aynen korunur.
        /// </summary>
        private DataTable MergePairedPurlinsIntoConnectionsSafe(DataTable dtConnections, DataTable dtPairs)
        {
            DataTable result = dtConnections.Clone();

            HashSet<string> pairSet = new HashSet<string>();

            foreach (DataRow pairRow in dtPairs.Rows)
            {
                string p1 = pairRow["Purlin1ID"]?.ToString() ?? "";
                string p2 = pairRow["Purlin2ID"]?.ToString() ?? "";

                string key = string.Compare(p1, p2, StringComparison.Ordinal) < 0
                    ? $"{p1}|{p2}"
                    : $"{p2}|{p1}";

                pairSet.Add(key);
            }

            var groups = dtConnections.AsEnumerable()
                .GroupBy(r => r["PreID"]?.ToString() ?? "");

            foreach (var group in groups)
            {
                var rows = group.ToList();
                bool[] used = new bool[rows.Count];

                for (int i = 0; i < rows.Count; i++)
                {
                    if (used[i]) continue;

                    DataRow row1 = rows[i];
                    string secId1 = row1["SecID"]?.ToString() ?? "";

                    int matchedIndex = -1;

                    for (int j = i + 1; j < rows.Count; j++)
                    {
                        if (used[j]) continue;

                        DataRow row2 = rows[j];
                        string secId2 = row2["SecID"]?.ToString() ?? "";

                        string pairKey = string.Compare(secId1, secId2, StringComparison.Ordinal) < 0
                            ? $"{secId1}|{secId2}"
                            : $"{secId2}|{secId1}";

                        if (pairSet.Contains(pairKey))
                        {
                            matchedIndex = j;
                            break;
                        }
                    }

                    if (matchedIndex >= 0)
                    {
                        DataRow mergedRow = result.NewRow();

                        mergedRow["PreID"] = row1["PreID"];
                        mergedRow["PreName"] = row1["PreName"];
                        mergedRow["PreProfile"] = row1["PreProfile"];

                        mergedRow["SecID"] = row1["SecID"];
                        mergedRow["SecName"] = row1["SecName"];
                        mergedRow["SecProfile"] = row1["SecProfile"];

                        DataRow row2 = rows[matchedIndex];
                        mergedRow["ThirdID"] = row2["SecID"];
                        mergedRow["ThirdName"] = row2["SecName"];
                        mergedRow["ThirdProfile"] = row2["SecProfile"];

                        result.Rows.Add(mergedRow);

                        used[i] = true;
                        used[matchedIndex] = true;
                    }
                    else
                    {
                        result.ImportRow(row1);
                        used[i] = true;
                    }
                }
            }

            return result;
        }

        #endregion

        /// <summary>
        /// DataGridView'deki hücre tıklama olayını işler. Sadece 1, 4 ve 7. kolonlar için geçerlidir (ID kolonları).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void advancedDataGridView2_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // Geçersiz satır/kolon kontrolü
                if (e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;

                // Sadece 1, 4 ve 7. kolonlar için çalışsın
                // DataGridView indexleri 0'dan başladığı için:
                // 1. kolon = 0
                // 4. kolon = 3
                // 7. kolon = 6
                if (e.ColumnIndex != 0 && e.ColumnIndex != 3 && e.ColumnIndex != 6)
                    return;

                if (!_model.GetConnectionStatus())
                {
                    MessageBox.Show("Tekla model bağlantısı kurulamadı.");
                    return;
                }

                object cellValue = advancedDataGridView2.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

                if (cellValue == null || string.IsNullOrWhiteSpace(cellValue.ToString()))
                    return;

                // ID parse et
                if (!int.TryParse(cellValue.ToString(), out int teklaId))
                {
                    MessageBox.Show("Geçerli bir Tekla ID değeri bulunamadı.");
                    return;
                }

                // Identifier oluştur
                Identifier identifier = new Identifier(teklaId);

                // Model objesini al
                ModelObject modelObject = _model.SelectModelObject(identifier);

                if (modelObject == null)
                {
                    MessageBox.Show($"ID={teklaId} için model objesi bulunamadı.");
                    return;
                }

                // Modelde seç
                ArrayList objectList = new ArrayList();
                objectList.Add(modelObject);

                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                selector.Select(objectList);

                // İstersen görünümde yaklaştırma için bunu da ekleyebilirsin:
                // Tekla.Structures.Model.Operations.Operation.ShowOnlySelected();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        /// <summary>
        /// DataGridView'deki hücre tıklama olayını işler. Sadece 1, 4 ve 7. kolonlar için geçerlidir (ID kolonları).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void advancedDataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // Geçersiz satır/kolon kontrolü
                if (e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;

                // Sadece 1, 4 ve 7. kolonlar için çalışsın
                // DataGridView indexleri 0'dan başladığı için:
                // 1. kolon = 0
                // 4. kolon = 3
                // 7. kolon = 6
                if (e.ColumnIndex != 0 && e.ColumnIndex != 3 && e.ColumnIndex != 6)
                    return;

                if (!_model.GetConnectionStatus())
                {
                    MessageBox.Show("Tekla model bağlantısı kurulamadı.");
                    return;
                }

                object cellValue = advancedDataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

                if (cellValue == null || string.IsNullOrWhiteSpace(cellValue.ToString()))
                    return;

                // ID parse et
                if (!int.TryParse(cellValue.ToString(), out int teklaId))
                {
                    MessageBox.Show("Geçerli bir Tekla ID değeri bulunamadı.");
                    return;
                }

                // Identifier oluştur
                Identifier identifier = new Identifier(teklaId);

                // Model objesini al
                ModelObject modelObject = _model.SelectModelObject(identifier);

                if (modelObject == null)
                {
                    MessageBox.Show($"ID={teklaId} için model objesi bulunamadı.");
                    return;
                }

                // Modelde seç
                ArrayList objectList = new ArrayList();
                objectList.Add(modelObject);

                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                selector.Select(objectList);

                // İstersen görünümde yaklaştırma için bunu da ekleyebilirsin:
                // Tekla.Structures.Model.Operations.Operation.ShowOnlySelected();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        /// <summary>
        /// Verilen DataTable'a "MacroNumber" adında bir kolon ekler ve tüm satırlara belirtilen macroNumber değerini atar.
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="macroNumber"></param>
        private void AddMacroNumberColumn(DataTable dt, int macroNumber)
        {
            if(!dt.Columns.Contains("MacroNumber"))
            {
                dt.Columns.Add("MacroNumber", typeof(int));
            }

            foreach (DataRow row in dt.Rows)
            {
                row["MacroNumber"] = macroNumber;
            }
        }
        #region GIRT-COLUMN RELATION

        private struct RelationVector3
        {
            public double X;
            public double Y;
            public double Z;

            public RelationVector3(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private RelationVector3 CreateVectorFromRow(DataRow row, string xCol, string yCol, string zCol)
        {
            return new RelationVector3(
                Convert.ToDouble(row[xCol]),
                Convert.ToDouble(row[yCol]),
                Convert.ToDouble(row[zCol])
            );
        }

        private RelationVector3 SubtractRelation(RelationVector3 a, RelationVector3 b)
        {
            return new RelationVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        private double DotRelation(RelationVector3 a, RelationVector3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        private double LengthRelation(RelationVector3 v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }

        private RelationVector3 NormalizeRelation(RelationVector3 v)
        {
            double len = LengthRelation(v);

            if (len < 1e-9)
                return new RelationVector3(0, 0, 0);

            return new RelationVector3(v.X / len, v.Y / len, v.Z / len);
        }

        private DataRow FindRowByIdentifier(DataTable dt, string id)
        {
            return dt.AsEnumerable()
                     .FirstOrDefault(r => string.Equals(r["Identifier"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));
        }

        private string GetParallelPerpendicularStatus(RelationVector3 memberDir, RelationVector3 axis, double parallelLimit = 0.85, double perpendicularLimit = 0.15)
        {
            double dot = Math.Abs(DotRelation(NormalizeRelation(memberDir), NormalizeRelation(axis)));

            if (dot >= parallelLimit)
                return "PARALLEL";

            if (dot <= perpendicularLimit)
                return "PERPENDICULAR";

            return "ANGLED";
        }

        /// <summary>
        /// dtMergedGirtConnections tablosuna yeni kolon ekler.
        /// Girt'in, column local AxisY yönüne göre PARALLEL / PERPENDICULAR / ANGLED bilgisini yazar.
        /// </summary>
        private void AddGirtColumnRelationColumn(DataTable dtMergedGirtConnections, DataTable dtLocalInfo)
        {
            if (!dtMergedGirtConnections.Columns.Contains("GirtToColumnAxisY"))
            {
                dtMergedGirtConnections.Columns.Add("GirtToColumnAxisY", typeof(string));
            }

            foreach (DataRow row in dtMergedGirtConnections.Rows)
            {
                string columnId = row["PreID"]?.ToString() ?? "";
                string girtId = row["SecID"]?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(columnId) || string.IsNullOrWhiteSpace(girtId))
                {
                    row["GirtToColumnAxisY"] = "";
                    continue;
                }

                DataRow columnRow = FindRowByIdentifier(dtLocalInfo, columnId);
                DataRow girtRow = FindRowByIdentifier(dtLocalInfo, girtId);

                if (columnRow == null || girtRow == null)
                {
                    row["GirtToColumnAxisY"] = "";
                    continue;
                }

                RelationVector3 girtStart = CreateVectorFromRow(girtRow, "StartX", "StartY", "StartZ");
                RelationVector3 girtEnd = CreateVectorFromRow(girtRow, "EndX", "EndY", "EndZ");
                RelationVector3 girtDir = NormalizeRelation(SubtractRelation(girtEnd, girtStart));

                RelationVector3 columnAxisY = CreateVectorFromRow(columnRow, "AxisY_X", "AxisY_Y", "AxisY_Z");
                columnAxisY = NormalizeRelation(columnAxisY);

                row["GirtToColumnAxisY"] = GetParallelPerpendicularStatus(girtDir, columnAxisY);
            }
        }

        #endregion

        #region GIRT-COLUMN CALCULATED VALUE

        private int ExtractFirstNumberFromProfile(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
                return 0;

            Match match = Regex.Match(profile, @"\d+");

            if (match.Success && int.TryParse(match.Value, out int number))
                return number;

            return 0;
        }

        private double GetSegmentToSegmentDistanceForGirtColumn(
            double p1x, double p1y, double p1z,
            double q1x, double q1y, double q1z,
            double p2x, double p2y, double p2z,
            double q2x, double q2y, double q2z)
        {
            const double EPS = 1e-9;

            double d1x = q1x - p1x;
            double d1y = q1y - p1y;
            double d1z = q1z - p1z;

            double d2x = q2x - p2x;
            double d2y = q2y - p2y;
            double d2z = q2z - p2z;

            double rx = p1x - p2x;
            double ry = p1y - p2y;
            double rz = p1z - p2z;

            double a = d1x * d1x + d1y * d1y + d1z * d1z;
            double e = d2x * d2x + d2y * d2y + d2z * d2z;
            double f = d2x * rx + d2y * ry + d2z * rz;

            double s, t;

            if (a <= EPS && e <= EPS)
            {
                return Math.Sqrt(rx * rx + ry * ry + rz * rz);
            }

            if (a <= EPS)
            {
                s = 0.0;
                t = ClampValue(f / e, 0.0, 1.0);
            }
            else
            {
                double c = d1x * rx + d1y * ry + d1z * rz;

                if (e <= EPS)
                {
                    t = 0.0;
                    s = ClampValue(-c / a, 0.0, 1.0);
                }
                else
                {
                    double b = d1x * d2x + d1y * d2y + d1z * d2z;
                    double denom = a * e - b * b;

                    if (Math.Abs(denom) > EPS)
                        s = ClampValue((b * f - c * e) / denom, 0.0, 1.0);
                    else
                        s = 0.0;

                    t = (b * s + f) / e;

                    if (t < 0.0)
                    {
                        t = 0.0;
                        s = ClampValue(-c / a, 0.0, 1.0);
                    }
                    else if (t > 1.0)
                    {
                        t = 1.0;
                        s = ClampValue((b - c) / a, 0.0, 1.0);
                    }
                }
            }

            double c1x = p1x + d1x * s;
            double c1y = p1y + d1y * s;
            double c1z = p1z + d1z * s;

            double c2x = p2x + d2x * t;
            double c2y = p2y + d2y * t;
            double c2z = p2z + d2z * t;

            double dx = c1x - c2x;
            double dy = c1y - c2y;
            double dz = c1z - c2z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private double ClampValue(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// dtMergedGirtConnections tablosuna yeni kolon ekler.
        /// PERPENDICULAR ise:
        /// MinDistance + GirtProfileFirstNumber - ColumnProfileFirstNumber
        /// PARALLEL ise:
        /// MinDistance + GirtProfileFirstNumber
        /// Sonuç tam sayı olarak yazılır.
        /// </summary>
        private void AddGirtColumnFinalValueColumn(DataTable dtMergedGirtConnections, DataTable dtLocalInfo)
        {
            if (!dtMergedGirtConnections.Columns.Contains("FinalValue"))
            {
                dtMergedGirtConnections.Columns.Add("FinalValue", typeof(int));
            }

            foreach (DataRow row in dtMergedGirtConnections.Rows)
            {
                string columnId = row["PreID"]?.ToString() ?? "";
                string girtId = row["SecID"]?.ToString() ?? "";
                string relation = row["GirtToColumnAxisY"]?.ToString()?.Trim().ToUpper() ?? "";

                if (string.IsNullOrWhiteSpace(columnId) || string.IsNullOrWhiteSpace(girtId))
                {
                    row["FinalValue"] = 0;
                    continue;
                }

                DataRow columnRow = FindRowByIdentifier(dtLocalInfo, columnId);
                DataRow girtRow = FindRowByIdentifier(dtLocalInfo, girtId);

                if (columnRow == null || girtRow == null)
                {
                    row["FinalValue"] = 0;
                    continue;
                }

                string columnProfile = columnRow["Profile"]?.ToString() ?? "";
                string girtProfile = girtRow["Profile"]?.ToString() ?? "";

                int columnNumber = ExtractFirstNumberFromProfile(columnProfile);
                int girtNumber = ExtractFirstNumberFromProfile(girtProfile);

                double minDistance = GetSegmentToSegmentDistanceForGirtColumn(
                    Convert.ToDouble(columnRow["StartX"]),
                    Convert.ToDouble(columnRow["StartY"]),
                    Convert.ToDouble(columnRow["StartZ"]),
                    Convert.ToDouble(columnRow["EndX"]),
                    Convert.ToDouble(columnRow["EndY"]),
                    Convert.ToDouble(columnRow["EndZ"]),

                    Convert.ToDouble(girtRow["StartX"]),
                    Convert.ToDouble(girtRow["StartY"]),
                    Convert.ToDouble(girtRow["StartZ"]),
                    Convert.ToDouble(girtRow["EndX"]),
                    Convert.ToDouble(girtRow["EndY"]),
                    Convert.ToDouble(girtRow["EndZ"])
                );

                int finalValue = 0;

                if (relation == "PERPENDICULAR")
                {
                    finalValue = (int)Math.Round(minDistance + girtNumber/2 - columnNumber/2, 0);
                }
                else if (relation == "PARALLEL")
                {
                    finalValue = (int)Math.Round(minDistance + girtNumber/2, 0);
                }
                else
                {
                    finalValue = (int)Math.Round(minDistance, 0);
                }

                row["FinalValue"] = finalValue;
            }
        }

        #endregion
        #region GIRT ATTRIBUTE NAME

        private string GetSectionPrefix(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
                return "";

            string trimmedProfile = profile.Trim();

            // Başlangıçta CC veya Z ile başlayan profilleri UPN'e çevir
            // CC200-80-20 -> UPN200
            // Z180-2.5    -> UPN180
            if (trimmedProfile.StartsWith("CC", StringComparison.OrdinalIgnoreCase) ||
                trimmedProfile.StartsWith("Z", StringComparison.OrdinalIgnoreCase))
            {
                Match numberMatch = Regex.Match(trimmedProfile, @"\d+");
                if (numberMatch.Success)
                    return "UNP" + numberMatch.Value;

                return "UNP";
            }

            // Diğer profiller için mevcut mantık devam etsin
            Match match = Regex.Match(trimmedProfile, @"^[A-Za-z]+(\d+)");
            if (match.Success)
                return match.Value;

            // Eğer başta harf+sayı formatı yakalanmazsa fallback olarak ilk token
            string[] parts = trimmedProfile.Split('-');
            if (parts.Length > 0)
                return parts[0];

            return trimmedProfile;
        }

        private int GetLengthCategory(int finalValue)
        {
            if (finalValue <= 500)
                return 500;

            if (finalValue > 500 && finalValue <= 750)
                return 750;

            return 1000;
        }

        /// <summary>
        /// Girt datasına AttributeName kolonu ekler.
        /// Format:
        /// GC-CON-{Section}-L_{Length}.{MacroNumber}
        /// </summary>
        private void AddGirtAttributeNameColumn(DataTable dtMergedGirtConnections)
        {
            if (!dtMergedGirtConnections.Columns.Contains("AttributeName"))
            {
                dtMergedGirtConnections.Columns.Add("AttributeName", typeof(string));
            }

            foreach (DataRow row in dtMergedGirtConnections.Rows)
            {
                string secProfile = row["SecProfile"]?.ToString()?.Trim() ?? "";
                string thirdProfile = row["ThirdProfile"]?.ToString()?.Trim() ?? "";

                int secNumber = ExtractFirstNumberFromProfile(secProfile);
                int thirdNumber = ExtractFirstNumberFromProfile(thirdProfile);

                string selectedProfile = "";

                if (!string.IsNullOrWhiteSpace(secProfile) && !string.IsNullOrWhiteSpace(thirdProfile))
                {
                    selectedProfile = secNumber >= thirdNumber ? secProfile : thirdProfile;
                }
                else if (!string.IsNullOrWhiteSpace(secProfile))
                {
                    selectedProfile = secProfile;
                }
                else if (!string.IsNullOrWhiteSpace(thirdProfile))
                {
                    selectedProfile = thirdProfile;
                }

                string sectionText = GetSectionPrefix(selectedProfile);

                int finalValue = 0;
                if (dtMergedGirtConnections.Columns.Contains("FinalValue"))
                {
                    int.TryParse(row["FinalValue"]?.ToString(), out finalValue);
                }

                int lengthCategory = GetLengthCategory(finalValue);

                string macroNumberText = row["MacroNumber"]?.ToString()?.Trim() ?? "";

                string attributeName = $"GC-CON-{sectionText}-L_{lengthCategory}";
                row["AttributeName"] = attributeName;
            }
        }

        #endregion
        private void advancedDataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (advancedDataGridView1.IsCurrentCellDirty)
            {
                advancedDataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }
        private void UpdateSingleGirtAttributeName(DataRow row)
        {
            string secProfile = row["SecProfile"]?.ToString()?.Trim() ?? "";
            string thirdProfile = row["ThirdProfile"]?.ToString()?.Trim() ?? "";

            int secNumber = ExtractFirstNumberFromProfile(secProfile);
            int thirdNumber = ExtractFirstNumberFromProfile(thirdProfile);

            string selectedProfile = "";

            if (!string.IsNullOrWhiteSpace(secProfile) && !string.IsNullOrWhiteSpace(thirdProfile))
                selectedProfile = secNumber >= thirdNumber ? secProfile : thirdProfile;
            else if (!string.IsNullOrWhiteSpace(secProfile))
                selectedProfile = secProfile;
            else if (!string.IsNullOrWhiteSpace(thirdProfile))
                selectedProfile = thirdProfile;

            string sectionText = GetSectionPrefix(selectedProfile);

            int finalValue = 0;
            int.TryParse(row["FinalValue"]?.ToString(), out finalValue);

            int lengthCategory = GetLengthCategory(finalValue);

            string macroNumberText = row["MacroNumber"]?.ToString()?.Trim() ?? "";

            string attributeName = $"GC-CON-{sectionText}-L_{lengthCategory}";

            if (!string.IsNullOrWhiteSpace(macroNumberText))
            {
                attributeName += $".j{macroNumberText}";
            }

            row["AttributeName"] = attributeName;
        }
        private void advancedDataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string columnName = advancedDataGridView1.Columns[e.ColumnIndex].Name;

            // AttributeName'i etkileyen kolonlardan biri değiştiyse yeniden hesapla
            if (columnName == "SecProfile" ||
                columnName == "ThirdProfile" ||
                columnName == "FinalValue" ||
                columnName == "MacroNumber")
            {
                DataRowView rowView = advancedDataGridView1.Rows[e.RowIndex].DataBoundItem as DataRowView;

                if (rowView != null)
                {
                    UpdateSingleGirtAttributeName(rowView.Row);
                }
            }
        }
        #region PURLIN ATTRIBUTE NAME

        private void AddPurlinAttributeNameColumn(DataTable dtMergedPurlinConnections)
        {
            if (!dtMergedPurlinConnections.Columns.Contains("AttributeName"))
            {
                dtMergedPurlinConnections.Columns.Add("AttributeName", typeof(string));
            }

            foreach (DataRow row in dtMergedPurlinConnections.Rows)
            {
                string secProfile = row["SecProfile"]?.ToString()?.Trim() ?? "";
                string thirdProfile = row["ThirdProfile"]?.ToString()?.Trim() ?? "";

                int secNumber = ExtractFirstNumberFromProfile(secProfile);
                int thirdNumber = ExtractFirstNumberFromProfile(thirdProfile);

                string selectedProfile = "";

                if (!string.IsNullOrWhiteSpace(secProfile) && !string.IsNullOrWhiteSpace(thirdProfile))
                {
                    selectedProfile = secNumber >= thirdNumber ? secProfile : thirdProfile;
                }
                else if (!string.IsNullOrWhiteSpace(secProfile))
                {
                    selectedProfile = secProfile;
                }
                else if (!string.IsNullOrWhiteSpace(thirdProfile))
                {
                    selectedProfile = thirdProfile;
                }

                string sectionText = GetSectionPrefix(selectedProfile);
                string macroNumberText = row["MacroNumber"]?.ToString()?.Trim() ?? "";

                string attributeName = $"PB-CON-{sectionText}";
                row["AttributeName"] = attributeName;
            }
        }

        #endregion
        private void advancedDataGridView2_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (advancedDataGridView2.IsCurrentCellDirty)
            {
                advancedDataGridView2.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }
        private void UpdateSinglePurlinAttributeName(DataRow row)
        {
            string secProfile = row["SecProfile"]?.ToString()?.Trim() ?? "";
            string thirdProfile = row["ThirdProfile"]?.ToString()?.Trim() ?? "";

            int secNumber = ExtractFirstNumberFromProfile(secProfile);
            int thirdNumber = ExtractFirstNumberFromProfile(thirdProfile);

            string selectedProfile = "";

            if (!string.IsNullOrWhiteSpace(secProfile) && !string.IsNullOrWhiteSpace(thirdProfile))
            {
                selectedProfile = secNumber >= thirdNumber ? secProfile : thirdProfile;
            }
            else if (!string.IsNullOrWhiteSpace(secProfile))
            {
                selectedProfile = secProfile;
            }
            else if (!string.IsNullOrWhiteSpace(thirdProfile))
            {
                selectedProfile = thirdProfile;
            }

            string sectionText = GetSectionPrefix(selectedProfile);
            string macroNumberText = row["MacroNumber"]?.ToString()?.Trim() ?? "";

            string attributeName = $"PB-CON-{sectionText}";

            if (!string.IsNullOrWhiteSpace(macroNumberText))
            {
                attributeName += $".j{macroNumberText}";
            }

            row["AttributeName"] = attributeName;
        }
        private void advancedDataGridView2_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string columnName = advancedDataGridView2.Columns[e.ColumnIndex].Name;

            if (columnName == "SecProfile" ||
                columnName == "ThirdProfile" ||
                columnName == "MacroNumber")
            {
                DataRowView rowView = advancedDataGridView2.Rows[e.RowIndex].DataBoundItem as DataRowView;

                if (rowView != null)
                {
                    UpdateSinglePurlinAttributeName(rowView.Row);
                }
            }
        }
        private void ApplyExcelStyle(DataGridView dgv)
        {
            System.Drawing.Color headerColor = ColorTranslator.FromHtml("#BFAC88");
            System.Drawing.Color rowColor1 = ColorTranslator.FromHtml("#F2F0EB");
            System.Drawing.Color rowColor2 = ColorTranslator.FromHtml("#D9C39A");

            // Header style
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = headerColor;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.Black;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Row style (zebra)
            dgv.RowsDefaultCellStyle.BackColor = rowColor1;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = rowColor2;

            // Text
            dgv.DefaultCellStyle.ForeColor = System.Drawing.Color.Black;
            dgv.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;

            // Selection (hafif koyu ton)
            dgv.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#BFAC88");

            // Grid görünümü
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = System.Drawing.Color.White;

            // Satır yüksekliği
            dgv.RowTemplate.Height = 28;

            // Kolon auto size
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;

            // Header height
            dgv.ColumnHeadersHeight = 32;
        }

        #region INSERT CONNECTIONS

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                var grids = new List<DataGridView>
                    {
                        advancedDataGridView1,
                        advancedDataGridView2
                    };

                grids = grids.Where(g => g != null).ToList();

                if (grids.Count == 0)
                {
                    MessageBox.Show("İşlenecek grid bulunamadı.");
                    return;
                }

                foreach (var grid in grids)
                    CommitGridEdits(grid);

                int totalOk = 0;
                int totalFail = 0;
                int totalSkipped = 0;

                foreach (var grid in grids)
                {
                    var result = InsertConnectionsFromGridForCurrentProject(grid);
                    totalOk += result.OK;
                    totalFail += result.FAIL;
                    totalSkipped += result.SKIPPED;
                }

                MessageBox.Show(
                    $"Başarılı: {totalOk}\nBaşarısız: {totalFail}\nAtlanan: {totalSkipped}",
                    "İşlem Sonucu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }
        private void CommitGridEdits(DataGridView grid)
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            grid.EndEdit();

            if (grid.DataSource != null)
            {
                var bc = grid.FindForm()?.BindingContext;
                if (bc != null)
                {
                    var cm = bc[grid.DataSource] as CurrencyManager;
                    cm?.EndCurrentEdit();
                }
            }

            grid.Refresh();
        }
        private InsertSummary InsertConnectionsFromGridForCurrentProject(DataGridView grid)
        {
            var model = new Model();
            if (!model.GetConnectionStatus())
            {
                MessageBox.Show("Tekla model connection is not available.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new InsertSummary { OK = 0, FAIL = 0, SKIPPED = 0 };
            }

            int ok = 0, fail = 0, skipped = 0;
            StringBuilder log = new StringBuilder();

            for (int r = 0; r < grid.Rows.Count; r++)
            {
                var row = grid.Rows[r];
                if (row.IsNewRow) continue;

                string attributeName = Convert.ToString(row.Cells["AttributeName"]?.Value)?.Trim() ?? "";
                int macroNumber = SafeInt(row.Cells["MacroNumber"]?.Value);
                string checkStatus = grid.Columns.Contains("CheckStatus")
                    ? Convert.ToString(row.Cells["CheckStatus"]?.Value)?.Trim() ?? ""
                    : "";

                if (checkStatus == "X")
                {
                    skipped++;
                    log.AppendLine($"Row {r + 1}: SKIPPED - Attribute file not found. AttributeName={attributeName}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(attributeName) || macroNumber <= 0)
                {
                    skipped++;
                    log.AppendLine($"Row {r + 1}: SKIPPED - Invalid AttributeName or MacroNumber.");
                    continue;
                }

                if (!int.TryParse(Convert.ToString(row.Cells["PreID"]?.Value), out int preId) ||
                    !int.TryParse(Convert.ToString(row.Cells["SecID"]?.Value), out int secId))
                {
                    fail++;
                    log.AppendLine($"Row {r + 1}: FAIL - PreID or SecID invalid.");
                    continue;
                }

                int thirdId = 0;
                bool hasThird = grid.Columns.Contains("ThirdID") &&
                                int.TryParse(Convert.ToString(row.Cells["ThirdID"]?.Value), out thirdId) &&
                                thirdId > 0;

                var primary = model.SelectModelObject(new Identifier(preId)) as Part;
                var secondary = model.SelectModelObject(new Identifier(secId)) as Part;
                var third = hasThird ? model.SelectModelObject(new Identifier(thirdId)) as Part : null;

                if (primary == null || secondary == null)
                {
                    fail++;
                    log.AppendLine($"Row {r + 1}: FAIL - Primary or Secondary part not found.");
                    continue;
                }

                try
                {
                    bool inserted = false;

                    switch (macroNumber)
                    {
                        case 70:
                            inserted = hasThird
                                ? Insert70_ThreeParts(primary, secondary, third, attributeName, r + 1, log)
                                : Insert70_TwoParts(primary, secondary, attributeName, r + 1, log);
                            break;

                        case 74:
                            inserted = hasThird
                                ? Insert74_ThreeParts(primary, secondary, third, attributeName, r + 1, log)
                                : Insert74_TwoParts(primary, secondary, attributeName, r + 1, log);
                            break;

                        default:
                            break;
                    }

                    if (inserted)
                        ok++;
                    else
                        fail++;
                }
                catch (Exception ex)
                {
                    fail++;
                    log.AppendLine($"Row {r + 1}: EXCEPTION - {ex.Message} | Macro={macroNumber}, Attr={attributeName}");
                }
            }

            model.CommitChanges();

            // Log yazma
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "InsertConnectionsLog.txt");
            try
            {
                File.WriteAllText(logPath, log.ToString());
            }
            catch { /* log yazma başarısız olsa bile devam et */ }

            return new InsertSummary { OK = ok, FAIL = fail, SKIPPED = skipped };
        }

        // ==================== 70 - İKİ PARÇALI ====================
        private bool Insert70_TwoParts(Part primary, Part secondary, string attributeName, int rowNumber, StringBuilder log)
        {
            // Önce normal sıra ile dene
            var conn = CreateBaseConnection(attributeName, 70);
            conn.SetPrimaryObject(primary);
            conn.SetSecondaryObject(secondary);
            conn.LoadAttributesFromFile(attributeName);
            conn.AutoDirectionType = AutoDirectionTypeEnum.AUTODIR_FROM_ATTRIBUTE_FILE;

            bool inserted = conn.Insert();

            if (!inserted)
            {
                // Swap (ikinci deneme)
                conn = CreateBaseConnection(attributeName, 70);
                conn.SetPrimaryObject(secondary);
                conn.SetSecondaryObject(primary);
                conn.LoadAttributesFromFile(attributeName);
                conn.AutoDirectionType = AutoDirectionTypeEnum.AUTODIR_FROM_ATTRIBUTE_FILE;
                inserted = conn.Insert();
            }

            if (inserted)
            {
                log.AppendLine($"Row {rowNumber}: OK - 70 (2 parçalı) → {attributeName}");
                return true;
            }

            log.AppendLine($"Row {rowNumber}: FAIL - 70 (2 parçalı) - Hem normal hem swap başarısız");
            return false;
        }

        // ==================== 70 - ÜÇ PARÇALI ====================
        private bool Insert70_ThreeParts(Part primary, Part secondary, Part third, string attributeName, int rowNumber, StringBuilder log)
        {
            var conn = CreateBaseConnection(attributeName, 70);
            conn.SetPrimaryObject(primary);

            ArrayList secList = new ArrayList { secondary, third };
            conn.SetSecondaryObjects(secList);

            conn.LoadAttributesFromFile(attributeName);
            conn.AutoDirectionType = AutoDirectionTypeEnum.AUTODIR_FROM_ATTRIBUTE_FILE;

            bool inserted = conn.Insert();

            if (!inserted)
            {
                // Swap için üçüncü parçayı da taşıyoruz
                conn = CreateBaseConnection(attributeName, 70);
                conn.SetPrimaryObject(secondary);

                ArrayList swapList = new ArrayList { primary, third };
                conn.SetSecondaryObjects(swapList);

                conn.LoadAttributesFromFile(attributeName);
                conn.AutoDirectionType = AutoDirectionTypeEnum.AUTODIR_FROM_ATTRIBUTE_FILE;
                inserted = conn.Insert();
            }

            if (inserted)
            {
                log.AppendLine($"Row {rowNumber}: OK - 70 (3 parçalı) → {attributeName}");
                return true;
            }

            log.AppendLine($"Row {rowNumber}: FAIL - 70 (3 parçalı) - Hem normal hem swap başarısız");
            return false;
        }

        // ====================== 74 - İKİ PARÇALI ======================
        private bool Insert74_TwoParts(Part primary, Part secondary, string attributeName, int rowNumber, StringBuilder log)
        {
            var conn = CreateBaseConnection(attributeName, 74);
            conn.SetPrimaryObject(primary);
            conn.SetSecondaryObject(secondary);

            conn.LoadAttributesFromFile(attributeName);
            conn.AutoDirectionType = AutoDirectionTypeEnum.AUTODIR_FROM_ATTRIBUTE_FILE;

            bool inserted = conn.Insert();

            if (inserted)
            {
                conn.UpVector = new Vector(0, 0, -1000);
                conn.PositionType = PositionTypeEnum.COLLISION_PLANE;
                conn.Modify();

                log.AppendLine($"Row {rowNumber}: OK - 74 (2 parçalı) → {attributeName}");
                return true;
            }

            log.AppendLine($"Row {rowNumber}: FAIL - 74 (2 parçalı) Insert failed");
            return false;
        }

        // ====================== 74 - ÜÇ PARÇALI ======================
        private bool Insert74_ThreeParts(Part primary, Part secondary, Part third, string attributeName, int rowNumber, StringBuilder log)
        {
            var conn = CreateBaseConnection(attributeName, 74);
            conn.SetPrimaryObject(primary);

            ArrayList secList = new ArrayList { secondary, third };
            conn.SetSecondaryObjects(secList);

            conn.LoadAttributesFromFile(attributeName);
            conn.AutoDirectionType = AutoDirectionTypeEnum.AUTODIR_FROM_ATTRIBUTE_FILE;   // Önemli

            bool inserted = conn.Insert();

            if (inserted)
            {
                conn.UpVector = new Vector(0, 0, -1000);     // +Z yönü
                conn.PositionType = PositionTypeEnum.COLLISION_PLANE;

                conn.Modify();

                log.AppendLine($"Row {rowNumber}: OK - 74 (3 parçalı) → {attributeName} (+Z yönü ile)");
                return true;
            }

            log.AppendLine($"Row {rowNumber}: FAIL - 74 (3 parçalı) Insert failed");
            return false;
        }
        private Tekla.Structures.Model.Connection CreateBaseConnection(string attributeName, int macroNumber)
        {
            return new Tekla.Structures.Model.Connection
            {
                Name = attributeName,
                Number = macroNumber
            };
        }

        private int SafeInt(object value)
        {
            if (value == null) return 0;
            int.TryParse(Convert.ToString(value), out int result);
            return result;
        }

        private class InsertSummary
        {
            public int OK { get; set; }
            public int FAIL { get; set; }
            public int SKIPPED { get; set; }
        }
        #endregion

        private void button4_Click(object sender, EventArgs e)
        {
            string selectedFolder = SelectFolderWithModernDialog();
            if (string.IsNullOrWhiteSpace(selectedFolder))
                return;

            ApplyCheckStatusToGrid(advancedDataGridView1, selectedFolder);
            ApplyCheckStatusToGrid(advancedDataGridView2, selectedFolder);

            MessageBox.Show("Check completed.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string SelectFolderWithModernDialog()
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "Please select the folder containing attribute files";
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    return dialog.FileName;
                }
            }

            return null;
        }

        private void ApplyCheckStatusToGrid(DataGridView dgv, string folderPath)
        {
            if (dgv == null || dgv.DataSource == null)
                return;

            DataTable dt = null;

            if (dgv.DataSource is DataTable dataTable)
                dt = dataTable;
            else if (dgv.DataSource is DataView dataView)
                dt = dataView.Table;

            if (dt == null)
                return;

            if (!dt.Columns.Contains("AttributeName"))
                return;

            if (!dt.Columns.Contains("CheckStatus"))
                dt.Columns.Add("CheckStatus", typeof(string));

            HashSet<string> fileNames = GetAllAttributeFileNames(folderPath);

            foreach (DataRow row in dt.Rows)
            {
                string attributeName = row["AttributeName"]?.ToString()?.Trim() ?? "";
                int macroNumber = 0;

                if (dt.Columns.Contains("MacroNumber"))
                    int.TryParse(row["MacroNumber"]?.ToString(), out macroNumber);

                if (string.IsNullOrWhiteSpace(attributeName) || macroNumber <= 0)
                {
                    row["CheckStatus"] = "X";
                    continue;
                }

                string fullExpectedName = BuildExpectedAttributeFileName(attributeName, macroNumber);

                if (fileNames.Contains(fullExpectedName))
                    row["CheckStatus"] = "✓";
                else
                    row["CheckStatus"] = "X";
            }

            dgv.Refresh();
        }

        private string BuildExpectedAttributeFileName(string attributeName, int macroNumber)
        {
            attributeName = attributeName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(attributeName) || macroNumber <= 0)
                return attributeName;

            // Zaten sonunda .j74 / .j70 gibi bir ifade varsa tekrar ekleme
            if (Regex.IsMatch(attributeName, @"\.j\d+$", RegexOptions.IgnoreCase))
                return attributeName;

            return $"{attributeName}.j{macroNumber}";
        }

        private HashSet<string> GetAllAttributeFileNames(string folderPath)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(folderPath))
                return names;

            string[] allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (string file in allFiles)
            {
                string fileName = Path.GetFileName(file)?.Trim();

                if (!string.IsNullOrWhiteSpace(fileName))
                    names.Add(fileName);
            }

            return names;
        }
       
        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _form1.Show();
            this.Close();
        }
    }
}
