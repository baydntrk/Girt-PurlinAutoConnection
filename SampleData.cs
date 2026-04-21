using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Girt_PurlinAutoConnection
{
    public static class SampleData
    {
        public static DataTable CreateSelectedMembersDataTable()
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("Type", typeof(string));
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Profile", typeof(string));
            dt.Columns.Add("Material", typeof(string));
            dt.Columns.Add("StartX", typeof(double));
            dt.Columns.Add("StartY", typeof(double));
            dt.Columns.Add("StartZ", typeof(double));
            dt.Columns.Add("EndX", typeof(double));
            dt.Columns.Add("EndY", typeof(double));
            dt.Columns.Add("EndZ", typeof(double));

            dt.Rows.Add("Beam", 1771201, "BEAM", "IPE160", "S235JR", 2000d, 2199.99972381766d, 3600d, 2000d, 3199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770974, "BEAM", "IPE120", "S235JR", 3500d, 649.999999818d, 3600d, 3500d, 1199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770923, "BEAM", "HEA120", "S235JR", 2000d, 649.999723817659d, 3600d, 5000.00000000005d, 650.000000000064d, 3600d);
            dt.Rows.Add("Beam", 1770765, "BEAM", "IPE120", "S235JR", 3500d, -0.000276182341167441d, 3600d, 3500d, 649.999999818d, 3600d);
            dt.Rows.Add("Beam", 1770715, "BEAM", "HEA120", "S235JR", 2000d, -0.000276182341167441d, 3600d, 2000d, 1199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770660, "BEAM", "IPE120", "S235JR", 3500d, 1199.99972381766d, 3600d, 3500d, 2199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770578, "BEAM", "IPE120", "S235JR", 2000d, 1199.99972381766d, 3600d, 2000d, 2199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770446, "BEAM", "IPE200", "S235JR", 3500d, 5000d, 3600d, 3500d, 2199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770381, "BEAM", "IPE200", "S235JR", 0d, 4199.99972381766d, 3600d, 3500d, 4199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770331, "BEAM", "IPE200", "S235JR", 0d, 3199.99972381766d, 3600d, 3500d, 3199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770281, "BEAM", "IPE200", "S235JR", 0d, 2199.99972381766d, 3600d, 5000d, 2199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1770158, "BEAM", "IPE200", "S235JR", 0d, 1199.99972381766d, 3600d, 5000d, 1199.99972381766d, 3600d);
            dt.Rows.Add("Beam", 1769891, "BEAM", "IPE240", "S235JR", 5000d, -1.59161572810262E-12d, 7200d, 5000d, 5000d, 7200d);
            dt.Rows.Add("Beam", 1769778, "BEAM", "IPE300", "S235JR", 2.27373675443232E-13d, 0d, 7200d, 5.11590769747272E-13d, 5000d, 7200d);
            dt.Rows.Add("Beam", 1769654, "BEAM", "IPE240", "S235JR", 0d, 5000d, 7200d, 5000d, 5000d, 7200d);
            dt.Rows.Add("Beam", 1769609, "BEAM", "IPE240", "S235JR", 0d, 0d, 7200d, 5000d, 0d, 7200d);
            dt.Rows.Add("Beam", 1769497, "COLUMN", "IPE300", "S235JR", 10000d, 0d, 5308.87610606249d, 10000d, 0d, 7200d);
            dt.Rows.Add("Beam", 1769397, "COLUMN", "IPE300", "S235JR", 10000d, 5000d, 1517.92630709286d, 10000d, 5000d, 7200d);
            dt.Rows.Add("Beam", 1769297, "COLUMN", "HEB300", "S235JR", 5000d, 5000d, 5410.1736383879d, 5000d, 5000d, 7200d);
            dt.Rows.Add("Beam", 1769197, "COLUMN", "HEB240", "S235JR", 5000d, 0d, 1750.01511564363d, 5000d, 0d, 7200d);
            dt.Rows.Add("Beam", 1769095, "COLUMN", "HEA400", "S235JR", 0d, 5000d, 5223.59472750367d, 0d, 5000d, 7200d);
            dt.Rows.Add("Beam", 1768993, "COLUMN", "HEA400", "S235JR", 0d, 0d, 1948.59146286941d, 0d, 0d, 7200d);
            dt.Rows.Add("Beam", 1768858, "BEAM", "IPE240", "S235JR", 10000d, 0d, 3600d, 10000d, 5000d, 3600d);
            dt.Rows.Add("Beam", 1768704, "BEAM", "HEA200", "S235JR", 5000d, -2.27373675443232E-13d, 3600d, 5000d, 5000d, 3600d);
            dt.Rows.Add("Beam", 1768387, "BEAM", "HEA200", "S235JR", -5.6843418860808E-14d, -1.13686837721616E-13d, 3600d, 0d, 5000d, 3600d);
            dt.Rows.Add("Beam", 1768116, "BEAM", "IPE240", "S235JR", 5000d, 5000d, 3600d, 10000d, 5000d, 3600d);
            dt.Rows.Add("Beam", 1768071, "BEAM", "HEA500", "S235JR", 0d, 5000d, 3600d, 5000d, 5000d, 3600d);
            dt.Rows.Add("Beam", 1768051, "BEAM", "IPE240", "S235JR", 5000d, 0d, 3600d, 10000d, 0d, 3600d);
            dt.Rows.Add("Beam", 1768012, "BEAM", "HEA500", "S235JR", 0d, 0d, 3600d, 5000d, 0d, 3600d);
            dt.Rows.Add("Beam", 1767847, "COLUMN", "HEA400", "S235JR", 0d, 5000d, 0d, 0d, 5000d, 5223.59472750367d);
            dt.Rows.Add("Beam", 1767817, "COLUMN", "HEB300", "S235JR", 5000d, 5000d, 0d, 5000d, 5000d, 5410.1736383879d);
            dt.Rows.Add("Beam", 1767787, "COLUMN", "IPE300", "S235JR", 10000d, 5000d, 0d, 10000d, 5000d, 1517.92630709286d);
            dt.Rows.Add("Beam", 1767757, "COLUMN", "IPE300", "S235JR", 10000d, 0d, 0d, 10000d, 0d, 5308.87610606249d);
            dt.Rows.Add("Beam", 1767727, "COLUMN", "HEB240", "S235JR", 5000d, 0d, 0d, 5000d, 0d, 1750.01511564363d);
            dt.Rows.Add("Beam", 1767682, "COLUMN", "HEA400", "S235JR", 0d, 0d, 0d, 0d, 0d, 1948.59146286941d);

            return dt;
        }
    }
}
