﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NetOffice.ExcelApi;
using NetOffice.ExcelApi.Enums;
using Excel = NetOffice.ExcelApi.Application;

using disfr.UI;

namespace disfr.Writer
{
    class XlsxWriter : TableWriterBase, IRowsWriter
    {
        public string Name { get { return "Xlsx Writer"; } }

        private readonly IList<string> _FilterString = new string[]
        {
            "Microsoft Excel File|*.xlsx",
            "XML Spreadsheet|*.xml",
        };

        public IList<string> FilterString { get { return _FilterString; } }

        public void Write(string filename, int filterindex, IEnumerable<IRowData> rows, ColumnDesc[] columns)
        {
            switch (filterindex)
            {
                case 0: WriteXlsx(filename, rows, columns); break;
                case 1: WriteXmlss(filename, rows, columns); break;
                default:
                    throw new ArgumentOutOfRangeException("filterindex");
            }
        }

        private static void WriteXmlss(string filename, IEnumerable<IRowData> rows, ColumnDesc[] columns)
        {
            using (var output = File.Create(filename))
            {
                var table = CreateXmlTree(rows, columns);
                Transform(table, output, "xmlss");
            }
        }

        private static void WriteXlsx(string filename, IEnumerable<IRowData> rows, ColumnDesc[] columns)
        {
            string tmpname = null;
            try
            {
                tmpname = CreateTempFile(Path.GetTempPath(), ".xml");
                using (var output = File.OpenWrite(tmpname))
                {
                    var table = CreateXmlTree(rows, columns);
                    Transform(table, output, "xmlss");
                }

                var excel = new Excel() { Visible = false, Interactive = false, DisplayAlerts = false };
                try
                {
                    var book = excel.Workbooks.Open(tmpname);
                    book.SaveAs(filename, XlFileFormat.xlOpenXMLWorkbook);
                    book.Close(false);
                }
                finally
                {
                    excel.Quit();
                    excel.Dispose();
                }
            }
            finally
            {
                if (tmpname != null) File.Delete(tmpname);
            }
        }

        private static string CreateTempFile(string folder, string ext)
        {
            var rng = new byte[9];
            using (var rand = new RNGCryptoServiceProvider())
            {
                for (int i = 0; i < 100; i++)
                {
                    rand.GetBytes(rng);
                    var name = Path.Combine(folder, "disfr-" + Convert.ToBase64String(rng).Replace('/', '-') + ext);
                    if (File.Exists(name)) continue;
                    try
                    {
                        var s = File.Open(name, FileMode.CreateNew);
                        s.Close();
                        return name;
                    }
                    catch (IOException)
                    {
                        ;
                    }
                }
                throw new Exception("Couldn't find a unique random file name.");
            }
        }
    }
}
