# SymatoIME

Bộ gõ tiếng Việt tùy chỉnh cho Windows 11, được xây dựng bằng .NET 8.0.

## ✨ Tính năng

### 🇻🇳 Gõ tiếng Việt (Telex-like)
| Phím | Chuyển đổi | Ví dụ |
|------|------------|-------|
| `z`  | Mũ (â, ê, ô)        | `az` → â, `ez` → ê |
| `w`  | Móc/Trăng (ă, ơ, ư) | `aw` → ă, `ow` → ơ |
| `dd` | Đ         | `dd` → đ |
| `s`  | Sắc       | `as` → á |
| `f`  | Huyền     | `af` → à |
| `r`  | Hỏi       | `ar` → ả |
| `x`  | Ngã       | `ax` → ã |
| `j`  | Nặng      | `aj` → ạ |

- **Khuyến khích bỏ dấu + thanh sau khi gõ hết âm tiết không dấu**
  - `muon` + `ws` => `mướn`
  - `muon` + `zj` => `muộn`
  - `duong` + `dwf` => `đường`
- **`uo + w` → `ươ`**: `muonw` → `mươn`, `luonw` → `lươn`
- **Auto `ie/ye` → `iê/yê`**: `tien` → `tiên`, `yen` → `yên`
- **Quy tắc bỏ dấu chuẩn**: 
  - `quas` → `quá` (không phải `qúa`)
  - `muonws` → `mướn` (dấu trên `ơ`)

### 🧠 Smart Validation (MỚI!)
- **2800+ âm tiết hợp lệ**: Chỉ áp dụng dấu cho âm tiết tiếng Việt hợp lệ
- **Auto-revert**: Khi gõ tiếng Anh (như `rerun`), tự động revert về ký tự gốc
- **Auto-reposition**: `múo` + `n` → `muón` (dấu tự động di chuyển)

### ⌨️ Key Remapping
Chu kỳ hoán đổi phím: **`~` ↔ `CapsLock` ↔ `Tab`**

| Phím vật lý | Chức năng |
|-------------|-----------|
| `~` (Grave) | → CapsLock |
| `CapsLock` | → Tab |
| `Tab` | → `~` |

### 🔊 Điều chỉnh âm lượng
**`Ctrl + Shift + Mouse Wheel`** - Tăng/giảm âm lượng hệ thống với OSD gốc Windows

### 🎛️ Điều khiển
| Phím tắt | Chức năng |
|----------|-----------|
| `Ctrl + Shift + S` | Bật/tắt gõ tiếng Việt |
| `Escape` | Hoàn tác về ASCII gốc |
| `Backspace` | Smart undo (hoàn tác dấu) |

## 🚀 Cài đặt & Chạy

### Yêu cầu
- Windows 11
- .NET 8.0 SDK

### Chạy từ source
```bash
cd /path/to/symato_qoder
dotnet run
```

### Build release
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Chạy unit tests
```bash
dotnet run -c Test -- --test
```

## 📁 Cấu trúc

```
symato_qoder/
├── Program.cs              # Entry point
├── SymatoContext.cs        # System tray, lifecycle
├── VietnameseConverter.cs  # Core Vietnamese input logic
├── SymatoSyms.cs           # 2800+ valid Vietnamese syllables
├── EngineTests.cs          # Unit tests (29 tests)
├── KeyboardHook.cs         # Low-level keyboard hook
├── MouseHook.cs            # Low-level mouse hook
├── VolumeControl.cs        # Volume control with OSD
├── NativeMethods.cs        # Win32 P/Invoke
├── Settings.cs             # User preferences
├── SymatoIME.csproj        # Project file
└── app.manifest            # UAC manifest
```

## 🏗️ Architecture

- **Render-time decision**: Không modify buffer trực tiếp, quyết định output khi render
- **Diff-based output**: Chỉ gửi thay đổi minimal đến màn hình
- **RebuildFromRaw**: Backspace rebuild lại buffer từ raw input
- **SendInput Unicode**: Gửi ký tự qua keyboard input (không dùng clipboard)

## 🎨 System Tray

- **Icon xanh (S)**: Vietnamese input đang BẬT
- **Icon xám (S)**: Vietnamese input đang TẮT
- **Right-click**: Menu với các tùy chọn

## 📝 Quy tắc bỏ dấu tiếng Việt

1. **Nguyên âm có mũ/móc ưu tiên** (ă, â, ê, ô, ơ, ư)
2. **`qu` là phụ âm** - `u` không nhận dấu → `quá` ✓
3. **`gi` + nguyên âm** - `i` không nhận dấu → `giá` ✓
4. **Nguyên âm đôi `ươ/iê/uô` + phụ âm cuối** → dấu trên nguyên âm thứ 2
   - `mướn` (dấu trên `ơ`), `tiếng` (dấu trên `ê`)
5. **Có phụ âm cuối** → dấu trên nguyên âm cuối
6. **Không có phụ âm cuối** → dấu trên nguyên âm áp cuối

## ✅ Unit Tests (40 tests)

```
✓ Tone tests: as→á, af→à, ar→ả, ax→ã, aj→ạ
✓ Circumflex: az→â, ez→ê, oz→ô
✓ Horn/breve: aw→ă, ow→ơ, uw→ư
✓ Combined: azs→ấ, uwj→ự
✓ UO cluster: tuongw→tương, muonws→mướn
✓ Validation: quas→quá, gias→giá
✓ Auto ie/ye: tien→tiên, yen→yên
✓ Invalid→raw: rerun→rerun, xyz→xyz
✓ Tone Stop Rule: c/ch/t/p only sắc(s)/nặng(j)
```

## 💡 Suggestions

- [ ] Tách Engine riêng để dễ maintain
- [ ] Thêm option tắt validation (cho user muốn gõ tự do)
- [ ] State-based design với CharState struct (như symato_droid)

