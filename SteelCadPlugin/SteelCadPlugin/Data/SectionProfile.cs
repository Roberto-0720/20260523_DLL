using System;

namespace SteelCadPlugin.Data
{
    /// <summary>Loại tiết diện thép</summary>
    public enum SectionType
    {
        H,      // H Built-up (BH...) & W-shape AISC
        I,      // I Rolled
        UB,     // UB/UC British Standard
        HEA,    // HEA/IPE European
        L,      // Angle (L-shape)
        L2,     // Double Angle
        C2,     // Double Channel
        T,      // T-Shape
        C,      // Channel
        BOX,    // Rectangular Hollow Section (Tube)
        PIPE    // Circular Hollow Section (Pipe)
    }

    /// <summary>Thông số một tiết diện thép từ database</summary>
    public class SectionProfile
    {
        public string Name     { get; set; }   // e.g. "BH500X200X10X16"
        public SectionType Type { get; set; }

        // Kích thước cơ bản (mm)
        public double d  { get; set; }   // Chiều cao tiết diện (depth)
        public double bf { get; set; }   // Chiều rộng cánh (flange width) / t2
        public double tw { get; set; }   // Chiều dày bụng (web thickness)
        public double tf { get; set; }   // Chiều dày cánh (flange thickness)
        public double R  { get; set; }   // Bán kính lượn (fillet radius)

        // Đặc trưng hình học (tùy chọn)
        public double Area   { get; set; }  // cm²
        public double Weight { get; set; }  // kg/m

        public override string ToString() => Name;
    }
}
