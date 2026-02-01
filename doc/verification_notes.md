# Kiểm chứng: lặp ký tự cuối + quy tắc modifier

## Phát hiện quan trọng
1. **Modifier (mark/tone) chỉ được áp dụng khi ở cuối chuỗi**  
   - Theo `RebuildFromRaw()` trong `VietnameseConverter.cs`, modifier chỉ áp dụng khi **tất cả ký tự còn lại cũng là modifier** (“STRICT RULE”).  
   - Điều này buộc `z/w/s/f/r/x/j/d` phải nằm **cuối sym** để được áp dụng.

2. **`oo/uu/aa` không phải là cách bỏ dấu**, nhưng **vẫn nằm trong whitelist**  
   - Code **không hề** có rule chuyển `oo → ô`, `uu → ư`, `aa → â`. Dấu chỉ áp dụng qua **`z/w`**.  
   - Tuy vậy `SymatoSyms.cs` vẫn chứa các âm tiết kết thúc bằng **hai ký tự trùng ở cuối**.  
   - Tổng cộng **45** âm tiết có **2 ký tự cuối giống nhau** (chỉ các đuôi: `oo`, `uu`, `aa`).

3. **`dd` không hợp lệ khi đứng một mình**, nhưng **`dd*` là hợp lệ** vì `dd` đại diện cho `đ` trong `GetBaseSym()`  
   - `GetBaseSym()` map `đ → "dd"`, nên các âm tiết bắt đầu bằng **đ** được lưu dưới dạng `dd...` trong `SymatoSyms`.  
   - Không có âm tiết nào kết thúc bằng `dd`, và `dd` một mình **không phải sym** hợp lệ.

---

## Bằng chứng (trích dòng từ `SymatoSyms.cs`)
Các ví dụ có đuôi lặp **ở cuối**:
- `boo` (line 10)  
- `buu` (line 12)  
- `ddaa` (line 21)  
- `ddoo` (line 24)  
- `dduu` (line 26)  
- `oo` (line 79)  
- `uu` (line 113)  

Nhóm đuôi lặp trong whitelist:
- **`oo` (21 mục):** `boo, choo, coo, ddoo, doo, goo, hoo, khoo, koo, loo, moo, noo, oo, phoo, poo, roo, soo, thoo, too, voo, xoo`
- **`uu` (23 mục):** `buu, chuu, cuu, dduu, duu, giuu, guu, huu, khuu, luu, muu, nguu, nhuu, nuu, phuu, quu, ruu, suu, thuu, truu, tuu, uu, vuu`
- **`aa` (1 mục):** `ddaa`

---

## Tiến trình kiểm chứng
1. **Đọc `VietnameseConverter.cs`:**
   - Xác nhận quy tắc “modifier chỉ ở cuối” trong `RebuildFromRaw()` (comment “STRICT RULE”).
   - Xác nhận chỉ có `z` (mũ) và `w` (móc/trăng) thay đổi nguyên âm; **không có** rule `oo/uu/aa`.
   - Xác nhận `GetBaseSym()` map `đ → "dd"`.

2. **Quét `SymatoSyms.cs`:**
   - Trích danh sách Raw và lọc các sym có **2 ký tự cuối giống nhau**.  
   - Kết quả: **45** mục, chỉ thuộc các đuôi `oo/uu/aa`.  
   - Lấy line number của các ví dụ tiêu biểu để tham chiếu nhanh.

---

## Ý nghĩa thực tế
- Dù **không phải cách bỏ dấu đúng** (`oo/uu/aa`), các chuỗi này **vẫn được coi là hợp lệ** vì nằm trong whitelist.  
- Nếu muốn cấm hoàn toàn các âm tiết kết thúc bằng ký tự lặp, cần:
  - Loại chúng khỏi `SymatoSyms`, **hoặc**
  - Thêm rule validation chặn đuôi lặp trong `IsValidSym()`.
