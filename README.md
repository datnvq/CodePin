# 📌 CodePin - Công cụ Ghim Ảnh & Cửa Sổ Nổi (Always On Top) Cho Coder

Ứng dụng C# (WPF .NET 10 + Windows Media OCR) siêu nhẹ, thiết kế riêng cho lập trình viên để ghim ảnh chụp màn hình code/spec và bất kỳ cửa sổ phần mềm nào nổi lơ lửng trên màn hình.

---

## 🚀 Cách cài đặt & Chạy ứng dụng

CodePin hỗ trợ 2 cách sử dụng cực kỳ tiện lợi:

### Cách 1: Cài đặt tự động 1-Click (Khuyên dùng)
- Nhấp đúp chạy file **`setup.bat`**.
- Chương trình sẽ tự động:
  - Cài đặt CodePin vào hệ thống (không cần quyền Admin).
  - Tạo biểu tượng lối tắt 📌 ngoài màn hình chính (**Desktop**).
  - Thêm CodePin vào **Menu Start** (chỉ cần bấm phím Windows và gõ "CodePin" là mở được ngay).
  - Nếu muốn gỡ cài đặt sau này: Chỉ cần chạy `uninstall.bat`.

### Cách 2: Chạy Portable trực tiếp không cần cài đặt
- Nhấp đúp trực tiếp vào file: 👉 **[`publish\CodePin.exe`](file:///d:/Antigravity/CodePin/publish/CodePin.exe)**.
- Lần đầu chạy, ứng dụng cũng sẽ tự động tạo lối tắt ngoài Desktop cho bạn!

---

## 🌟 3 Tính Năng Mới Vừa Nâng Cấp:

### 1. 📝 Trích xuất Code từ ảnh (OCR - Copy Text trực tiếp)
- Tích hợp công nghệ **Windows Media OCR** chạy offline hoàn toàn.
- Khi đang ghim một ảnh chụp màn hình code, chỉ cần:
  - Bấm nút **`📝 OCR`** trên thanh công cụ hoặc nhấn phím tắt **`Ctrl + T`**.
  - CodePin sẽ nhận diện toàn bộ văn bản/dòng code trong ảnh và **tự động sao chép thẳng vào Clipboard**.
  - Bạn chỉ cần sang VS Code nhấn `Ctrl + V` là xong, không cần gõ lại bằng tay!

### 2. 🪟 Ghim cả Cửa Sổ Ứng Dụng Khác (Window Pinning)
- Không chỉ ghim ảnh, bạn có thể ghim **bất kỳ ứng dụng nào** (Notepad, Terminal, Calculator, trình duyệt...):
  - **Cách 1 (Nhanh nhất):** Click vào cửa sổ muốn ghim rồi bấm tổ hợp phím tắt toàn cục **`Ctrl + Alt + T`**.
  - **Cách 2:** Bấm nút **`🪟 Ghim App`** trên thanh công cụ để mở danh sách toàn bộ các cửa sổ đang chạy và bật/tắt ghim bằng một cú click.

### 3. 🔄 Tự nhớ vị trí & Khôi phục khi mở lại (Session Restore)
- CodePin tự động ghi nhớ vị trí tọa độ, kích thước, độ mờ (opacity), tỷ lệ thu phóng (zoom) và **bức ảnh bạn đang xem dở**.
- Khi tắt máy hoặc đóng app rồi mở lại: Toàn bộ ảnh và trạng thái sẽ tự động được khôi phục nguyên vẹn.
- Muốn làm mới phiên làm việc: Chuột phải vào ảnh chọn **`🗑️ Xoá ảnh / Làm mới phiên`**.

---

### 4. 🗔 Chạy ngầm dưới Khay Hệ Thống (System Tray)
- Khi bấm nút **`✕`** hoặc nhấn **`Esc`**: CodePin sẽ tự động **thu nhỏ xuống khay hệ thống (System Tray)** thay vì tắt hẳn.
- **Bấm đúp** vào biểu tượng 📌 dưới khay hoặc bấm chuột phải chọn **`📌 Mở CodePin`** để mở lại ngay lập tức mà không làm mất ảnh đang ghim.
- Các phím tắt toàn cục như **`Ctrl + Alt + T`** (ghim cửa sổ khác) vẫn luôn hoạt động kể cả khi CodePin đang ẩn dưới khay.
- Để thoát hoàn toàn: Chuột phải vào biểu tượng khay hoặc trên cửa sổ chọn **`❌ Thoát hoàn toàn`**.

---

## ⌨️ Bảng Phím Tắt & Thao Tác Toàn Diện

| Thao tác | Phím tắt / Chuột | Chức năng |
| :--- | :--- | :--- |
| **Trích xuất Code (OCR)** | **`Ctrl + T`** hoặc nút `📝 OCR` | Đọc toàn bộ code trong ảnh và copy vào Clipboard |
| **Ghim cửa sổ app bất kỳ** | **`Ctrl + Alt + T`** | Ghim hoặc bỏ ghim cửa sổ đang trỏ chuột |
| **Mở danh sách cửa sổ** | Nút `🪟 Ghim App` | Xem và chọn cửa sổ để ghim |
| **Dán ảnh siêu tốc** | **`Ctrl + V`** | Dán ảnh chụp từ Clipboard (`Win + Shift + S`) |
| **Kéo thả file** | Kéo thả file ảnh vào app | Mở nhanh ảnh PNG, JPG, BMP, WEBP, GIF... |
| **Phóng to / Thu nhỏ** | Lăn chuột (Scroll Wheel) | Phóng to để soi chi tiết code nhỏ |
| **Chỉnh độ mờ (Opacity)** | **`Shift` + Lăn chuột** | Làm mờ từ 20% đến 100% để nhìn xuyên qua code bên dưới |
| **Cuộn vùng xem (Pan)** | Chuột giữa **hoặc** `Space` + Kéo | Di chuyển ảnh khi đang phóng to (tiện cho laptop) |
| **Đặt lại kích thước** | `Ctrl + 0` hoặc Click đúp ảnh | Đưa ảnh về tỷ lệ chuẩn 100% |
| **Bật/Tắt Ghim CodePin** | Nút `📌` hoặc Menu chuột phải | Bật/tắt chế độ Always-on-top của CodePin |
| **Chế độ xuyên chuột** | **`F8`** hoặc **`Ctrl + Alt + P`** | Chuột click xuyên qua ảnh xuống IDE bên dưới |
| **Kéo đổi kích thước** | Kéo góc dưới cùng bên phải | Co giãn khung hiển thị linh hoạt |
| **Thu nhỏ xuống khay** | **`Esc`** hoặc nút `✕` | Ẩn xuống khay hệ thống, tiện mở lại tức thì |
| **Thoát hoàn toàn** | Menu chuột phải -> `Thoát` | Tắt hoàn toàn ứng dụng và lưu phiên |
