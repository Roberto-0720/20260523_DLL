# AutoCAD .NET Plugin — SteelCAD Dynamic Blocks

Plugin C# (.NET) cho AutoCAD hỗ trợ vẽ và quản lý cấu kiện thép hình với Dynamic Block đầy đủ tính năng.

> [!IMPORTANT]
> **Đã xác nhận:** AutoCAD 2023 (target ≥ 2018), .NET Framework 4.7, Excel cùng thư mục DLL, scope ban đầu: H-shapes.

---

## Tổng quan

Xây dựng AutoCAD Plugin dạng `.arx`/`.dll` (AutoCAD .NET API) để:
- Tạo **Dynamic Block** cấu kiện thép với 7 grips tương tác
- Hỗ trợ **3 kiểu thể hiện** có thể hoán đổi cho nhau:
  - **Type 1 — Plan View**: Nhìn từ trên xuống (mặt bằng)
  - **Type 2 — Elevation View**: Nhìn từ mặt bên (mặt đứng)
  - **Type 3 — Section View**: Nhìn dọc trục (cắt ngang)
- Đọc dữ liệu tiết diện từ file **`Section Data.xlsx`** có sẵn
- Giao diện WPF Palette tích hợp trong AutoCAD

---

## Dữ liệu tiết diện hiện có (`Section Data.xlsx`)

| Sheet | Loại tiết diện | Số lượng |
|-------|---------------|---------|
| W Section | W-shape (AISC) | ~273 |
| H_shapes_metric | H Built-up (BH) | ~1095 |
| I_shapes_metric | I Rolled | ~58 |
| UB_UC_shapes_metric | UB/UC (BS) | ~103 |
| HE_IPE_shapes_metric | HEA/IPE (EU) | ~48 |
| Angle | L-shape | ~320 |
| Double Angle | 2L-shape | ~400 |
| Double Channel | 2C-shape | ~53 |
| T-Shape | T-shape | ~207 |
| Channel | C-shape | ~149 |
| Tube | BOX/RHS | ~146 |
| Pipe | CHS/Pipe | ~562 |

**Cột chung:** `name, d_mm (d/t3), bf_mm (t2), tw_mm, tf_mm, R_mm`

---

## Kiến trúc Plugin

```
SteelCadPlugin/
├── SteelCadPlugin.csproj          (.NET Framework 4.8, AutoCAD refs)
├── Commands/
│   ├── PlaceBeamCommand.cs        Lệnh STEELBEAM — đặt cấu kiện
│   └── EditSectionCommand.cs      Lệnh STEELEDIT — sửa tiết diện
├── DynamicBlocks/
│   ├── BlockBuilder.cs            Tạo Dynamic Block bằng API
│   ├── GripOverrule.cs            Custom Grips (WP, Cut, Text, Move)
│   └── ViewSwitcher.cs            Chuyển đổi Type1/Type2/Type3
├── SectionData/
│   ├── SectionLoader.cs           Đọc Excel → List<SectionProfile>
│   ├── SectionProfile.cs          Model dữ liệu tiết diện
│   └── SectionDatabase.cs         Singleton cache
├── UI/
│   ├── SteelPalette.xaml          WPF Palette chính
│   ├── SteelPalette.xaml.cs
│   ├── SectionPickerDialog.xaml   Hộp chọn tiết diện
│   └── SectionPickerDialog.xaml.cs
└── Utils/
    ├── GeometryHelper.cs          Tính toán hình học
    └── LayerHelper.cs             Quản lý layer
```

---

## Chi tiết Dynamic Block

### Grips (7 grips như ảnh)

| Grip | Kiểu | Mô tả |
|------|------|-------|
| WP_Start | Point | Working Point đầu — kéo thả tự do |
| WP_End | Point | Working Point cuối — kéo thả tự do |
| Cut_Start | Linear | Khoảng cắt đầu (Cut1) |
| Cut_End | Linear | Khoảng cắt cuối (Cut2) |
| Text_Pos | Move | Vị trí nhãn tên cấu kiện |
| Move_All | Move | Di chuyển toàn bộ block |
| Section_Picker | Lookup | Chọn tiết diện từ bảng |

### Custom Properties (Block XData / ExtendedData)

```
PROFILE    = "H588X300X12X20"
d          = 588.0   (mm)
bf         = 300.0   (mm)
tw         = 12.0    (mm)
tf         = 20.0    (mm)
Cut1       = 0.0     (mm)
Cut2       = 0.0     (mm)
ViewType   = 1       (1=Plan, 2=Elevation, 3=Section)
TextPos    = relative offset
```

### 3 Kiểu thể hiện

#### Type 1 — Plan View (mặt bằng, nhìn từ trên)
- Vẽ 2 đường song song (cánh trên + cánh dưới) cách nhau `bf`
- Đường tâm dashed ở giữa
- Độ dài = `L - Cut1 - Cut2`

