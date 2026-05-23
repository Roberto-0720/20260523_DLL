using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelCadPlugin.Data
{
    /// <summary>
    /// Singleton database chứa toàn bộ tiết diện thép.
    /// Gọi Load() một lần khi khởi tạo plugin.
    /// </summary>
    public class SectionDatabase
    {
        private static readonly Lazy<SectionDatabase> _instance =
            new Lazy<SectionDatabase>(() => new SectionDatabase());

        public static SectionDatabase Instance => _instance.Value;

        private readonly List<SectionProfile> _all = new List<SectionProfile>();
        private bool _loaded = false;

        private SectionDatabase() { }

        public int TotalCount => _all.Count;
        public bool IsLoaded  => _loaded;

        /// <summary>Load dữ liệu từ file Excel. Gọi một lần khi khởi động.</summary>
        public void Load(string excelPath)
        {
            if (_loaded) return;
            try
            {
                var sections = SectionLoader.LoadHShapes(excelPath);
                _all.AddRange(sections);
                _loaded = true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot load Section Data.xlsx: {ex.Message}", ex);
            }
        }

        // ── Truy vấn ────────────────────────────────────────────────────

        /// <summary>Lấy tất cả tiết diện</summary>
        public IReadOnlyList<SectionProfile> GetAll() => _all;

        /// <summary>Lọc theo loại tiết diện</summary>
        public IReadOnlyList<SectionProfile> GetByType(SectionType type) =>
            _all.Where(s => s.Type == type).ToList();

        /// <summary>Tìm theo tên chính xác</summary>
        public SectionProfile GetByName(string name) =>
            _all.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Tìm kiếm theo từ khóa (tên chứa keyword)</summary>
        public IReadOnlyList<SectionProfile> Search(string keyword, SectionType? type = null)
        {
            var q = _all.AsEnumerable();
            if (type.HasValue) q = q.Where(s => s.Type == type.Value);
            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(s => s.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            return q.ToList();
        }

        /// <summary>Lấy tiết diện đầu tiên của loại chỉ định</summary>
        public SectionProfile GetFirstByType(SectionType type) =>
            _all.FirstOrDefault(s => s.Type == type);

        /// <summary>Danh sách loại tiết diện có trong database</summary>
        public IReadOnlyList<SectionType> AvailableTypes =>
            _all.Select(s => s.Type).Distinct().OrderBy(t => t).ToList();
    }
}
