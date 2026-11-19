-- ===========================================
-- SQL Script: เพิ่ม column ใหม่ใน movies table
-- สำหรับ database: absolute_cinema
-- ===========================================

-- เพิ่ม column ใหม่ให้ตาราง movies
ALTER TABLE movies
    ADD COLUMN Genre VARCHAR(100) NULL AFTER Status,
    ADD COLUMN Description TEXT NULL AFTER Genre,
    ADD COLUMN PosterUrl VARCHAR(500) NULL AFTER Description,
    ADD COLUMN TrailerUrl VARCHAR(500) NULL AFTER PosterUrl,
    ADD COLUMN PriceMin DECIMAL(10,2) NOT NULL DEFAULT 0.00 AFTER TrailerUrl,
    ADD COLUMN PriceMax DECIMAL(10,2) NOT NULL DEFAULT 0.00 AFTER PriceMin,
    ADD COLUMN Rating DOUBLE NOT NULL DEFAULT 0 AFTER PriceMax,
    ADD COLUMN ReleaseDate DATE NULL AFTER Rating;

-- เพิ่มที่นั่ง (Seats) ให้ cinema
ALTER TABLE cinemas
    ADD COLUMN TotalSeats INT NOT NULL DEFAULT 100 AFTER Status;

-- เพิ่มที่นั่งที่จอง (SeatNumbers) และจำนวนที่นั่ง (SeatCount) ให้ bookings
ALTER TABLE bookings
    ADD COLUMN SeatNumbers VARCHAR(500) NULL AFTER TotalAmount,
    ADD COLUMN SeatCount INT NOT NULL DEFAULT 1 AFTER SeatNumbers;

-- ===========================================
-- Seed ข้อมูลตัวอย่าง: หนังที่กำลังฉาย
-- ===========================================

-- เพิ่มโรงภาพยนตร์ (ถ้ายังไม่มี)
INSERT IGNORE INTO cinemas (CinemaId, CinemaName, Status, TotalSeats) VALUES
(1, 'IMAX Hall 1', 'Ready', 200),
(2, 'Dolby Atmos Hall 2', 'Ready', 180),
(3, 'Standard Hall 3', 'Ready', 150),
(4, 'VIP Hall 4', 'Ready', 80);

-- เพิ่มหนังตัวอย่าง
INSERT INTO movies (Title, DurationMins, Status, Genre, Description, PosterUrl, PriceMin, PriceMax, Rating, ReleaseDate) VALUES
('ตุ๊ดตู่กู้ชาติ: ยุทธการตุ๊ดครองเมือง', 120, 'Showing', 'ตลก / แอ็คชัน', 'ภาพยนตร์ตลกแอ็คชันที่จะทำให้คุณหัวเราะจนน้ำตาไหล', NULL, 240, 380, 8.5, '2026-10-15'),
('โคกอาศิ', 130, 'Showing', 'ดราม่า', 'เรื่องราวดราม่าสุดซึ้ง', NULL, 200, 350, 7.8, '2026-10-20'),
('มหัศจรรย์ลิขิต', 140, 'Showing', 'แฟนตาซี / ผจญภัย', 'การผจญภัยในโลกแฟนตาซีที่ยิ่งใหญ่', NULL, 240, 380, 9.1, '2026-11-01'),
('สายลับ จิบ ดาย', 115, 'Showing', 'แอ็คชัน / ทริลเลอร์', 'หนังสายลับสุดมันส์', NULL, 200, 350, 7.5, '2026-11-05'),
('บทเพลงสุดโหด', 125, 'Showing', 'ดราม่า / เพลง', 'เรื่องราวของนักดนตรีที่ต่อสู้เพื่อความฝัน', NULL, 180, 320, 8.0, '2026-10-25'),
('สมรภูมิผู้กล้า', 155, 'Showing', 'แอ็คชัน / สงคราม', 'มหากาพย์สงครามที่ยิ่งใหญ่', NULL, 240, 380, 8.8, '2026-10-10'),
('ตั๋ววาสนา', 105, 'Showing', 'ตลก / โรแมนติก', 'เมื่อโชคชะตาพาให้รักกัน', NULL, 180, 320, 7.2, '2026-11-03'),
('ซาคอนอามีโรงเรียนหำ', 110, 'Showing', 'ตลก / วัยรุ่น', 'หนังตลกวัยรุ่นสุดฮา', NULL, 180, 320, 7.0, '2026-10-28'),
('มหานครไฟว 2099', 150, 'ComingSoon', 'ไซไฟ / แอ็คชัน', 'อนาคตที่เทคโนโลยีครองโลก', NULL, 250, 400, 0, '2026-12-15'),
('หัวใจดวงสุดท้าย', 120, 'ComingSoon', 'โรแมนติก / ดราม่า', 'เรื่องราวรักสุดซึ้ง', NULL, 200, 350, 0, '2026-12-20');

-- เพิ่มรอบฉาย (Showtimes) สำหรับหนังที่กำลังฉาย
-- วันนี้ + 7 วันข้างหน้า
INSERT INTO showtimes (MovieId, CinemaId, StartTime, EndTime) VALUES
-- ตุ๊ดตู่กู้ชาติ (MovieId ตาม auto_increment)
(1, 1, CONCAT(CURDATE(), ' 11:30:00'), CONCAT(CURDATE(), ' 13:30:00')),
(1, 1, CONCAT(CURDATE(), ' 14:15:00'), CONCAT(CURDATE(), ' 16:15:00')),
(1, 1, CONCAT(CURDATE(), ' 17:00:00'), CONCAT(CURDATE(), ' 19:00:00')),
(1, 2, CONCAT(CURDATE(), ' 19:40:00'), CONCAT(CURDATE(), ' 21:40:00')),
-- โคกอาศิ
(2, 2, CONCAT(CURDATE(), ' 12:00:00'), CONCAT(CURDATE(), ' 14:10:00')),
(2, 2, CONCAT(CURDATE(), ' 15:00:00'), CONCAT(CURDATE(), ' 17:10:00')),
(2, 3, CONCAT(CURDATE(), ' 19:30:00'), CONCAT(CURDATE(), ' 21:40:00')),
-- มหัศจรรย์ลิขิต
(3, 1, CONCAT(CURDATE(), ' 13:00:00'), CONCAT(CURDATE(), ' 15:20:00')),
(3, 3, CONCAT(CURDATE(), ' 16:15:00'), CONCAT(CURDATE(), ' 18:35:00')),
(3, 3, CONCAT(CURDATE(), ' 19:00:00'), CONCAT(CURDATE(), ' 21:20:00')),
(3, 4, CONCAT(CURDATE(), ' 21:00:00'), CONCAT(CURDATE(), ' 23:20:00')),
-- สายลับ จิบ ดาย
(4, 2, CONCAT(CURDATE(), ' 12:45:00'), CONCAT(CURDATE(), ' 14:40:00')),
(4, 4, CONCAT(CURDATE(), ' 16:15:00'), CONCAT(CURDATE(), ' 18:10:00')),
(4, 4, CONCAT(CURDATE(), ' 19:00:00'), CONCAT(CURDATE(), ' 20:55:00')),
(4, 4, CONCAT(CURDATE(), ' 19:50:00'), CONCAT(CURDATE(), ' 21:45:00'));