#### Type 2 — Elevation View (mặt đứng, nhìn từ bên)
- Vẽ chữ I: 2 cánh ngang (dày `tf`) + bụng đứng (cao `d`)
- Chiều cao block = `d`, chiều rộng cánh = `bf`
- Phù hợp bản vẽ mặt cắt đứng/mặt đứng công trình

#### Type 3 — Section View (cắt ngang dọc trục)
- Vẽ tiết diện chữ H đứng (cross-section)
- Hiển thị hatch (gạch chéo) thể hiện vật liệu thép
- Kích thước thực: `d × bf × tw × tf`

---

## Proposed Changes

### Component 1: Solution & Project Setup

#### [NEW] SteelCadPlugin/SteelCadPlugin.csproj
- Target: .NET Framework 4.8
- References: `acmgd.dll`, `acdbmgd.dll`, `accoremgd.dll`, `AcWindows.dll`
- NuGet: `ClosedXML` hoặc `EPPlus` (đọc Excel)

---

### Component 2: Section Data Layer

#### [NEW] SectionData/SectionProfile.cs
Model chứa thông tin 1 tiết diện:
- `Name, SectionType, d, bf, tw, tf, R, Area, Weight`

#### [NEW] SectionData/SectionLoader.cs
- Đọc toàn bộ 12 sheets từ `Section Data.xlsx`
- Map sang `List<SectionProfile>`
- Xử lý các format khác nhau (sheets Angle/Channel có header khác)

#### [NEW] SectionData/SectionDatabase.cs
- Singleton pattern
- Cache theo `SectionType` enum: `H, W, I, UB, HEA, L, 2L, 2C, T, C, BOX, PIPE`
- Method: `GetAll()`, `GetByType()`, `Search(keyword)`

---

### Component 3: Dynamic Block Builder

#### [NEW] DynamicBlocks/BlockBuilder.cs
- Tạo `BlockTableRecord` bằng AutoCAD .NET API
- Vẽ geometry theo ViewType (Plan/Elevation/Section)
- Thêm `AttributeDefinition` cho PROFILE label
- Gắn XData cho custom properties

#### [NEW] DynamicBlocks/GripOverrule.cs
- Kế thừa `GripOverrule` từ AutoCAD API
- Override `GetGripPoints`, `MoveGripPointsAt`
- Implement 7 grips với visual feedback

#### [NEW] DynamicBlocks/ViewSwitcher.cs
- Method `SwitchViewType(ObjectId blockRefId, int newType)`
- Rebuild geometry giữ nguyên WP, Cut, Text position
- Update Properties panel

---

### Component 4: Commands

#### [NEW] Commands/PlaceBeamCommand.cs
- Lệnh: `STEELBEAM`
- Jig để preview block khi di chuột
- Click WP_Start → Click WP_End → Enter xác nhận

#### [NEW] Commands/EditSectionCommand.cs
- Lệnh: `STEELEDIT`
- Click vào block → mở dialog chọn tiết diện mới

---

### Component 5: WPF UI

#### [NEW] UI/SteelPalette.xaml
Palette dạng panel dọc trong AutoCAD:
- Dropdown chọn loại tiết diện (H, W, I, L...)
- Danh sách tiết diện có lọc/tìm kiếm
- Preview hình thumbnail tiết diện
- Nút: **Place (Plan)** | **Place (Elevation)** | **Place (Section)**
- Hiển thị properties của block đang chọn

#### [NEW] UI/SectionPickerDialog.xaml
- Dialog riêng khi click Lookup Grip
- DataGrid hiển thị tên + thông số `d, bf, tw, tf`
- Filter theo tên, sort theo cột

---

## Decisions (Đã xác nhận)

| # | Quyết định | Giá trị |
|---|-----------|--------|
| 1 | AutoCAD target | 2018+ (dùng DLL từ 2023, .NET Framework 4.7) |
| 2 | Vị trí Excel | Cùng thư mục với `.dll` |
| 3 | Scope Phase 1 | H-shapes (BH, W, I, UB, HEA) |
| 4 | Grip approach | **Custom Grip Overrule** (Cách B) |
| 5 | Block approach | Unique block per beam, inserted at origin |

---

## Verification Plan

### Build & Test
```
dotnet build SteelCadPlugin.sln
```
- Load `.dll` vào AutoCAD bằng lệnh `NETLOAD`
- Test lệnh `STEELBEAM` → đặt dầm H588
- Test kéo WP grips → block thay đổi chiều dài
- Test Cut grips → đoạn cắt thay đổi
- Test chuyển Type 1 → Type 2 → Type 3

### Manual Verification
- Kiểm tra Properties panel hiển thị đúng `d, bf, tw, tf, Cut1, Cut2`
- Kiểm tra export ra DWG và mở lại vẫn đúng geometry
- Test với H588X300X12X20 và W44X335 như dữ liệu mẫu
