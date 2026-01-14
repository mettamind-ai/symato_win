Tạo 1 bộ gõ tiếng Việt chỉ sử dụng latest features from win11, hoàn toàn bằng C#.

bộ gõ chạy ngầm trên windows, có hiển thị logo hình chữ S ở taskbar.
khi chữ S màu trắng trên nền xanh nước biển thì báo hiệu bộ gõ active 
khi chữ S màu trắng trên nền xám thì báo hiệu bộ gõ inactive.

Khi active bộ gõ đơn giản convert sym tức là một âm tiết tiếng việt 
không dấu hợp lệ thành tiếng việt utf8 theo luật sau:

1. người dùng gõ `z` để bỏ dấu â, ê, ô ... trên nguyên âm hợp lệ
2. người dùng gõ `w` để bỏ đấu ơ, ă, ư ... trên nguyên âm hợp lệ
3. người dùng gõ `s, f, r, x, j` để bỏ dấu sắc huyền hỏi ngã nặng trên nguyên âm hợp lệ
4. cuối dùng người dùng gõ `d` để chỉ có thể biến biến ký từ đầu d thành đ
5. khi đang gõ esc sẽ trả về chuỗi raw char từ keyboard
6. backspace tự động khử dấu, ví dụ ouw => ơư + backspace => ou, tương tự với z, s, f, r, x, j
7. Tìm hiểu những tr hợp bỏ dấu như qúa (sai) => quá (đúng), tương tự lựơng là sai ... web search và dùng common sense, tương tự cho các cách bỏ dấu khác ...
8. uo + w => ươ và có hiệu lực ở truờng hợp tổng quát như muon + w => mươn ...
9. ie, ye trong tiếng việt luôn dc convert thành iê, yê (web search và tự reasoning)

<proactive>
- Plan first. Code second. Test and fix bugs last.
- Search web when stuck. Decide yourself. Don't bother user.
- Tự tìm hiểu xem thế nào là 1 âm tiết không dấu hợp lệ
- Tự tìm hiểu cách biến chuỗi asscii thành utf8 dựa trên các luật được cung cấp ở 1. 2. 3. 4. ....
</proactive>

Tiện ích bổ xung:
- key mapping để đổi phím ~ => caplock => tab => ~
- ctrl+shift+wheel scroll để tăng giảm âm lượng, hiển thị volum panel để thấy âm tăng giảm cùng với wheel scroll
- ctrl+shift+s để active / deactive
- khởi động cùng windows option
- có hiển thị tray icon menu (right click) để on / off những lựa chọn trên (default là on)

IMPORTANT:
- Toàn bộ code và doc phải được lưu trong symato_win/ folder
- Chỉ viết và đọc code / tài liệu trong symato_win/ (ko tìm kiếm hướng dẫn ở thư mục khác)
- Viết xong code cần test / review / fix hết bugs rồi mới commit
- Sau khi commit email dungtn@gmail.com để báo cáo tiến độ và đặt thêm câu hỏi nếu có
