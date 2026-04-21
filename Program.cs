using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Girt_PurlinAutoConnection
{
    internal static class Program
    {

        static void Executer()
        {
            Application.Run(new Form1());
        }


        /// <summary>
        /// Uygulamanın ana girdi noktası.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //if (!Licensing.EnsureAuthorized())
            //{
            //    MessageBox.Show("INVALID LICENSE!");
            //    Environment.Exit(2);
            //    return;
            //}

            Executer();
        }
    }
}
