using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;

namespace SteelCadPlugin.Data
{
    /// <summary>
    /// Đọc dữ liệu tiết diện từ file Section Data.xlsx
    /// </summary>
    public static class SectionLoader
    {
        /// <summary>Đọc toàn bộ tiết diện H-shapes từ Excel</summary>
        public static List<SectionProfile> LoadHShapes(string excelPath)
        {
            var result = new List<SectionProfile>();
            if (!File.Exists(excelPath)) return result;

            using (var wb = new XLWorkbook(excelPath))
            {
                // Sheet: H_shapes_metric (BH...)
                result.AddRange(LoadGenericSheet(wb, "H_shapes_metric", SectionType.H,
                    nameCol: 1, dCol: 4, bfCol: 5, twCol: 6, tfCol: 7, rCol: 8,
                    areaCol: 3, wCol: 2, startRow: 2));

                // Sheet: W Section (W-shapes AISC)
                result.AddRange(LoadGenericSheet(wb, "W Section", SectionType.H,
                    nameCol: 1, dCol: 4, bfCol: 5, twCol: 6, tfCol: 7, rCol: 8,
                    areaCol: 3, wCol: 2, startRow: 2));

                // Sheet: I_shapes_metric
                result.AddRange(LoadGenericSheet(wb, "I_shapes_metric", SectionType.I,
                    nameCol: 1, dCol: 4, bfCol: 5, twCol: 6, tfCol: 7, rCol: 8,
                    areaCol: 3, wCol: 2, startRow: 2));

                // Sheet: UB_UC_shapes_metric
                result.AddRange(LoadGenericSheet(wb, "UB_UC_shapes_metric", SectionType.UB,
                    nameCol: 1, dCol: 4, bfCol: 5, twCol: 6, tfCol: 7, rCol: 8,
                    areaCol: 3, wCol: 2, startRow: 2));

                // Sheet: HE_IPE_shapes_metric
                result.AddRange(LoadGenericSheet(wb, "HE_IPE_shapes_metric", SectionType.HEA,
                    nameCol: 1, dCol: 4, bfCol: 5, twCol: 6, tfCol: 7, rCol: 8,
                    areaCol: 3, wCol: 2, startRow: 2));
            }

            return result;
        }

        /// <summary>Đọc một sheet theo định dạng chuẩn (name, W, A, d, bf, tw, tf, R)</summary>
        private static IEnumerable<SectionProfile> LoadGenericSheet(
            XLWorkbook wb,
            string sheetName,
            SectionType type,
            int nameCol, int dCol, int bfCol, int twCol, int tfCol, int rCol,
            int areaCol, int wCol,
            int startRow)
        {
            if (!wb.TryGetWorksheet(sheetName, out IXLWorksheet ws))
                yield break;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            for (int r = startRow; r <= lastRow; r++)
            {
                var nameCell = ws.Cell(r, nameCol);
                if (nameCell.IsEmpty()) continue;

                string name = nameCell.GetString().Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                yield return new SectionProfile
                {
                    Name   = name,
                    Type   = type,
                    d      = GetDouble(ws, r, dCol),
                    bf     = GetDouble(ws, r, bfCol),
                    tw     = GetDouble(ws, r, twCol),
                    tf     = GetDouble(ws, r, tfCol),
                    R      = GetDouble(ws, r, rCol),
                    Area   = GetDouble(ws, r, areaCol),
                    Weight = GetDouble(ws, r, wCol)
                };
            }
        }

        private static double GetDouble(IXLWorksheet ws, int row, int col)
        {
            try
            {
                var cell = ws.Cell(row, col);
                if (cell.IsEmpty()) return 0;
                if (cell.DataType == XLDataType.Number) return cell.GetDouble();
                if (double.TryParse(cell.GetString(), out double v)) return v;
            }
            catch { }
            return 0;
        }
    }
}
