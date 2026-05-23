using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SteelCadPlugin.Core
{
    /// <summary>
    /// Đọc/ghi thông số BeamData vào XData của BlockReference.
    ///
    /// Cấu trúc XData (group codes):
    ///   1001 "STEELCAD"        -- registered app name
    ///   1000 "STEELBEAM_V1"    -- marker + version
    ///   1000  SectionName
    ///   1040  d_mm
    ///   1040  bf_mm
    ///   1040  tw_mm
    ///   1040  tf_mm
    ///   1040  R_mm
    ///   1010  WpStart.X   1020 WpStart.Y   1030 WpStart.Z
    ///   1011  WpEnd.X     1021 WpEnd.Y     1031 WpEnd.Z
    ///   1041  Cut1
    ///   1041  Cut2
    ///   1070  ViewType (1/2/3)
    ///   1040  TextOffsetX
    ///   1040  TextOffsetY
    ///   1040  MmPerUnit
    /// </summary>
    public static class BeamXData
    {
        public const string AppName = "STEELCAD";
        private const string Marker = "STEELBEAM_V1";

        // ── Đăng ký ứng dụng XData ────────────────────────────────────

        public static void RegisterApp(Database db)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
                if (!rat.Has(AppName))
                {
                    rat.UpgradeOpen();
                    var rec = new RegAppTableRecord { Name = AppName };
                    rat.Add(rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }
                tr.Commit();
            }
        }

        // ── Ghi XData ────────────────────────────────────────────────

        public static void Write(Entity entity, BeamData data)
        {
            var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, Marker),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, data.SectionName ?? ""),
                new TypedValue((int)DxfCode.ExtendedDataReal, data.d_mm),
                new TypedValue((int)DxfCode.ExtendedDataReal, data.bf_mm),
                new TypedValue((int)DxfCode.ExtendedDataReal, data.tw_mm),
                new TypedValue((int)DxfCode.ExtendedDataReal, data.tf_mm),
                new TypedValue((int)DxfCode.ExtendedDataReal, data.R_mm),
                // WpStart
                new TypedValue(1010, data.WpStart.X),
                new TypedValue(1020, data.WpStart.Y),
                new TypedValue(1030, data.WpStart.Z),
                // WpEnd
                new TypedValue(1011, data.WpEnd.X),
                new TypedValue(1021, data.WpEnd.Y),
                new TypedValue(1031, data.WpEnd.Z),
                // Cuts
                new TypedValue((int)DxfCode.ExtendedDataDist, data.Cut1),
                new TypedValue((int)DxfCode.ExtendedDataDist, data.Cut2),
                // ViewType
                new TypedValue((int)DxfCode.ExtendedDataInteger16, (short)data.ViewType),
                // TextOffset
                new TypedValue((int)DxfCode.ExtendedDataReal, data.TextOffsetX),
                new TypedValue((int)DxfCode.ExtendedDataReal, data.TextOffsetY),
                // Scale
                new TypedValue((int)DxfCode.ExtendedDataReal, data.MmPerUnit)
            );

            entity.XData = rb;
        }

        // ── Đọc XData ────────────────────────────────────────────────

        /// <summary>Đọc BeamData từ entity. Trả về null nếu không có XData của plugin.</summary>
        public static BeamData Read(Transaction tr, ObjectId objId)
        {
            try
            {
                var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                return entity == null ? null : ReadFromEntity(entity);
            }
            catch { return null; }
        }

        public static BeamData ReadFromEntity(Entity entity)
        {
            var rb = entity.GetXDataForApplication(AppName);
            if (rb == null) return null;

            var vals = rb.AsArray();
            // vals[0] = AppName (1001), vals[1] = Marker (1000)
            if (vals.Length < 2) return null;
            if (vals[1].Value?.ToString() != Marker) return null;

            try
            {
                var data = new BeamData();
                int i = 2;

                data.SectionName = vals[i++].Value?.ToString();
                data.d_mm        = ToDouble(vals[i++]);
                data.bf_mm       = ToDouble(vals[i++]);
                data.tw_mm       = ToDouble(vals[i++]);
                data.tf_mm       = ToDouble(vals[i++]);
                data.R_mm        = ToDouble(vals[i++]);

                // WpStart: 3 values (X=1010, Y=1020, Z=1030)
                double sx = ToDouble(vals[i++]);
                double sy = ToDouble(vals[i++]);
                double sz = ToDouble(vals[i++]);
                data.WpStart = new Point3d(sx, sy, sz);

                // WpEnd: 3 values (X=1011, Y=1021, Z=1031)
                double ex = ToDouble(vals[i++]);
                double ey = ToDouble(vals[i++]);
                double ez = ToDouble(vals[i++]);
                data.WpEnd = new Point3d(ex, ey, ez);

                data.Cut1        = ToDouble(vals[i++]);
                data.Cut2        = ToDouble(vals[i++]);
                data.ViewType    = (ViewType)(short)vals[i++].Value;
                data.TextOffsetX = ToDouble(vals[i++]);
                data.TextOffsetY = ToDouble(vals[i++]);
                data.MmPerUnit   = i < vals.Length ? ToDouble(vals[i]) : 1.0;

                return data;
            }
            catch { return null; }
        }

        /// <summary>Kiểm tra entity có phải là SteelBeam không</summary>
        public static bool IsSteelBeam(Entity entity) =>
            entity?.GetXDataForApplication(AppName) != null;

        private static double ToDouble(TypedValue tv)
        {
            if (tv.Value is double d) return d;
            if (double.TryParse(tv.Value?.ToString(), out double r)) return r;
            return 0;
        }
    }
}
