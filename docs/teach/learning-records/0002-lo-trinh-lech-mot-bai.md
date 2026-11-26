# Lộ trình lệch một bài: update lớn chen trước kỹ năng đọc code

Kế hoạch ban đầu (learning record 0001) là bài 02 dạy đọc một file C# thật. Thực tế bài 02 lại là update 26/11/2026 — wave chuyển vào `EnemySpawner`, thêm vật cản, sửa màn 1-4 — vì người học vừa làm xong feature đó với agent và cần hiểu thứ mình vừa thay đổi. Kỹ năng đọc code được dời sang bài 03 ("Một con quái đi qua những file nào"), nơi luồng runtime được trace đúng 8 bước có file:dòng.

**Implications**: người học tiếp xúc code qua việc mình vừa làm, không qua ví dụ trừu tượng — các bài sau nên bám vào thay đổi thật trong repo (bug vừa sửa, file vừa tạo) thay vì dựng bài tập giả. Bài 03 mới chỉ là đọc; chưa có bằng chứng người học tự phân loại được Data/Logic/View hay tự sửa một file mà không làm hỏng file khác, nên bài 04 nên là bài tập phân loại + sửa một chỉ số (asset) và một câu chữ (HUD) rồi kiểm chứng bằng `StageDataValidator`.
