# SteelCAD Plugin — Task List

## Phase 1: Project Setup
- [x] Xác nhận AutoCAD DLL paths (C:\Program Files\Autodesk\AutoCAD 2023\)
- [x] Xác nhận cấu trúc Section Data.xlsx (12 sheets, H-shapes first)
- [ ] Tạo thư mục dự án SteelCadPlugin\
- [ ] Tạo SteelCadPlugin.csproj (.NET 4.7, x64, ClosedXML)
- [ ] Tạo SteelCadPlugin.sln

## Phase 2: Data Layer
- [ ] Data/SectionProfile.cs — Model tiết diện
- [ ] Data/SectionLoader.cs — Đọc Excel bằng ClosedXML
- [ ] Data/SectionDatabase.cs — Singleton cache

## Phase 3: Core Engine
- [ ] Core/BeamData.cs — Thông số dầm thép
- [ ] Core/BeamXData.cs — Đọc/ghi XData
- [ ] Core/BeamGeometry.cs — Vẽ geometry (Plan/Elevation/Section)
- [ ] Core/BeamManager.cs — Tạo/cập nhật block trong CAD
- [ ] Core/BeamGripOverrule.cs — 7 custom grips

## Phase 4: Commands & App
- [ ] App.cs — IExtensionApplication (register grips, load DB)
- [ ] Commands.cs — STEELBEAM, STEELVIEW, STEELPALETTE

## Phase 5: WPF UI
- [ ] UI/SteelPaletteControl.xaml + .cs — Main palette
- [ ] UI/SectionPickerWindow.xaml + .cs — Section picker dialog

## Phase 6: Build & Test
- [ ] Kiểm tra build không lỗi
- [ ] Tạo README.md hướng dẫn cài đặt
- [ ] Test NETLOAD trong AutoCAD 2023
