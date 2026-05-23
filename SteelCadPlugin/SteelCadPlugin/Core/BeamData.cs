using Autodesk.AutoCAD.Geometry;

namespace SteelCadPlugin.Core
{
    /// <summary>
    /// Kiểu thể hiện của cấu kiện trên bản vẽ
    /// </summary>
    public enum ViewType
    {
        Plan      = 1,   // Mặt bằng  — nhìn từ trên xuống
        Elevation = 2,   // Mặt đứng  — nhìn từ bên / trắc diện
        Section   = 3    // Tiết diện — nhìn dọc trục cấu kiện
    }

    /// <summary>
    /// Toàn bộ thông số của một cấu kiện dầm thép đã vẽ.
    /// Được lưu vào XData của BlockReference.
    /// </summary>
    public class BeamData
    {
        // ── Tiết diện ─────────────────────────────────────────────────
        public string SectionName { get; set; }  // "BH500X200X10X16"
        public double d_mm  { get; set; }        // Chiều cao (mm)
        public double bf_mm { get; set; }        // Chiều rộng cánh (mm)
        public double tw_mm { get; set; }        // Chiều dày bụng (mm)
        public double tf_mm { get; set; }        // Chiều dày cánh (mm)
        public double R_mm  { get; set; }        // Bán kính lượn (mm)

        // ── Working Points (WCS, đơn vị bản vẽ) ─────────────────────
        public Point3d WpStart { get; set; }
        public Point3d WpEnd   { get; set; }

        // ── Đoạn cắt (đơn vị bản vẽ) ─────────────────────────────────
        public double Cut1 { get; set; }   // Cắt đầu (tính từ WpStart)
        public double Cut2 { get; set; }   // Cắt cuối (tính từ WpEnd)

        // ── Kiểu thể hiện ─────────────────────────────────────────────
        public ViewType ViewType { get; set; } = ViewType.Plan;

        // ── Vị trí text (offset so với midpoint, local coords) ────────
        public double TextOffsetX { get; set; } = 0;
        public double TextOffsetY { get; set; } = 50;   // mm mặc định

        // ── Hệ số quy đổi đơn vị bản vẽ ─────────────────────────────
        // 1 đơn vị bản vẽ = MmPerUnit mm
        // Mặc định 1 (bản vẽ đơn vị mm). 
        // Nếu bản vẽ đơn vị m: MmPerUnit = 1000
        public double MmPerUnit { get; set; } = 1.0;

        // ── Thuộc tính tính toán ──────────────────────────────────────

        /// <summary>Chiều dài tổng (đơn vị bản vẽ)</summary>
        public double Length => WpStart.DistanceTo(WpEnd);

        /// <summary>Chiều dài vẽ thực (sau khi trừ cut, đơn vị bản vẽ)</summary>
        public double DrawLength => Length - Cut1 - Cut2;

        /// <summary>Góc của dầm (radian) so với trục X</summary>
        public double Angle =>
            System.Math.Atan2(WpEnd.Y - WpStart.Y, WpEnd.X - WpStart.X);

        /// <summary>Điểm giữa của dầm (WCS)</summary>
        public Point3d Midpoint =>
            new Point3d((WpStart.X + WpEnd.X) / 2,
                        (WpStart.Y + WpEnd.Y) / 2,
                        (WpStart.Z + WpEnd.Z) / 2);

        // ── Quy đổi mm → đơn vị bản vẽ ──────────────────────────────
        public double d  => d_mm  / MmPerUnit;
        public double bf => bf_mm / MmPerUnit;
        public double tw => tw_mm / MmPerUnit;
        public double tf => tf_mm / MmPerUnit;
        public double R  => R_mm  / MmPerUnit;
    }
}
