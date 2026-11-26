PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE users (
 id TEXT PRIMARY KEY,
 username TEXT NOT NULL UNIQUE,
 password_hash TEXT NOT NULL,
 full_name TEXT NOT NULL DEFAULT '',
 phone TEXT NOT NULL DEFAULT '',
 address TEXT NOT NULL DEFAULT '',
 role TEXT NOT NULL DEFAULT 'User' CHECK(role IN ('User','Admin')),
 status TEXT NOT NULL DEFAULT 'Active' CHECK(status IN ('Active','Locked','Deleted')),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL
);
INSERT INTO users VALUES('a965eeea99aa4c7eab237ad289a2af3f','test1','AQAAAAIAAYagAAAAEJQQ55RyHBXz/BQDGwHJMtzuEjG/hwX9a+Zs7orlqjsZzVAdBKteNsJmA1tr8tdqUQ==','Lăng Kinh Hư Không','0949492972','An Giang','User','Active','2026-10-16T19:32:29.6279352+00:00','2026-10-17T01:23:56.8940921+00:00');
INSERT INTO users VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','admin','AQAAAAIAAYagAAAAEMs9QnaFwJNEgZHGBmQY0MtQ8oogViqMtp3/o+lfVCtl0E/k3zke1G+OM08tcBWD1w==','Lăng Kính Hư Không','','','Admin','Active','2026-10-16T19:38:56.845088+00:00','2026-10-16T19:38:56.845088+00:00');
INSERT INTO users VALUES('36ca78fc343c4da5aa65a2ad48b3a944','testnopremium','AQAAAAIAAYagAAAAEGWOlvGilatOndQm6UGanVpHbnJ3W37UtezxMr8VgDgYGZ7SNDorEUVHGV2NKgUjZw==','LKHK','','','User','Locked','2026-10-21T06:26:26.5817331+00:00','2026-10-26T08:37:46.9855042+00:00');
INSERT INTO users VALUES('68a743f0cd774650ad5da802b4ea20d7','hoivienyoutube','AQAAAAIAAYagAAAAEEhU4MfeorqV0/rwgYsI2XDLwtxA4NbrHEONZNrD/r1/AFPd2DndpyHb+Ntfc86+0w==','Hội viên T10','','','User','Locked','2026-10-24T16:34:38.4974741+00:00','2026-10-26T08:37:36.1694568+00:00');
INSERT INTO users VALUES('6c736976b6754717aaa008ccb9f12976','hoivient10','AQAAAAIAAYagAAAAEAVAT/rtAF8aE4RVHejh6vGZ5sNsFiMaSjUzBvDwAtPFlPjcpwwLWmoky0LZApQrSQ==','Hội viên T10','','','User','Active','2026-10-24T16:35:40.7480220+00:00','2026-10-24T16:35:40.7480220+00:00');
INSERT INTO users VALUES('d4a574a4b6af4d6785f443c27976e891','hoivient11','AQAAAAIAAYagAAAAEAwiXQhZMvrkmFw3BBSnp8qRp/x8RaHuOz4EnNQ1WyHeIG8fKxUF0x7fjYucgypuCA==','Độ kiếp','','','User','Active','2026-10-29T07:23:33.7006488+00:00','2026-10-29T07:23:33.7006488+00:00');
INSERT INTO users VALUES('ce145e6154f54d418c3cdae288d74804','hoang260887','AQAAAAIAAYagAAAAEESjp4nA1QWNrdcHcOzAWipyJmnBXmCqsyPsRdAWQusrrW7AFCehBTa2V9iSm6m9oA==','lê hoàng','0398616989','thái bình','User','Active','2026-11-20T09:02:13.8439368+00:00','2026-11-20T09:02:13.8439368+00:00');
INSERT INTO users VALUES('b4c3a0b489264d9488066360b6d05e30','hoivient12','AQAAAAIAAYagAAAAEMoTEZE0EBGb2iW1/s6gxXYay0J4yz/mp9RrW2pW61jNa63IpqTU9nvuLAU9fE9fDw==','youtube hội viên T12','','','User','Active','2026-11-25T06:43:38.9924713+00:00','2026-11-25T06:43:38.9924713+00:00');
CREATE TABLE stories (
 id TEXT PRIMARY KEY,
 slug TEXT NOT NULL UNIQUE,
 title TEXT NOT NULL,
 author TEXT NOT NULL DEFAULT '',
 genre TEXT NOT NULL DEFAULT '',
 description TEXT NOT NULL DEFAULT '',
 cover_url TEXT NOT NULL DEFAULT '',
 cover_file TEXT NOT NULL DEFAULT '',
 storage_prefix TEXT NOT NULL DEFAULT '',
 status TEXT NOT NULL DEFAULT 'Draft' CHECK(status IN ('Draft','Published','Hidden')),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL
, drive_folder_id TEXT NOT NULL DEFAULT '');
INSERT INTO stories VALUES('efffe4516265459ca5a0d8e56cf4f69b','am-duong-tao-hoa-kinh','Âm Dương Tạo Hóa Kinh','Tàn Dương','Huyền Huyễn',replace('• Nhân vật chính: Hồ Nguyên Vũ – một cựu giám đốc marketing xuyên không nhập vào thân xác của Cửu hoàng tử Đại Nam Thần Triều (con trai Âu Cơ Thánh Tôn).\n• Bối cảnh: Diễn ra tại Lạc Hồng Tinh, thời đại Vô Tiên sau khi Thế Giới Chi Thụ (Đa Thần Thụ) bị chặt đứt cách đó 1 tỷ năm. Khi linh khí cạn kiệt, các tu sĩ, tiên nhân và ma tộc phải tranh đoạt cơ duyên để tìm con đường trường sinh.\n• Nội dung chính: Xoay quanh hành trình nghịch thiên cải mệnh, vươn lên trong một bàn cờ vận mệnh bao trùm chư thiên vạn giới của nhân vật chính Hồ Nguyên Vũ','\n',char(10)),'','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Image/cover-4467acfa3232427cb7fbfa8bf84a02a1.png','stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b','Published','2026-10-16T19:40:39.2621381+00:00','2026-11-04T08:42:47.7904648+00:00','1v5avgGrPdVK6wmtsoixWlmlZSsG-RV3F');
INSERT INTO stories VALUES('28006d5e7067438d9adfd2c690be897d','su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai','Sư Nương Tuyệt Sắc Đuổi Tôi Đi, Tình Cờ Sống Chung Với Nữ Tổng Tài','Lăng Phong','Đô Thị','Sau khi xuống núi vì bị người sư nương tuyệt sắc xua đuổi, nam chính Lăng Phong bước vào chốn đô thị phồn hoa. Tại đây, anh tình cờ gặp gỡ và bắt đầu cuộc sống ở chung (đồng cư) đầy những tình huống dở khóc dở cười nhưng không kém phần ngọt ngào với một nữ tổng tài xinh đẹp, kiêu kỳ. Sở hữu bản lĩnh phi thường cùng tài nghệ được sư nương truyền dạy, Lăng Phong từng bước giải quyết các sóng gió đô thị, bảo vệ mỹ nhân và xây dựng thế lực của riêng mình.','','r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/Image/cover-f163e67c6f614da0a9fef001461e23f6.png','stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d','Published','2026-10-18T10:09:23.7324956+00:00','2026-10-18T10:10:24.3507007+00:00','1H0G7DjfB94EC7DAT3KEMDgTJguMAMUTF');
INSERT INTO stories VALUES('dd9a6040fdcd4922b8651b2d1bf3c087','moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh','Mối Quan Hệ Bí Mật Giữa Những Người Trong Gia Đình','Tranh Phong Cảnh','Đô Thị','Thể loại gia đình 18+ nặng, anh em cân nhắc trước khi nghe','','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Image/cover-ac42ff625c5d42aa9ff60bc2cd1c7fa0.png','stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087','Published','2026-10-20T16:36:10.6630372+00:00','2026-11-04T08:43:11.9775984+00:00','11DHL1IJvU7DIDNHHLsolEhzQvZ4aPvEl');
INSERT INTO stories VALUES('93d4ff86b54848a0a4231fe9ef6a5520','hanh-phuc-mot-gia-dinh-thieu-cha','Hạnh Phúc Một Gia Đình Thiếu Cha','Tổng hợp - Lăng Kính Hư Không','Đô Thị','Bộ truyện về cậu con trai mới lớn trong gia đình tình tiết 18+ nặng cân nhắc trước khi nghe','','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Image/cover-5aae087f2c1e442481e68f7408704d28.png','stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520','Published','2026-10-21T06:19:12.3984572+00:00','2026-11-04T08:43:19.6333019+00:00','1rtFd6sf9z_Bi0ua551rOA3tL83BPed1o');
INSERT INTO stories VALUES('95603483874d4db5ba1957bd2cadadc3','huyen-hoac','Huyễn hoặc','Lão Tây','Đô Thị','- Tổng hợp từ nhiều nguồn','','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Image/cover-8b463f8e640e4ef0a568ba9e69b10726.png','stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3','Published','2026-11-05T06:17:58.7995184+00:00','2026-11-05T09:18:10.2674394+00:00','143T6wFVW5RJsfVX_AlbJKuecgjtQiUUk');
INSERT INTO stories VALUES('884e4166dd7946c0bbbb3a84c60417d2','tan-vuong-bao-sung-y-phi-ngong-cuong','TÀN VƯƠNG BẠO SỦNG Y PHI NGÔNG CUỒNG','Túy Linh Túc','Xuyên Không',replace('- Một tiểu thư sa ngã đã lặng lẽ chết trong kiệu cưới. Khi tỉnh dậy, nàng thấy mình xuyên không vào thân xác một thiên tài y thuật.\n- Con gái của một danh y lại bắt nạt nàng, xé rách áo cưới và ép nàng hủy hôn ước? Vị hôn phu của nàng lại bảo vệ người khác, làm nhục, khinh miệt và đe dọa nàng? Một lũ vô ơn trong gia tộc muốn ép ông nội nàng phải chết và chiếm đoạt gia sản? Fu Zhaoning xắn tay áo lên và đối mặt với thử thách. Nàng sẽ thanh toán hết mọi ân oán, hủy hôn ước ngay lập tức, giết chết mọi kẻ vô ơn, và tiêu diệt từng tên khốn nạn!\n- Nàng cũng sẽ đối đầu với Thái tử Jun quyền lực, người có địa vị thống trị toàn bộ kinh đô.\n- Thái tử: Ta đã sai, ta cầu xin tha thứ, cầu xin hòa bình, cầu xin một cái ôm…','\n',char(10)),'','','stories/tan-vuong-bao-sung-y-phi-ngong-cuong--884e4166dd7946c0bbbb3a84c60417d2','Draft','2026-11-06T08:24:31.4751287+00:00','2026-11-06T08:24:31.4751287+00:00','1lqqxQ1kzeD1dV0fzJuwXrVM2BCDX925W');
INSERT INTO stories VALUES('b68f184a75e9494d8cdd82114a9fee6c','kieu-kieu-su-nuong','Kiều Kiều Sư Nương','Thanh Trà Đạm Phạn','Sắc Hiệp','Lăng Phong lên phái Hoa Sơn bái sư học võ. Nhờ có cơ duyên đặc biệt, hắn vô tình lĩnh hội được bí pháp song tu tuyệt đỉnh (Tiêu Dao Ngự Nữ Tâm Kinh) từ đời trước để lại. Từ đây, hắn từng bước chinh phục các mỹ nhân trong môn phái và trên giang hồ, bao gồm sư nương, các sư tỷ (như tam sư tỷ Đàm Uyển Phượng, ngũ sư tỷ Tạ Lâm Lan, lục sư tỷ Lục Phỉ Nhi), cho đến các nhân vật trong triều đình.','','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Image/cover-5d89123f90bb4aef888bc94c8266b761.png','stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c','Published','2026-11-06T09:52:53.7962202+00:00','2026-11-11T00:27:45.1346620+00:00','1uuC4KnZmxjraz0Krojm0_MImzC44tE7y');
INSERT INTO stories VALUES('de38c2eaa8fa492fbfde8c5a842ac969','xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia','Xuyên Thành Pháo Hôi Ăn Bám Thành Đại Gia','Tổng hợp','Đô Thị','Chuyện gì xảy ra khi một thanh niên xui xẻo xuyên không vào thế giới tiểu thuyết, lại ngay lập tức sắm vai một tên pháo hôi (nhân vật làm nền cho cốt truyện chính) có cái kết thảm khốc dưới tay nữ phản diện?','','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Image/cover-2655e08b3c524510811fdb3a8f5a2154.png','stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969','Published','2026-11-06T10:01:15.1867577+00:00','2026-11-06T10:01:32.4227526+00:00','');
INSERT INTO stories VALUES('7ba4eb40d6ee4acb872fc613e2d96ba6','trung-sinh-sung-me-sm','Trùng sinh sủng mẹ SM','','Xuyên Không','Web: Lăng Kính Hư Không','','','stories/trung-sinh-sung-me-sm--7ba4eb40d6ee4acb872fc613e2d96ba6','Draft','2026-11-10T07:19:49.1900833+00:00','2026-11-10T07:19:49.1900833+00:00','1PrgWoUeIGK3wi4FmJ082yG8YnME3r8YV');
INSERT INTO stories VALUES('4b51262e153e4f379e024c7dd3eed9c4','cau-hinh-config','Cấu hình Config','LKHK','Khác','','','','stories/cau-hinh-config--4b51262e153e4f379e024c7dd3eed9c4','Draft','2026-11-25T07:35:13.7034311+00:00','2026-11-25T07:35:13.7034311+00:00','1MWYOX6nx1K4_9roszgKj8SHfxaBpIR9B');
CREATE TABLE episodes (
 id TEXT PRIMARY KEY,
 story_id TEXT NOT NULL REFERENCES stories(id),
 title TEXT NOT NULL,
 episode_number INTEGER NOT NULL,
 full_file TEXT NOT NULL,
 preview_file TEXT NOT NULL,
 hls_ref TEXT NOT NULL DEFAULT '',
 duration_seconds INTEGER NOT NULL DEFAULT 0,
 status TEXT NOT NULL DEFAULT 'Published' CHECK(status IN ('Published','Hidden')),
 created_at TEXT NOT NULL,
 UNIQUE(story_id, episode_number)
);
INSERT INTO episodes VALUES('a0c06ab17b2a4b9f946db3a5ebfb4037','efffe4516265459ca5a0d8e56cf4f69b','Chương 1-7: Giới thiệu',1,'r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Episodes/Tap-000007-4d7d535d956c458493b6dba4b67cdd6b.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Previews/Tap-000007-4d7d535d956c458493b6dba4b67cdd6b.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/HLS/Tap-000007-a2780dceb3f54bb7bae93561a18629f2',2591,'Published','2026-10-16T19:54:16.2030589+00:00');
INSERT INTO episodes VALUES('399e00fb883647ca919016bf20b81da6','efffe4516265459ca5a0d8e56cf4f69b','Chương 8-13: Cảm giác lạ',2,'r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Episodes/Tap-000013-0b4308920f7940978ed45a97b048494a.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Previews/Tap-000013-0b4308920f7940978ed45a97b048494a.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/HLS/Tap-000013-0cd3e8ad0bcb46e6a2894ccb4a407341',2163,'Published','2026-10-16T19:56:59.3859830+00:00');
INSERT INTO episodes VALUES('069620a9629e4eceb8034d29c6b28449','efffe4516265459ca5a0d8e56cf4f69b','Chương 14-18: Âm Dương Tạo Hóa',3,'r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Episodes/Tap-000004-842ebdc199a14520974859221f1d775c.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Previews/Tap-000004-842ebdc199a14520974859221f1d775c.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/HLS/Tap-000004-37c1b9a7e435466384cd25527400c3c3',1788,'Published','2026-10-16T19:59:18.6254526+00:00');
INSERT INTO episodes VALUES('c5300a06cdc1444eafaaba6feddca88a','efffe4516265459ca5a0d8e56cf4f69b','Chương 19-24: Chinh phục',4,'r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Episodes/Tap-000024-b3d05411e9d94dfa97ee181d8bac6100.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Previews/Tap-000024-b3d05411e9d94dfa97ee181d8bac6100.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/HLS/Tap-000024-ecf53cce019344ccb29e9e8dccf42c51',2102,'Published','2026-10-16T20:05:02.2001273+00:00');
INSERT INTO episodes VALUES('f824e5d1d88c41b8a0f3fd8f78765771','28006d5e7067438d9adfd2c690be897d','Chương 01-20: Ta Xuống Núi',1,'r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/Episodes/Tap-000001-9fc1986b9991421da3313e3d9efd89b9.mp3','r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/Previews/Tap-000001-9fc1986b9991421da3313e3d9efd89b9.mp3','r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/HLS/Tap-000001-aacfd54f339d4ab8a54ee44ea8fda2cd',3970,'Published','2026-10-18T10:15:14.0949658+00:00');
INSERT INTO episodes VALUES('d6ee5625936e4ed6aaab818e7e1fb6da','28006d5e7067438d9adfd2c690be897d','Chương 21-43: NGƯƠI THỬ ĐỘNG VÀO MỘT SỢI TÓC CỦA ANH ẤY XEM',2,'r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/Episodes/Tap-000002-897ab378ac3e4d3e855250c9bb9edcc0.mp3','r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/Previews/Tap-000002-897ab378ac3e4d3e855250c9bb9edcc0.mp3','r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/HLS/Tap-000002-b071650994f1417894ee905d3b690f22',3825,'Published','2026-10-20T15:33:12.7740604+00:00');
INSERT INTO episodes VALUES('47e1644c1d824fd38212c10deb4c7ec2','dd9a6040fdcd4922b8651b2d1bf3c087','Chương 01-07: Phần đầu',1,'r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Episodes/Tap-000001-85c31888774a4020934edba3e6d0c001.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Previews/Tap-000001-85c31888774a4020934edba3e6d0c001.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/HLS/Tap-000001-e92d4a4d6a7447b9b99c03a1952a731e',4660,'Published','2026-10-20T16:59:39.2961102+00:00');
INSERT INTO episodes VALUES('121da1cbf2f64d38ade6cff885d666ef','dd9a6040fdcd4922b8651b2d1bf3c087','Chương 08-13: Chi phục',2,'r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Episodes/Tap-000002-87656d1ed7a04e60abb7b499adf49412.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Previews/Tap-000002-87656d1ed7a04e60abb7b499adf49412.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/HLS/Tap-000002-a6757f4f0a174e19a1e941d9fe8168a0',4563,'Published','2026-10-20T17:39:06.8171292+00:00');
INSERT INTO episodes VALUES('148afe30024247e3b95f96f0de2a181a','dd9a6040fdcd4922b8651b2d1bf3c087','Chương 14-18: Mẹ nuôi',3,'r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Episodes/Tap-000003-59056d03dd6d473c8d8489a65b7210bf.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Previews/Tap-000003-59056d03dd6d473c8d8489a65b7210bf.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/HLS/Tap-000003-fb885869ebde498195183a2cf30ed4c8',3591,'Published','2026-10-20T23:43:53.9854922+00:00');
INSERT INTO episodes VALUES('f345e6e3ea5a4e3f84f487cc1ad5170e','93d4ff86b54848a0a4231fe9ef6a5520','Tập 1: Hạnh Phúc Một Gia Đình Thiếu Cha',1,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000001-5d66fbf4985f44f4b19d757e5a2988e4.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000001-5d66fbf4985f44f4b19d757e5a2988e4.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000001-c9d257f1f67b4806964021488a423f86',4537,'Published','2026-10-21T06:23:49.4735721+00:00');
INSERT INTO episodes VALUES('f7031da17ba64a46b45f3aa79087b3f7','93d4ff86b54848a0a4231fe9ef6a5520','Tập 2: Hạnh Phúc Một Gia Đình Thiếu Cha',2,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000002-8f40ea2e85f947f8abff209915239410.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000002-8f40ea2e85f947f8abff209915239410.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000002-eca79c6362b44f60b9548d25f6e6a34c',4515,'Published','2026-10-21T06:28:01.0399242+00:00');
INSERT INTO episodes VALUES('a30e70eddb0d4ad29eb7518b6e38a4b5','93d4ff86b54848a0a4231fe9ef6a5520','Tập 3: Hạnh Phúc Một Gia Đình Thiếu Cha',3,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000003-d8bd81e48fae4695bbce4c5370b525a1.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000003-d8bd81e48fae4695bbce4c5370b525a1.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000003-2f56b17daff24e238a465f131076f1ef',4563,'Published','2026-10-21T06:39:59.1685115+00:00');
INSERT INTO episodes VALUES('67651e41446e46b1a83cb7fb0369b623','93d4ff86b54848a0a4231fe9ef6a5520','Tập 4: Hạnh Phúc Một Gia Đình Thiếu Cha',4,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000004-757994b02a1147f4bd722487a189be36.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000004-757994b02a1147f4bd722487a189be36.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000004-75d0cf3e4d454a2bbfd465c7eed20fd5',4537,'Published','2026-10-21T06:45:18.6358775+00:00');
INSERT INTO episodes VALUES('75ed634d6e1d4916814064abd2445c96','93d4ff86b54848a0a4231fe9ef6a5520','Tập 5: Hạnh Phúc Một Gia Đình Thiếu Cha',5,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000005-5ce3f9effdad4c47ab2d4acea5a46c56.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000005-5ce3f9effdad4c47ab2d4acea5a46c56.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000005-2672260dee6a47adb5186d2820f23516',4572,'Published','2026-10-21T07:16:19.1272562+00:00');
INSERT INTO episodes VALUES('d902c233a95d4e5db938b7edb45ef742','28006d5e7067438d9adfd2c690be897d','Chương 44-75: RA ĐÂY NHẬN LỖI!',3,'r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/Episodes/Tap-000003-e0f494014187436b899b8c8a62312601.mp3','r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/Previews/Tap-000003-e0f494014187436b899b8c8a62312601.mp3','r2:stories/su-nuong-tuyet-sac-duoi-toi-di-tinh-co-song-chung-voi-nu-tong-tai--28006d5e7067438d9adfd2c690be897d/HLS/Tap-000003-8c8d4a448cf543cb9bce94b593cc36ec',4699,'Published','2026-10-21T07:31:20.7170343+00:00');
INSERT INTO episodes VALUES('799b443d9f6e46828f33eba39384f566','93d4ff86b54848a0a4231fe9ef6a5520','Tập 6: Hạnh Phúc Một Gia Đình Thiếu Cha',6,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000006-4a5f3c93dc2640e8ae0ee5d16942f743.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000006-4a5f3c93dc2640e8ae0ee5d16942f743.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000006-b16271466e8d4cde99c7dc77d8d7d5ec',4362,'Published','2026-10-21T07:36:02.2613275+00:00');
INSERT INTO episodes VALUES('14c847ac12794f2d8ae01b7239902111','93d4ff86b54848a0a4231fe9ef6a5520','Tập 7: Hạnh Phúc Một Gia Đình Thiếu Cha',7,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000007-0d7facac03ee4ab6a39d81f34e812256.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000007-0d7facac03ee4ab6a39d81f34e812256.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000007-c287c67eb2014564b69d8a3b76d32670',4561,'Published','2026-10-21T08:24:05.9933068+00:00');
INSERT INTO episodes VALUES('62541b841b194cff8a7c8dee5756ce7b','93d4ff86b54848a0a4231fe9ef6a5520','Tập End: Hạnh Phúc Một Gia Đình Thiếu Cha',8,'r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Episodes/Tap-000008-628396201492490ab3e214953912efe6.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/Previews/Tap-000008-628396201492490ab3e214953912efe6.mp3','r2:stories/hanh-phuc-mot-gia-dinh-thieu-cha--93d4ff86b54848a0a4231fe9ef6a5520/HLS/Tap-000008-e318a76ab1044b72bf876b52c71cbb86',1764,'Published','2026-10-21T08:40:01.4807962+00:00');
INSERT INTO episodes VALUES('3d127d2e55584332adb83cde14089251','dd9a6040fdcd4922b8651b2d1bf3c087','Chương 19-End: Happy endding',4,'r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Episodes/Tap-000004-add3843cdc314c6d98448edde221c35e.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/Previews/Tap-000004-add3843cdc314c6d98448edde221c35e.mp3','r2:stories/moi-quan-he-bi-mat-giua-nhung-nguoi-trong-gia-dinh--dd9a6040fdcd4922b8651b2d1bf3c087/HLS/Tap-000004-af8e8dff57cf4bf0b62189ee2cc1297d',4231,'Published','2026-10-22T13:59:19.7375795+00:00');
INSERT INTO episodes VALUES('e043e1833224430e98e7bcd07f0ead9a','efffe4516265459ca5a0d8e56cf4f69b','Chương 25-35: Trực giác đáng sợ của nữ nhân',5,'r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Episodes/Tap-000005-48ca7f91200c4087baf3b45a2cad1c5a.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/Previews/Tap-000005-48ca7f91200c4087baf3b45a2cad1c5a.mp3','r2:stories/am-duong-tao-hoa-kinh--efffe4516265459ca5a0d8e56cf4f69b/HLS/Tap-000005-2012dcd9367745f98a9677438aff29a6',4117,'Published','2026-11-04T09:27:54.8827358+00:00');
INSERT INTO episodes VALUES('3729b1849e774977aec218dca452083d','95603483874d4db5ba1957bd2cadadc3','Chương 01-10',1,'r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Episodes/Tap-000001-ba1b409584bd467cbb9e90d8875432fd.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Previews/Tap-000001-ba1b409584bd467cbb9e90d8875432fd.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/HLS/Tap-000001-8f1d7936b69a4d1eaf74617ad213956b',2209,'Published','2026-11-05T06:21:04.9483465+00:00');
INSERT INTO episodes VALUES('bea2531c71444dc887074ba96b117aef','95603483874d4db5ba1957bd2cadadc3','Chương 11-20',2,'r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Episodes/Tap-000002-d981985cb111429b80beb88a2dc4e47a.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Previews/Tap-000002-d981985cb111429b80beb88a2dc4e47a.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/HLS/Tap-000002-3e823f0f96fa4e8fb71cc2747a977501',2005,'Published','2026-11-05T07:50:59.9622873+00:00');
INSERT INTO episodes VALUES('a489d03ecddf426c83c5a9e4eafcbe8a','95603483874d4db5ba1957bd2cadadc3','Chương 21-30',3,'r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Episodes/Tap-000003-d457c2885a1942b39ef304ebea0dd81d.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Previews/Tap-000003-d457c2885a1942b39ef304ebea0dd81d.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/HLS/Tap-000003-70e11e51d330425baffdcfb871dc8a1a',2006,'Published','2026-11-05T08:40:28.0319634+00:00');
INSERT INTO episodes VALUES('a32f8a0482b742aea338e894766b0da4','95603483874d4db5ba1957bd2cadadc3','Chương 31-40',4,'r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Episodes/Tap-000004-6e894e03899a445da7ea76a6b43b0078.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Previews/Tap-000004-6e894e03899a445da7ea76a6b43b0078.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/HLS/Tap-000004-3492b270f88745d09782bd73f3d5fe80',2774,'Published','2026-11-05T08:47:50.3814865+00:00');
INSERT INTO episodes VALUES('0492264c6256405598495dc4dda7e883','95603483874d4db5ba1957bd2cadadc3','Chương 41-50',5,'r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Episodes/Tap-000005-326fff5ec9fe4c39ae156c62e046464c.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Previews/Tap-000005-326fff5ec9fe4c39ae156c62e046464c.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/HLS/Tap-000005-6ed8f26bb5b443ebbb1cc3f8f52626eb',2460,'Published','2026-11-05T09:09:23.1689128+00:00');
INSERT INTO episodes VALUES('120c5f9c52f04c73930c6d8af5efb5b4','95603483874d4db5ba1957bd2cadadc3','Chương 51-60',6,'r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Episodes/Tap-000006-a98d7386624149ecab35818d39432ac8.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Previews/Tap-000006-a98d7386624149ecab35818d39432ac8.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/HLS/Tap-000006-9f34b0d926d4425b9e566b60a7edce3c',3042,'Published','2026-11-05T09:18:39.0079453+00:00');
INSERT INTO episodes VALUES('8436303b08f9417da0008a6163bb14b5','95603483874d4db5ba1957bd2cadadc3','Chương 61-74',7,'r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Episodes/Tap-000007-139c3199b1ca44e6919b241af542daea.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/Previews/Tap-000007-139c3199b1ca44e6919b241af542daea.mp3','r2:stories/huyen-hoac--95603483874d4db5ba1957bd2cadadc3/HLS/Tap-000007-c6e0b04a90654012b1e772ed3f6f74d0',6469,'Published','2026-11-05T09:46:48.4443366+00:00');
INSERT INTO episodes VALUES('d68cadde27164fd68284b1445f34e87b','b68f184a75e9494d8cdd82114a9fee6c','Chương 01-09',1,'r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Episodes/Tap-000001-5fb260ea17ee485883c63dd406b18c25.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Previews/Tap-000001-5fb260ea17ee485883c63dd406b18c25.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/HLS/Tap-000001-b7061c1ccfef4185a2662156982a626b',4282,'Published','2026-11-06T09:57:52.6351376+00:00');
INSERT INTO episodes VALUES('8929f2bbda8a4de5a656385bef8e806a','de38c2eaa8fa492fbfde8c5a842ac969','Chương 1',1,'r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Episodes/Tap-000001-b27f67f090ea4d8a8a65c9f6228c8721.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Previews/Tap-000001-b27f67f090ea4d8a8a65c9f6228c8721.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/HLS/Tap-000001-7c161a118fa9467fa9619e1fc7a524f1',16353,'Published','2026-11-06T10:23:18.4624247+00:00');
INSERT INTO episodes VALUES('7da013a6db124a6da7375ca4340cb2f4','de38c2eaa8fa492fbfde8c5a842ac969','Chương 2',2,'r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Episodes/Tap-000002-77111ad429ea40b0aa70b62c1f65cc53.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Previews/Tap-000002-77111ad429ea40b0aa70b62c1f65cc53.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/HLS/Tap-000002-a09f9d15681e43358514998d31c9f9b6',16352,'Published','2026-11-07T00:23:10.4897479+00:00');
INSERT INTO episodes VALUES('325305c507584685af30fc03e358c59c','de38c2eaa8fa492fbfde8c5a842ac969','Chương 3',3,'r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Episodes/Tap-000003-903bd122e11a4af796d975ab40e8c54b.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Previews/Tap-000003-903bd122e11a4af796d975ab40e8c54b.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/HLS/Tap-000003-545ea44df4fa43e59b0cdbc9512baf6d',16352,'Published','2026-11-07T00:48:43.1210142+00:00');
INSERT INTO episodes VALUES('30ac5dd70a4b4048a1e50e9c3068f377','de38c2eaa8fa492fbfde8c5a842ac969','Chương 4',4,'r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Episodes/Tap-000004-f9dc34e2febf4751a4e56eb8448283e7.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Previews/Tap-000004-f9dc34e2febf4751a4e56eb8448283e7.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/HLS/Tap-000004-0fa96ace11814d22930dfbacd469dd62',16353,'Published','2026-11-07T01:12:41.8633659+00:00');
INSERT INTO episodes VALUES('fe8b30ce8312474290b92ba4b00e396c','de38c2eaa8fa492fbfde8c5a842ac969','Chương 5: end',5,'r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Episodes/Tap-000005-695ad77793134787b8d02769b6ebd893.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/Previews/Tap-000005-695ad77793134787b8d02769b6ebd893.mp3','r2:stories/xuyen-thanh-phao-hoi-an-bam-thanh-dai-gia--de38c2eaa8fa492fbfde8c5a842ac969/HLS/Tap-000005-0322b4b483eb4c738dabf834a322a7c5',16353,'Published','2026-11-07T01:45:39.2734269+00:00');
INSERT INTO episodes VALUES('6e28d0d2b6c9414697f68b17aaa63c37','b68f184a75e9494d8cdd82114a9fee6c','Chương 10-34',2,'r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Episodes/Tap-000002-a04c8fdbe5714a00a8bf221be69b7e2f.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Previews/Tap-000002-a04c8fdbe5714a00a8bf221be69b7e2f.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/HLS/Tap-000002-17412eb473964e83b593b152e49bebbf',12342,'Published','2026-11-10T00:21:12.4072190+00:00');
INSERT INTO episodes VALUES('ff31f30ee7b24a2cbfb3d97fa6b3b980','b68f184a75e9494d8cdd82114a9fee6c','Chương 35-50',3,'r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Episodes/Tap-000003-7845d3ce223a41748404caaad58e9706.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Previews/Tap-000003-7845d3ce223a41748404caaad58e9706.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/HLS/Tap-000003-c6a1a25e94b44976807f5d2c000e0b7b',9010,'Published','2026-11-10T09:40:33.7865092+00:00');
INSERT INTO episodes VALUES('5b8720d3b4364da1a099b098e2959209','b68f184a75e9494d8cdd82114a9fee6c','Chương 51-70 (part-1)',4,'r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Episodes/Tap-000004-f3b76323c4cc4fcfaf99e8dced31a7a2.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Previews/Tap-000004-f3b76323c4cc4fcfaf99e8dced31a7a2.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/HLS/Tap-000004-c5a986059aa24740851fb0b32d9f62dc',10065,'Published','2026-11-10T10:57:43.6611509+00:00');
INSERT INTO episodes VALUES('655b3ad1def543529c9f1dc9de65d591','b68f184a75e9494d8cdd82114a9fee6c','Chương 51-70 (part-2)',5,'r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Episodes/Tap-000005-53e0674f37984280856a65d1dab73978.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Previews/Tap-000005-53e0674f37984280856a65d1dab73978.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/HLS/Tap-000005-ab1f00cf6c57446790630e19f0027821',10305,'Published','2026-11-10T11:46:49.8960249+00:00');
INSERT INTO episodes VALUES('200b2da792a745b88aebd3b29d4602ab','b68f184a75e9494d8cdd82114a9fee6c','Chương 71-100 (part-1)',6,'r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Episodes/Tap-000006-2fbf4fae2a194bfd9456c9feba5d42cc.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Previews/Tap-000006-2fbf4fae2a194bfd9456c9feba5d42cc.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/HLS/Tap-000006-f2e7341a4b2c4868bfa8a67a50776a60',7200,'Published','2026-11-11T00:15:05.3792640+00:00');
INSERT INTO episodes VALUES('d8f1298107af4982beb08f9ac36d167f','b68f184a75e9494d8cdd82114a9fee6c','Chương 71-100 (part-2)',7,'r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Episodes/Tap-000007-376b3c24d91c4af08e271bced8b474d5.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/Previews/Tap-000007-376b3c24d91c4af08e271bced8b474d5.mp3','r2:stories/kieu-kieu-su-nuong--b68f184a75e9494d8cdd82114a9fee6c/HLS/Tap-000007-608b28fa6e8c44258cf4319538a47473',7632,'Published','2026-11-11T00:27:45.1346620+00:00');
CREATE TABLE subscription_plans (
 id TEXT PRIMARY KEY,
 code TEXT NOT NULL UNIQUE,
 name TEXT NOT NULL,
 price_vnd INTEGER NOT NULL CHECK(price_vnd>=0),
 duration_months INTEGER NOT NULL CHECK(duration_months>0),
 updated_at TEXT NOT NULL
);
INSERT INTO subscription_plans VALUES('premium','PREMIUM','Premium 1 tháng',100000,1,'2026-10-16T18:49:43.7788996+00:00');
CREATE TABLE bank_accounts (
 id INTEGER PRIMARY KEY CHECK(id=1),
 bank_bin TEXT NOT NULL DEFAULT '',
 bank_name TEXT NOT NULL DEFAULT '',
 account_number TEXT NOT NULL DEFAULT '',
 account_name TEXT NOT NULL DEFAULT '',
 updated_at TEXT NOT NULL
);
INSERT INTO bank_accounts VALUES(1,'','','','','2026-10-16T18:49:43.7788996+00:00');
CREATE TABLE bank_receivers (
 id TEXT PRIMARY KEY,
 label TEXT NOT NULL DEFAULT '',
 bank_bin TEXT NOT NULL,
 bank_name TEXT NOT NULL,
 account_number TEXT NOT NULL,
 account_name TEXT NOT NULL,
 is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0,1)),
 is_default INTEGER NOT NULL DEFAULT 0 CHECK(is_default IN (0,1)),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL,
 UNIQUE(bank_bin,account_number)
);
INSERT INTO bank_receivers VALUES('fe07fd85d7c243a0893bd79918352a9b','Lăng Kính Hư Không','970422','MBBank','4510100015555','LUU THIEN VAN',1,1,'2026-10-17T01:23:03.0101800+00:00','2026-10-17T01:23:03.0101800+00:00');
CREATE TABLE app_migrations (
 key TEXT PRIMARY KEY,
 applied_at TEXT NOT NULL
);
INSERT INTO app_migrations VALUES('receivers-v1.9.2','2026-10-16T18:49:43.7788996+00:00');
CREATE TABLE orders (
 id TEXT PRIMARY KEY,
 code TEXT NOT NULL UNIQUE,
 user_id TEXT NOT NULL REFERENCES users(id),
 plan_id TEXT NOT NULL REFERENCES subscription_plans(id),
 amount_vnd INTEGER NOT NULL,
 duration_months INTEGER NOT NULL,
 bank_bin TEXT NOT NULL,
 bank_name TEXT NOT NULL,
 account_number TEXT NOT NULL,
 account_name TEXT NOT NULL,
 status TEXT NOT NULL DEFAULT 'Pending' CHECK(status IN ('Pending','Reviewing','Paid','Rejected','Cancelled','Expired','Refunded')),
 created_at TEXT NOT NULL,
 expires_at TEXT NOT NULL,
 paid_at TEXT,
 reviewed_by TEXT,
 review_note TEXT NOT NULL DEFAULT '',
 order_type TEXT NOT NULL DEFAULT 'Purchase' CHECK(order_type IN ('Purchase','AdminGrant')),
 grant_reason TEXT NOT NULL DEFAULT '',
 premium_start_at TEXT,
 premium_end_at TEXT
, bank_receiver_id TEXT NOT NULL DEFAULT '');
INSERT INTO orders VALUES('ab86fbcdbcfe4941935df86775519ec5','LKHAD261017012424629EA0','a965eeea99aa4c7eab237ad289a2af3f','premium',0,12,'','','','','Paid','2026-10-17T01:24:24.8152534+00:00','2026-10-17T01:24:24.8152534+00:00',NULL,'e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Voucher cho user Youtube hội viên','AdminGrant','Voucher cho user Youtube hội viên','2026-10-17T01:24:24.8151757+00:00','2027-10-17T01:24:24.8151757+00:00','');
INSERT INTO orders VALUES('250f1e82eb384f29946c51ae3246b8e0','LKH261024163605633D6DE1','6c736976b6754717aaa008ccb9f12976','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Paid','2026-10-24T16:36:05.5732336+00:00','2026-10-25T16:36:05.5732357+00:00','2026-10-24T16:37:08.8544148+00:00','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','','Purchase','','2026-10-24T16:37:08.8569865+00:00','2026-11-24T16:37:08.8569865+00:00','fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('0add0888d8f7409eadae5c254757d247','LKHAD2610290724150DDEDD','d4a574a4b6af4d6785f443c27976e891','premium',0,1,'','','','','Paid','2026-10-29T07:24:15.8498378+00:00','2026-10-29T07:24:15.8498378+00:00',NULL,'e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Tặng các bạn Độ Kiếp Tháng 11','AdminGrant','Tặng các bạn Độ Kiếp Tháng 11','2026-10-29T07:24:15.8486892+00:00','2026-11-29T07:24:15.8486892+00:00','');
INSERT INTO orders VALUES('d71ab40d7d274993b29e319533b48f16','LKH26112009082558ED4151','ce145e6154f54d418c3cdae288d74804','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Expired','2026-11-20T09:08:25.6960501+00:00','2026-11-21T09:08:25.6960517+00:00',NULL,NULL,'','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('11857cd8715649cc9f56afdfc4aa5e83','LKH261120090828235C6BD7','ce145e6154f54d418c3cdae288d74804','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Expired','2026-11-20T09:08:28.3196531+00:00','2026-11-21T09:08:28.3196545+00:00',NULL,NULL,'','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('99b6a58ab658462497d3aeb4d15378f8','LKH2611200908296172898D','ce145e6154f54d418c3cdae288d74804','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Expired','2026-11-20T09:08:29.9948796+00:00','2026-11-21T09:08:29.9948808+00:00',NULL,NULL,'','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('b711d837610047928003e624ba576e0f','LKH2611255486D6','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Pending','2026-11-25T06:42:27.0499514+00:00','2026-11-26T06:42:27.0499536+00:00',NULL,NULL,'','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('8b1c17faccbf411499020b1e7f6a4c3b','LKH261125311ADB','b4c3a0b489264d9488066360b6d05e30','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Rejected','2026-11-25T06:45:15.2827393+00:00','2026-11-26T06:45:15.2827413+00:00',NULL,'e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Lỗi','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('13ddccc21f754b1e93c0818a7a98a720','LKH2611255FCF7D','b4c3a0b489264d9488066360b6d05e30','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Rejected','2026-11-25T06:45:40.4476626+00:00','2026-11-26T06:45:40.4476651+00:00',NULL,'e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Lỗi','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('16b936d6972e453793abc59149a419f2','LKH2611258EA318','b4c3a0b489264d9488066360b6d05e30','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Rejected','2026-11-25T06:45:53.4893026+00:00','2026-11-26T06:45:53.4893198+00:00',NULL,'e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Lỗi','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('5d1ad1eed8734a94866af20c0e245c1b','LKH2611252EE21F','b4c3a0b489264d9488066360b6d05e30','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Pending','2026-11-25T06:59:08.9406218+00:00','2026-11-26T06:59:08.9406246+00:00',NULL,NULL,'','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('a6983813131a482885df11d1b0babf7b','LKH261125613226','b4c3a0b489264d9488066360b6d05e30','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Pending','2026-11-25T07:06:25.7827080+00:00','2026-11-26T07:06:25.7827101+00:00',NULL,NULL,'','Purchase','',NULL,NULL,'fe07fd85d7c243a0893bd79918352a9b');
INSERT INTO orders VALUES('766299927fc94c1393b4d3e398a56442','LKH261125CB94FF','b4c3a0b489264d9488066360b6d05e30','premium',100000,1,'970422','MBBank','4510100015555','LUU THIEN VAN','Paid','2026-11-25T07:07:24.3557917+00:00','2026-11-26T07:07:24.3557953+00:00','2026-11-25T07:08:57.7223115+00:00','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Tự động kích hoạt qua SePay (MBBank · GD #102459341 · Ref: FT26329261606676)','Purchase','','2026-11-25T07:08:57.7254602+00:00','2026-12-25T07:08:57.7254602+00:00','fe07fd85d7c243a0893bd79918352a9b');
CREATE TABLE payments (
 id TEXT PRIMARY KEY,
 order_id TEXT NOT NULL UNIQUE REFERENCES orders(id),
 bank_reference TEXT NOT NULL UNIQUE,
 amount_vnd INTEGER NOT NULL,
 confirmed_by TEXT NOT NULL REFERENCES users(id),
 confirmed_at TEXT NOT NULL
);
INSERT INTO payments VALUES('dfa0767b3a6d4e4082972056b6c7b6cb','250f1e82eb384f29946c51ae3246b8e0','LKH261024163605633D6DE1',100000,'e6406994-d2b2-4651-bdbb-5cdf7a0e3202','2026-10-24T16:37:08.8547746+00:00');
INSERT INTO payments VALUES('c66877ea61e54b019825e125100386f2','766299927fc94c1393b4d3e398a56442','SEPAY-102459341-FT26329261606676',100000,'e6406994-d2b2-4651-bdbb-5cdf7a0e3202','2026-11-25T07:08:57.7223115+00:00');
CREATE TABLE subscriptions (
 id TEXT PRIMARY KEY,
 user_id TEXT NOT NULL REFERENCES users(id),
 starts_at TEXT NOT NULL,
 ends_at TEXT NOT NULL,
 source TEXT NOT NULL CHECK(source IN ('Order','Admin')),
 order_id TEXT UNIQUE REFERENCES orders(id),
 granted_by TEXT REFERENCES users(id),
 note TEXT NOT NULL DEFAULT '',
 created_at TEXT NOT NULL
);
INSERT INTO subscriptions VALUES('f80be3e80b824043b053d7e8c084f140','a965eeea99aa4c7eab237ad289a2af3f','2026-10-17T01:24:24.8151757+00:00','2027-10-17T01:24:24.8151757+00:00','Admin','ab86fbcdbcfe4941935df86775519ec5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Voucher cho user Youtube hội viên','2026-10-17T01:24:24.8152534+00:00');
INSERT INTO subscriptions VALUES('6b20e63d185a4857b6ea1f72718a7648','6c736976b6754717aaa008ccb9f12976','2026-10-24T16:37:08.8569865+00:00','2026-11-24T16:37:08.8569865+00:00','Order','250f1e82eb384f29946c51ae3246b8e0','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','','2026-10-24T16:37:08.8583069+00:00');
INSERT INTO subscriptions VALUES('8932d4b58b77476298ca20c6c0fa9065','d4a574a4b6af4d6785f443c27976e891','2026-10-29T07:24:15.8486892+00:00','2026-11-29T07:24:15.8486892+00:00','Admin','0add0888d8f7409eadae5c254757d247','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Tặng các bạn Độ Kiếp Tháng 11','2026-10-29T07:24:15.8498378+00:00');
INSERT INTO subscriptions VALUES('6b2dedb96fca467eb0d0b3e9098021cf','b4c3a0b489264d9488066360b6d05e30','2026-11-25T07:08:57.7254602+00:00','2026-12-25T07:08:57.7254602+00:00','Order','766299927fc94c1393b4d3e398a56442','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Tự động kích hoạt qua SePay (MBBank · GD #102459341 · Ref: FT26329261606676)','2026-11-25T07:08:57.7223115+00:00');
CREATE TABLE listening_progress (
 user_id TEXT NOT NULL REFERENCES users(id),
 episode_id TEXT NOT NULL REFERENCES episodes(id),
 seconds INTEGER NOT NULL DEFAULT 0,
 updated_at TEXT NOT NULL,
 PRIMARY KEY(user_id,episode_id)
);
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','a0c06ab17b2a4b9f946db3a5ebfb4037',29,'2026-10-16T20:27:28.3856189+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','399e00fb883647ca919016bf20b81da6',2162,'2026-10-20T15:55:12.4202711+00:00');
INSERT INTO listening_progress VALUES('a965eeea99aa4c7eab237ad289a2af3f','399e00fb883647ca919016bf20b81da6',2162,'2026-10-17T06:26:44.9163656+00:00');
INSERT INTO listening_progress VALUES('a965eeea99aa4c7eab237ad289a2af3f','069620a9629e4eceb8034d29c6b28449',1787,'2026-10-17T06:44:15.1624984+00:00');
INSERT INTO listening_progress VALUES('a965eeea99aa4c7eab237ad289a2af3f','c5300a06cdc1444eafaaba6feddca88a',2101,'2026-10-17T07:22:09.0604967+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','069620a9629e4eceb8034d29c6b28449',704,'2026-10-18T07:07:14.1346255+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','f824e5d1d88c41b8a0f3fd8f78765771',3922,'2026-11-06T02:54:48.7492848+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','c5300a06cdc1444eafaaba6feddca88a',2101,'2026-10-19T05:36:35.4528558+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','47e1644c1d824fd38212c10deb4c7ec2',225,'2026-11-06T02:54:24.7417498+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','121da1cbf2f64d38ade6cff885d666ef',1881,'2026-11-03T17:00:59.4052742+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','148afe30024247e3b95f96f0de2a181a',224,'2026-10-21T00:59:47.5537697+00:00');
INSERT INTO listening_progress VALUES('36ca78fc343c4da5aa65a2ad48b3a944','f345e6e3ea5a4e3f84f487cc1ad5170e',300,'2026-10-21T06:27:07.1080940+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','f345e6e3ea5a4e3f84f487cc1ad5170e',4536,'2026-10-21T15:30:14.9461444+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','f7031da17ba64a46b45f3aa79087b3f7',4395,'2026-10-21T18:39:33.0894587+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','a30e70eddb0d4ad29eb7518b6e38a4b5',4041,'2026-10-21T18:52:24.9060306+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','62541b841b194cff8a7c8dee5756ce7b',1697,'2026-10-21T18:55:59.3489536+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','75ed634d6e1d4916814064abd2445c96',3738,'2026-10-22T13:51:50.8430816+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','67651e41446e46b1a83cb7fb0369b623',4473,'2026-10-21T19:08:38.4389792+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','799b443d9f6e46828f33eba39384f566',3170,'2026-10-22T13:40:27.5033966+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','3729b1849e774977aec218dca452083d',2208,'2026-11-05T10:53:00.6018229+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','bea2531c71444dc887074ba96b117aef',20,'2026-11-07T07:19:35.2747885+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','a489d03ecddf426c83c5a9e4eafcbe8a',473,'2026-11-07T07:19:44.0868219+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','a32f8a0482b742aea338e894766b0da4',2528,'2026-11-07T07:30:07.7756904+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','0492264c6256405598495dc4dda7e883',726,'2026-11-06T01:58:02.3151864+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','120c5f9c52f04c73930c6d8af5efb5b4',2938,'2026-11-06T01:57:08.6305938+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','8436303b08f9417da0008a6163bb14b5',6403,'2026-11-05T14:26:17.8120005+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','d902c233a95d4e5db938b7edb45ef742',8,'2026-11-06T02:55:09.6541504+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','30ac5dd70a4b4048a1e50e9c3068f377',29,'2026-11-07T01:48:48.6483924+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','d68cadde27164fd68284b1445f34e87b',104,'2026-11-26T00:29:09.1388713+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','6e28d0d2b6c9414697f68b17aaa63c37',12012,'2026-11-10T11:30:24.4859761+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','ff31f30ee7b24a2cbfb3d97fa6b3b980',8712,'2026-11-10T14:34:12.0614891+00:00');
INSERT INTO listening_progress VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','5b8720d3b4364da1a099b098e2959209',5188,'2026-11-10T16:02:08.5299551+00:00');
CREATE TABLE audit_logs (
 id TEXT PRIMARY KEY,
 actor_id TEXT,
 action TEXT NOT NULL,
 subject_id TEXT,
 details TEXT NOT NULL,
 created_at TEXT NOT NULL
);
INSERT INTO audit_logs VALUES('1569c44028fe415ab975d4b26087974d','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','efffe4516265459ca5a0d8e56cf4f69b','Âm Dương Tạo Hóa Kinh','2026-10-16T19:40:39.2681440+00:00');
INSERT INTO audit_logs VALUES('e0b75dbbe53c4bdb9c846bb481538be6','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.cover_replace','efffe4516265459ca5a0d8e56cf4f69b','storage=R2; old=none','2026-10-16T19:40:56.0769247+00:00');
INSERT INTO audit_logs VALUES('73fb09c69ed846ada620bd53b6e11f67','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','a0c06ab17b2a4b9f946db3a5ebfb4037','story=efffe4516265459ca5a0d8e56cf4f69b; episode=7; storage=R2','2026-10-16T19:54:16.2077527+00:00');
INSERT INTO audit_logs VALUES('7bacb32fb46f4aeca21264d75c84ff6e','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','399e00fb883647ca919016bf20b81da6','story=efffe4516265459ca5a0d8e56cf4f69b; episode=13; storage=R2','2026-10-16T19:56:59.3874284+00:00');
INSERT INTO audit_logs VALUES('83c41655404a4f9a9f8bf9b53e989851','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','069620a9629e4eceb8034d29c6b28449','story=efffe4516265459ca5a0d8e56cf4f69b; episode=4; storage=R2','2026-10-16T19:59:18.6278824+00:00');
INSERT INTO audit_logs VALUES('e27b221e79a746e39dc765a974c29513','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','c5300a06cdc1444eafaaba6feddca88a','story=efffe4516265459ca5a0d8e56cf4f69b; episode=24; storage=R2','2026-10-16T20:05:02.2019983+00:00');
INSERT INTO audit_logs VALUES('c2c3c350c3f84ac9a282d5b0cec542d8','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','069620a9629e4eceb8034d29c6b28449','Chương 14-18: Âm Dương Tạo Hóa','2026-10-16T20:06:43.0509224+00:00');
INSERT INTO audit_logs VALUES('a3a9c60ec95a4818ab93408c65a5af90','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','a0c06ab17b2a4b9f946db3a5ebfb4037','Tập 1: Giới thiệu','2026-10-16T20:06:49.8311507+00:00');
INSERT INTO audit_logs VALUES('a94c3aa59fda480b85e7c8431676eddb','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','399e00fb883647ca919016bf20b81da6','Tập 2: Chinh phục Mẹ','2026-10-16T20:06:55.6661179+00:00');
INSERT INTO audit_logs VALUES('6a0f2b25a0d9429daee9d296c0164f84','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','c5300a06cdc1444eafaaba6feddca88a','Chương 19-24: Chinh phục','2026-10-16T20:07:01.2579438+00:00');
INSERT INTO audit_logs VALUES('441118fb4c154cf3ba4ac41ec06c9445','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','a0c06ab17b2a4b9f946db3a5ebfb4037','Chương 1-7: Giới thiệu','2026-10-16T20:07:16.0751377+00:00');
INSERT INTO audit_logs VALUES('dafdc3d1b8904598968d9ffaa8237dbe','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','399e00fb883647ca919016bf20b81da6','Chương 8-13: Cảm giác lạ','2026-10-16T20:07:48.1876891+00:00');
INSERT INTO audit_logs VALUES('4ebdba8e8df440049afd28155e850c0c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','bank.create','fe07fd85d7c243a0893bd79918352a9b','Bank BIN=970422; account=***5555','2026-10-17T01:23:03.0122648+00:00');
INSERT INTO audit_logs VALUES('56c5f53964a648bbb23014638b42e581','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','user.update','a965eeea99aa4c7eab237ad289a2af3f','status=Active','2026-10-17T01:23:56.8951958+00:00');
INSERT INTO audit_logs VALUES('34f84a25478a41bfad9fcfd0d41d0fd5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','premium.grant','a965eeea99aa4c7eab237ad289a2af3f','order=LKHAD261017012424629EA0; months=12; reason=Voucher cho user Youtube hội viên','2026-10-17T01:24:24.8156170+00:00');
INSERT INTO audit_logs VALUES('ebbbad1b0a154a47aec9c483b81ad307','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','28006d5e7067438d9adfd2c690be897d','Sư Nương Tuyệt Sắc Đuổi Tôi Đi, Tình Cờ Sống Chung Với Nữ Tổng Tài','2026-10-18T10:09:23.7342492+00:00');
INSERT INTO audit_logs VALUES('f854c18006c540008e0071d79067395b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.cover_replace','28006d5e7067438d9adfd2c690be897d','storage=R2; old=none','2026-10-18T10:10:24.3522294+00:00');
INSERT INTO audit_logs VALUES('0bb30646e8844912a419674ab3421511','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','f824e5d1d88c41b8a0f3fd8f78765771','story=28006d5e7067438d9adfd2c690be897d; episode=1; storage=R2','2026-10-18T10:15:14.0966225+00:00');
INSERT INTO audit_logs VALUES('cc2928f6e1ed45c3ac9198fe388f95b8','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','d6ee5625936e4ed6aaab818e7e1fb6da','story=28006d5e7067438d9adfd2c690be897d; episode=2; storage=R2','2026-10-20T15:33:12.7766045+00:00');
INSERT INTO audit_logs VALUES('cabe79b465aa41eaba56dde182c2ed60','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','dd9a6040fdcd4922b8651b2d1bf3c087','Mối Quan Hệ Bí Mật Giữa Những Người Trong Gia Đình','2026-10-20T16:36:10.6639033+00:00');
INSERT INTO audit_logs VALUES('c5012a4a54014865805c3cade7670109','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.cover_replace','dd9a6040fdcd4922b8651b2d1bf3c087','storage=R2; old=none','2026-10-20T16:36:22.1023136+00:00');
INSERT INTO audit_logs VALUES('ea868fd5507d42c0a9512627d948073d','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','47e1644c1d824fd38212c10deb4c7ec2','story=dd9a6040fdcd4922b8651b2d1bf3c087; episode=1; storage=R2','2026-10-20T16:59:39.3002100+00:00');
INSERT INTO audit_logs VALUES('60f5ad1005df45e2b0b23966d27497b2','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','121da1cbf2f64d38ade6cff885d666ef','story=dd9a6040fdcd4922b8651b2d1bf3c087; episode=2; storage=R2','2026-10-20T17:39:06.8213395+00:00');
INSERT INTO audit_logs VALUES('0fd1b3f02d40489899a66ad40d1ec39b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','148afe30024247e3b95f96f0de2a181a','story=dd9a6040fdcd4922b8651b2d1bf3c087; episode=3; storage=R2','2026-10-20T23:43:53.9886860+00:00');
INSERT INTO audit_logs VALUES('1581a3e5b55743c6a30d1d670f959e29','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','148afe30024247e3b95f96f0de2a181a','Chương 14-18: Chị gái','2026-10-21T00:21:52.8009871+00:00');
INSERT INTO audit_logs VALUES('0cdeab0bbc1445c9835b303a0eb25de2','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','148afe30024247e3b95f96f0de2a181a','Chương 14-18: Mẹ nuôi','2026-10-21T00:59:29.2586213+00:00');
INSERT INTO audit_logs VALUES('e2032462d66a461087dc456ddb37b201','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','93d4ff86b54848a0a4231fe9ef6a5520','Hạnh Phúc Một Gia Đình Thiếu Cha','2026-10-21T06:19:12.3988329+00:00');
INSERT INTO audit_logs VALUES('f83a58208e0941e19ad854039773a880','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.cover_replace','93d4ff86b54848a0a4231fe9ef6a5520','storage=R2; old=none','2026-10-21T06:19:37.2925103+00:00');
INSERT INTO audit_logs VALUES('feee8a68ebac42ecb1799a3dd8b0d8e3','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','f345e6e3ea5a4e3f84f487cc1ad5170e','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=1; storage=R2','2026-10-21T06:23:49.4749499+00:00');
INSERT INTO audit_logs VALUES('d120d8f16b5a48e3a78c8a8282ccb865','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','f7031da17ba64a46b45f3aa79087b3f7','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=2; storage=R2','2026-10-21T06:28:01.0416147+00:00');
INSERT INTO audit_logs VALUES('cb0e8f8a51da45d784e9cdb3a354473a','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','a30e70eddb0d4ad29eb7518b6e38a4b5','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=3; storage=R2','2026-10-21T06:39:59.1708480+00:00');
INSERT INTO audit_logs VALUES('a1edf727ce1f4cee8942bdfd243fc312','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','67651e41446e46b1a83cb7fb0369b623','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=4; storage=R2','2026-10-21T06:45:18.6394750+00:00');
INSERT INTO audit_logs VALUES('27bf1039a6e04d5a920fb94a00d3a2b9','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','75ed634d6e1d4916814064abd2445c96','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=5; storage=R2','2026-10-21T07:16:19.1313559+00:00');
INSERT INTO audit_logs VALUES('3ef5a55d625e444489806d7367803a83','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','d902c233a95d4e5db938b7edb45ef742','story=28006d5e7067438d9adfd2c690be897d; episode=3; storage=R2','2026-10-21T07:31:20.7196785+00:00');
INSERT INTO audit_logs VALUES('463d32c3fab24cefa4cbece5161d3608','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','799b443d9f6e46828f33eba39384f566','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=6; storage=R2','2026-10-21T07:36:02.2632512+00:00');
INSERT INTO audit_logs VALUES('8c5b1433ee514c2d9efdc88aff8a9e97','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','14c847ac12794f2d8ae01b7239902111','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=7; storage=R2','2026-10-21T08:24:05.9951737+00:00');
INSERT INTO audit_logs VALUES('d25d924c38b848509b9bf018f318dea5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','62541b841b194cff8a7c8dee5756ce7b','story=93d4ff86b54848a0a4231fe9ef6a5520; episode=8; storage=R2','2026-10-21T08:40:01.4843827+00:00');
INSERT INTO audit_logs VALUES('bd7324ba288a435b92f71608b5a8d3f6','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.update','62541b841b194cff8a7c8dee5756ce7b','Tập End: Hạnh Phúc Một Gia Đình Thiếu Cha','2026-10-21T15:50:01.4264217+00:00');
INSERT INTO audit_logs VALUES('753f0fe05e6a4a94ab19a3179af63549','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','3d127d2e55584332adb83cde14089251','story=dd9a6040fdcd4922b8651b2d1bf3c087; episode=4; storage=R2','2026-10-22T13:59:19.7390742+00:00');
INSERT INTO audit_logs VALUES('c7cc1da062cb45ffbe38d3cdf663fed7','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','order.confirm','250f1e82eb384f29946c51ae3246b8e0','bank_reference=LKH261024163605633D6DE1; amount=100000','2026-10-24T16:37:08.8587669+00:00');
INSERT INTO audit_logs VALUES('2474591087104de5876d1ca8ef3404d5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','user.update','68a743f0cd774650ad5da802b4ea20d7','status=Locked','2026-10-26T08:37:36.1786108+00:00');
INSERT INTO audit_logs VALUES('c82e6148458549c1a89d97981a93b31d','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','user.update','36ca78fc343c4da5aa65a2ad48b3a944','status=Locked','2026-10-26T08:37:46.9868605+00:00');
INSERT INTO audit_logs VALUES('49cf0910deb64f90a6c1a9b1bceb54b0','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','premium.grant','d4a574a4b6af4d6785f443c27976e891','order=LKHAD2610290724150DDEDD; months=1; reason=Tặng các bạn Độ Kiếp Tháng 11','2026-10-29T07:24:15.8510566+00:00');
INSERT INTO audit_logs VALUES('fd217d11b83b4e1ba598377026db3368','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','genre.create','duong-tinh-voi-nguoi-lon','Dương tính với người lớn','2026-11-04T08:42:00.5499338+00:00');
INSERT INTO audit_logs VALUES('3ce03f8e38ee40e58cbba01294cbd95b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.update','efffe4516265459ca5a0d8e56cf4f69b','Âm Dương Tạo Hóa Kinh','2026-11-04T08:42:47.7931306+00:00');
INSERT INTO audit_logs VALUES('a457e69aec8e43c2a89dddc43cf4e274','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.update','dd9a6040fdcd4922b8651b2d1bf3c087','Mối Quan Hệ Bí Mật Giữa Những Người Trong Gia Đình','2026-11-04T08:42:57.0029391+00:00');
INSERT INTO audit_logs VALUES('f0375569270e42718dfe53f7dbfff003','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.update','dd9a6040fdcd4922b8651b2d1bf3c087','Mối Quan Hệ Bí Mật Giữa Những Người Trong Gia Đình','2026-11-04T08:43:11.9779202+00:00');
INSERT INTO audit_logs VALUES('d1e642538ced400f84fc742a5e43591b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.update','93d4ff86b54848a0a4231fe9ef6a5520','Hạnh Phúc Một Gia Đình Thiếu Cha','2026-11-04T08:43:19.6336660+00:00');
INSERT INTO audit_logs VALUES('27816910967d461ebc7a484e423e77a8','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','e043e1833224430e98e7bcd07f0ead9a','story=efffe4516265459ca5a0d8e56cf4f69b; episode=5; storage=R2','2026-11-04T09:27:54.8882220+00:00');
INSERT INTO audit_logs VALUES('30ff6a14f7b44a2187147ff4fe09306f','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','95603483874d4db5ba1957bd2cadadc3','Huyễn hoặc','2026-11-05T06:17:58.8018597+00:00');
INSERT INTO audit_logs VALUES('58e15141f0ae48df916de70929950b57','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.cover_replace','95603483874d4db5ba1957bd2cadadc3','storage=R2; old=none','2026-11-05T06:18:14.2833579+00:00');
INSERT INTO audit_logs VALUES('e2724b7593d64763896b40b4bfdcb5be','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','3729b1849e774977aec218dca452083d','story=95603483874d4db5ba1957bd2cadadc3; episode=1; storage=R2','2026-11-05T06:21:04.9508456+00:00');
INSERT INTO audit_logs VALUES('c5bb29d146b940a28f6ab7b6a7f0fa78','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','bea2531c71444dc887074ba96b117aef','story=95603483874d4db5ba1957bd2cadadc3; episode=2; storage=R2','2026-11-05T07:50:59.9731168+00:00');
INSERT INTO audit_logs VALUES('7dd795d630f3422ca5a6fdf82e951cba','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','a489d03ecddf426c83c5a9e4eafcbe8a','story=95603483874d4db5ba1957bd2cadadc3; episode=3; storage=R2','2026-11-05T08:40:28.0367817+00:00');
INSERT INTO audit_logs VALUES('41d680ce138e447b8a01db25539843e2','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','a32f8a0482b742aea338e894766b0da4','story=95603483874d4db5ba1957bd2cadadc3; episode=4; storage=R2','2026-11-05T08:47:50.3966620+00:00');
INSERT INTO audit_logs VALUES('5a70d321184f4fbba0bb77041c221685','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','0492264c6256405598495dc4dda7e883','story=95603483874d4db5ba1957bd2cadadc3; episode=5; storage=R2','2026-11-05T09:09:23.1774060+00:00');
INSERT INTO audit_logs VALUES('25adcdeb164649f481f4fcd360a49a00','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.update','95603483874d4db5ba1957bd2cadadc3','Huyễn hoặc','2026-11-05T09:18:10.2783372+00:00');
INSERT INTO audit_logs VALUES('491c6f0a0b60455b8bdfcaca9cc66bae','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','120c5f9c52f04c73930c6d8af5efb5b4','story=95603483874d4db5ba1957bd2cadadc3; episode=6; storage=R2','2026-11-05T09:18:39.0136247+00:00');
INSERT INTO audit_logs VALUES('b6795bda96ee495a9279a6c21f74fe51','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','8436303b08f9417da0008a6163bb14b5','story=95603483874d4db5ba1957bd2cadadc3; episode=7; storage=R2','2026-11-05T09:46:48.4528174+00:00');
INSERT INTO audit_logs VALUES('d402b336660b481b84f7bc73b8bb6980','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.audio_replace','a32f8a0482b742aea338e894766b0da4','duration=2774; storage=R2; r2Verified=True; old queued','2026-11-06T02:30:13.2154647+00:00');
INSERT INTO audit_logs VALUES('7a688009e98b4b48966166923d4e49f5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','95603483874d4db5ba1957bd2cadadc3','count=1; files=huyenhoac01-10.txt','2026-11-06T02:48:52.6492216+00:00');
INSERT INTO audit_logs VALUES('e48ef47b960d4dccb5f44dc86a8aa76c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','95603483874d4db5ba1957bd2cadadc3','count=1; files=huyenhoac11-20.txt','2026-11-06T02:49:03.9128035+00:00');
INSERT INTO audit_logs VALUES('d2a146ea06104672b73faff63eef952a','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','95603483874d4db5ba1957bd2cadadc3','count=1; files=huyenhoac21-30.txt','2026-11-06T02:49:10.3932584+00:00');
INSERT INTO audit_logs VALUES('994e61bb177543149428bfa52c6b819e','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','95603483874d4db5ba1957bd2cadadc3','count=1; files=huyenhoac31-40.txt','2026-11-06T02:49:17.6255247+00:00');
INSERT INTO audit_logs VALUES('08eadb642f3d46e3915e3ce06abaf073','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','95603483874d4db5ba1957bd2cadadc3','count=1; files=huyenhoac41-50.txt','2026-11-06T02:49:23.9644827+00:00');
INSERT INTO audit_logs VALUES('d870c83078df4307afd3faa292aa73f6','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','95603483874d4db5ba1957bd2cadadc3','count=1; files=huyenhoac51-60.txt','2026-11-06T02:49:31.8326479+00:00');
INSERT INTO audit_logs VALUES('c09035e038784fb6b7f97815666dddc5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','95603483874d4db5ba1957bd2cadadc3','count=1; files=huyenhoac61-74.txt','2026-11-06T02:49:39.1755378+00:00');
INSERT INTO audit_logs VALUES('ed38f9fdf4a84c709ee4043201257fb9','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','93d4ff86b54848a0a4231fe9ef6a5520','count=1; files=gdll.txt','2026-11-06T02:51:19.2143616+00:00');
INSERT INTO audit_logs VALUES('224f62c564f44755b062c0ec338396b5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','93d4ff86b54848a0a4231fe9ef6a5520','textId=d78a7c05e50a4069a07863edd8a87fb8; file=hanh-phuc-mot-gia-dinh.txt; size=923761','2026-11-06T02:52:30.8010870+00:00');
INSERT INTO audit_logs VALUES('d3801778e56740268caefd299b640136','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','efffe4516265459ca5a0d8e56cf4f69b','count=1; files=amduongtaohoakinh0-7.txt','2026-11-06T02:53:24.3580380+00:00');
INSERT INTO audit_logs VALUES('3295ba0a139e4fba96e070778e072add','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','efffe4516265459ca5a0d8e56cf4f69b','count=1; files=amduongtaohoakinh8-13.txt','2026-11-06T02:53:37.2690383+00:00');
INSERT INTO audit_logs VALUES('e9cece77ebae4577bf79f09bcb44f124','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','efffe4516265459ca5a0d8e56cf4f69b','count=1; files=amduongtaohoakinh14-18.txt','2026-11-06T02:53:46.2882383+00:00');
INSERT INTO audit_logs VALUES('0decab44c8ca49b59b4d9023490012bd','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','efffe4516265459ca5a0d8e56cf4f69b','count=1; files=amduongtaohoakinh19-24.txt','2026-11-06T02:53:57.6771404+00:00');
INSERT INTO audit_logs VALUES('f1578a6a0ef948c1855a12c537fab12d','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','efffe4516265459ca5a0d8e56cf4f69b','count=1; files=amduongtaohoakinh25-35.txt','2026-11-06T02:54:06.2779344+00:00');
INSERT INTO audit_logs VALUES('9e0004e74b8444a59114dac1a398fa56','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.config','','rootFolder=LangKinhHuKhong-BanThao','2026-11-06T06:07:11.6855289+00:00');
INSERT INTO audit_logs VALUES('c2867d648df042379f72db2ba01fc4e4','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.config','','rootFolder=LangKinhHuKhong-BanThao','2026-11-06T06:07:15.3656780+00:00');
INSERT INTO audit_logs VALUES('2c5271ae2c054de0b385ade5ad7d55fb','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.config','','rootFolder=LangKinhHuKhong-BanThao','2026-11-06T06:09:34.6200371+00:00');
INSERT INTO audit_logs VALUES('3fd26ed2045241ceb39daa6cef60acce','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','884e4166dd7946c0bbbb3a84c60417d2','TÀN VƯƠNG BẠO SỦNG Y PHI NGÔNG CUỒNG','2026-11-06T08:24:31.4837014+00:00');
INSERT INTO audit_logs VALUES('da96ca89f75f4c1e8fadb72ff60ad68a','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.config','','rootFolder=LangKinhHuKhong-BanThao','2026-11-06T08:28:53.8491332+00:00');
INSERT INTO audit_logs VALUES('d032cb62ce764c30a2b094f0ff4a9d3f','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.config','','rootFolder=LangKinhHuKhong-BanThao','2026-11-06T08:29:35.3496928+00:00');
INSERT INTO audit_logs VALUES('f2c3454cc62f490aa3d64f5f3ddbbf4b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.config','','rootFolder=LangKinhHuKhong-BanThao','2026-11-06T08:41:54.4988337+00:00');
INSERT INTO audit_logs VALUES('fe1562f5ba1d43b68e491090d81e6965','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.connect','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Connected Google Drive OAuth2','2026-11-06T08:47:23.8569033+00:00');
INSERT INTO audit_logs VALUES('70dd72a250924fd0996ce2b07c4a5933','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong01-25.txt','2026-11-06T08:49:30.3134218+00:00');
INSERT INTO audit_logs VALUES('59a075202b884fc9bb9e7269f190fe32','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong26-50.txt','2026-11-06T08:49:58.3483285+00:00');
INSERT INTO audit_logs VALUES('1f36b6b12a3c4617b3a39be1c8bc06ef','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong51-75.txt','2026-11-06T08:50:31.7347000+00:00');
INSERT INTO audit_logs VALUES('551896540eed4c22be5fa26b12782f77','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong76-100.txt','2026-11-06T08:50:50.3504020+00:00');
INSERT INTO audit_logs VALUES('72fd933b03d244b78027f38485908f19','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong101-120.txt','2026-11-06T08:51:07.5284079+00:00');
INSERT INTO audit_logs VALUES('c47fae7fae864e898a3d3435e1a5c8c9','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong121-140.txt','2026-11-06T08:51:21.6180347+00:00');
INSERT INTO audit_logs VALUES('c94ad3cce05b4017850b719f2664a047','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong141-160.txt','2026-11-06T08:51:37.6059734+00:00');
INSERT INTO audit_logs VALUES('d4435577e9944d7e88e7771fbed0ed71','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong161-180.txt','2026-11-06T08:51:56.1070808+00:00');
INSERT INTO audit_logs VALUES('3ce6486b965f4095b7011fae7ef1de24','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong181-200.txt','2026-11-06T08:52:09.6279788+00:00');
INSERT INTO audit_logs VALUES('148bb679238747c897c84834b09e990c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.sync_push','','uploaded=13; stories=6','2026-11-06T08:52:57.8404742+00:00');
INSERT INTO audit_logs VALUES('000b95077ead4916909f5ce3ee2c67d6','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong201-300.txt','2026-11-06T08:54:41.6488799+00:00');
INSERT INTO audit_logs VALUES('62d9e4fab926404f9353c0ccfc138041','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong301-400.txt','2026-11-06T08:55:00.3886214+00:00');
INSERT INTO audit_logs VALUES('9f5078164a904211834576bbdb1359f4','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong401-500.txt','2026-11-06T08:55:13.7248222+00:00');
INSERT INTO audit_logs VALUES('425b90d240ec41ca9464affcddf769ef','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong501-600.txt','2026-11-06T08:55:28.6687106+00:00');
INSERT INTO audit_logs VALUES('a8bf73f9f48e46d09e45a778049655e9','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong601-700.txt','2026-11-06T08:55:40.3619588+00:00');
INSERT INTO audit_logs VALUES('140d6cb2f04f459da9db57ae7b09725e','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','b68f184a75e9494d8cdd82114a9fee6c','Kiều Kiều Sư Nương','2026-11-06T09:52:53.8060283+00:00');
INSERT INTO audit_logs VALUES('5abbf7fd1fda45beaf1dd0cf334e00e2','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','d68cadde27164fd68284b1445f34e87b','story=b68f184a75e9494d8cdd82114a9fee6c; episode=1; storage=R2; r2Verified=True','2026-11-06T09:57:52.6391075+00:00');
INSERT INTO audit_logs VALUES('3673c59ab3fc45cba00f931437f7913a','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.cover_replace','b68f184a75e9494d8cdd82114a9fee6c','storage=R2; old=none','2026-11-06T09:59:51.2246140+00:00');
INSERT INTO audit_logs VALUES('00ce445571a14fcaac2e91b73ea11440','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','de38c2eaa8fa492fbfde8c5a842ac969','Xuyên Thành Pháo Hôi Ăn Bám Thành Đại Gia','2026-11-06T10:01:15.1873470+00:00');
INSERT INTO audit_logs VALUES('23ed0f23f72f489187a0d78eb52d9c62','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.cover_replace','de38c2eaa8fa492fbfde8c5a842ac969','storage=R2; old=none','2026-11-06T10:01:32.4231611+00:00');
INSERT INTO audit_logs VALUES('980485350aee4948a00e773cc456faef','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','b68f184a75e9494d8cdd82114a9fee6c','target=both; count=1; files=chuong01-09.txt','2026-11-06T10:09:50.5067443+00:00');
INSERT INTO audit_logs VALUES('1fa2068f39184b6a9462f71f42e95822','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','8929f2bbda8a4de5a656385bef8e806a','story=de38c2eaa8fa492fbfde8c5a842ac969; episode=1; storage=R2; r2Verified=True','2026-11-06T10:23:18.4658115+00:00');
INSERT INTO audit_logs VALUES('af3d8183a8f64a0cadd3da0a88c8a35b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','7da013a6db124a6da7375ca4340cb2f4','story=de38c2eaa8fa492fbfde8c5a842ac969; episode=2; storage=R2; r2Verified=True','2026-11-07T00:23:10.4968245+00:00');
INSERT INTO audit_logs VALUES('1253d696fe374766aba247487937f517','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','325305c507584685af30fc03e358c59c','story=de38c2eaa8fa492fbfde8c5a842ac969; episode=3; storage=R2; r2Verified=True','2026-11-07T00:48:43.1297788+00:00');
INSERT INTO audit_logs VALUES('76190ee12b9743899b0233452fa37dce','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','30ac5dd70a4b4048a1e50e9c3068f377','story=de38c2eaa8fa492fbfde8c5a842ac969; episode=4; storage=R2; r2Verified=True','2026-11-07T01:12:41.8726586+00:00');
INSERT INTO audit_logs VALUES('81b0133298d04bcc8a030fd401547594','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','fe8b30ce8312474290b92ba4b00e396c','story=de38c2eaa8fa492fbfde8c5a842ac969; episode=5; storage=R2; r2Verified=True','2026-11-07T01:45:39.2905991+00:00');
INSERT INTO audit_logs VALUES('8724ec9310144dcb89da5b6c762f5f3b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','884e4166dd7946c0bbbb3a84c60417d2','textId=33084472ea34447883093d626d0bc352; file=chuong301-400.txt; size=762793; target=drive','2026-11-07T17:30:39.8851190+00:00');
INSERT INTO audit_logs VALUES('cb056dda8b954d96ac7fb4248b4aac56','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong701-800.txt','2026-11-07T17:35:47.3555826+00:00');
INSERT INTO audit_logs VALUES('458a16f469144166b743eaa1fdc7398d','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong801-900.txt','2026-11-07T17:36:15.5171085+00:00');
INSERT INTO audit_logs VALUES('494ac123b361409cabc5e43797046d07','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong901-1000.txt','2026-11-09T06:06:35.0783934+00:00');
INSERT INTO audit_logs VALUES('85b9db2eac8c40e2a51e007b84459c3c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','6e28d0d2b6c9414697f68b17aaa63c37','story=b68f184a75e9494d8cdd82114a9fee6c; episode=2; storage=R2; r2Verified=True','2026-11-10T00:21:12.4143650+00:00');
INSERT INTO audit_logs VALUES('0ed09629fd5d496caaeb2a6afefbb47c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_delete','b68f184a75e9494d8cdd82114a9fee6c','textId=994d1e97586345c48eb32d839857ef6b; file=chuong01-09.txt','2026-11-10T00:26:35.7658823+00:00');
INSERT INTO audit_logs VALUES('1a48d5113c784868beeaf06d74dc2c85','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','b68f184a75e9494d8cdd82114a9fee6c','target=drive; count=1; files=kieukieusunuong01-09.txt','2026-11-10T00:34:23.8955097+00:00');
INSERT INTO audit_logs VALUES('6c5ea31c02b34477b0fe0dde30f45b94','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','b68f184a75e9494d8cdd82114a9fee6c','target=drive; count=1; files=kieukieusunuong10-34.txt','2026-11-10T00:34:43.2165034+00:00');
INSERT INTO audit_logs VALUES('f979b724c3104dfda898b2ae29923064','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=01a758e54f8640488d20490f60e802c5; file=kieukieusunuong10-34.txt; size=361200; target=drive','2026-11-10T02:27:44.7022212+00:00');
INSERT INTO audit_logs VALUES('e1520bb9b5ea4b3e871b18c322a3c6e5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','b68f184a75e9494d8cdd82114a9fee6c','target=drive; count=1; files=kieukieusunuong35-50.txt','2026-11-10T02:33:28.4466029+00:00');
INSERT INTO audit_logs VALUES('063bbfa54aaa4c7c8e4f7595f020887a','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','b68f184a75e9494d8cdd82114a9fee6c','target=drive; count=1; files=kieukieusunuong51-70.txt','2026-11-10T02:39:35.3535979+00:00');
INSERT INTO audit_logs VALUES('399718e098a34da5bfd2bc2b130dd7f2','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','b68f184a75e9494d8cdd82114a9fee6c','target=drive; count=1; files=kieukieusunuong71-100.txt','2026-11-10T02:39:50.9926831+00:00');
INSERT INTO audit_logs VALUES('9928e8c9c0594d2b8db51cb530843372','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','b68f184a75e9494d8cdd82114a9fee6c','target=drive; count=1; files=Kiều Kiều Sư Nương_style.txt','2026-11-10T02:40:20.2292395+00:00');
INSERT INTO audit_logs VALUES('d8a5e030d6354abca68ebb1dde7f7aed','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=b5e03e5adcb543be908ff28213fb8cbd; file=kieukieusunuong51-70.txt; size=298865; target=drive','2026-11-10T02:43:55.9491785+00:00');
INSERT INTO audit_logs VALUES('2f7b62b79521460eba4b1d9a2010474c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=67dbf38c80444f67abfad53783382bc3; file=Kiều Kiều Sư Nương_style.txt; size=3915; target=drive','2026-11-10T03:09:43.4747323+00:00');
INSERT INTO audit_logs VALUES('a92d8d5ca90d4190a351b22cde9379cf','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=67dbf38c80444f67abfad53783382bc3; file=Kiều Kiều Sư Nương_style.txt; size=6752; target=drive','2026-11-10T03:13:40.4697680+00:00');
INSERT INTO audit_logs VALUES('bac90367718b496ab2cf30bf5deddbf1','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=67dbf38c80444f67abfad53783382bc3; file=Kiều Kiều Sư Nương_style.txt; size=6752; target=drive','2026-11-10T03:14:36.6116562+00:00');
INSERT INTO audit_logs VALUES('3ca34116d6b44709b9b9f2f452a3d800','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=67dbf38c80444f67abfad53783382bc3; file=Kiều Kiều Sư Nương_style.txt; size=12241; target=drive','2026-11-10T03:53:00.0716074+00:00');
INSERT INTO audit_logs VALUES('afcde4e0c628461c97b99c9dd780435e','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=67dbf38c80444f67abfad53783382bc3; file=Kiều Kiều Sư Nương_style.txt; size=12241; target=drive','2026-11-10T04:02:39.0537950+00:00');
INSERT INTO audit_logs VALUES('3f9e575d2c1740898d167a8d4c78f781','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.audio_replace','6e28d0d2b6c9414697f68b17aaa63c37','duration=12342; storage=R2; r2Verified=True; old queued','2026-11-10T06:43:14.1938373+00:00');
INSERT INTO audit_logs VALUES('06ea416592134f04a4582280f1df915d','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','7ba4eb40d6ee4acb872fc613e2d96ba6','Trùng sinh sủng mẹ SM','2026-11-10T07:19:49.1945118+00:00');
INSERT INTO audit_logs VALUES('c9fa3f9b461f48b48e1bd82cacabaef0','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_create','7ba4eb40d6ee4acb872fc613e2d96ba6','textId=56f872ff2ef64c9a88a348e8ec873269; file=ban-thao-20261110-0719.txt; size=0; target=drive','2026-11-10T07:20:23.0677165+00:00');
INSERT INTO audit_logs VALUES('299570257a4846f6ac20b9607f4daaef','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','7ba4eb40d6ee4acb872fc613e2d96ba6','textId=56f872ff2ef64c9a88a348e8ec873269; file=ban-thao-20261110-0719.txt; size=13152; target=drive','2026-11-10T07:20:46.7349324+00:00');
INSERT INTO audit_logs VALUES('c7454a1bb7b24f1695a052ce72678ab5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','b68f184a75e9494d8cdd82114a9fee6c','textId=67dbf38c80444f67abfad53783382bc3; file=Kiều Kiều Sư Nương_style.txt; size=580; target=drive','2026-11-10T07:21:10.7572266+00:00');
INSERT INTO audit_logs VALUES('a7707487b781479babf927b02edc1453','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','7ba4eb40d6ee4acb872fc613e2d96ba6','textId=56f872ff2ef64c9a88a348e8ec873269; file=ban-thao-20261110-0719.txt; size=13152; target=drive','2026-11-10T07:29:59.6518982+00:00');
INSERT INTO audit_logs VALUES('ee5263fc9ba04aa389e62b3aa2f7f8a5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_create','7ba4eb40d6ee4acb872fc613e2d96ba6','textId=1170adde604e49ce9384ad1afb3bfbb4; file=ban-thao-20261110-0730.txt; size=6125; target=drive','2026-11-10T07:30:57.9884639+00:00');
INSERT INTO audit_logs VALUES('48226dca72294be4aabfbdb99d80cfc5','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','7ba4eb40d6ee4acb872fc613e2d96ba6','textId=1170adde604e49ce9384ad1afb3bfbb4; file=ban-thao-20261110-0730.txt; size=11739; target=drive','2026-11-10T07:47:15.8813379+00:00');
INSERT INTO audit_logs VALUES('a9c1be14efa8465b9d1ec36cd366adc4','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_create','7ba4eb40d6ee4acb872fc613e2d96ba6','textId=19719f30b6da4f85bfd4d960195f5fb9; file=ban-thao-20261110-0747.txt; size=447; target=drive','2026-11-10T07:47:36.4381930+00:00');
INSERT INTO audit_logs VALUES('067a4247ef43401eb16627e38a7aa1a4','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','7ba4eb40d6ee4acb872fc613e2d96ba6','textId=56f872ff2ef64c9a88a348e8ec873269; file=ban-thao-20261110-0719.txt; size=26924; target=drive','2026-11-10T07:52:49.1478142+00:00');
INSERT INTO audit_logs VALUES('5850a3658f344eada0d7033013e75e68','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','ff31f30ee7b24a2cbfb3d97fa6b3b980','story=b68f184a75e9494d8cdd82114a9fee6c; episode=3; storage=R2; r2Verified=True','2026-11-10T09:40:33.7959404+00:00');
INSERT INTO audit_logs VALUES('1de66f15df58498598f18c2e2c9958d3','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','5b8720d3b4364da1a099b098e2959209','story=b68f184a75e9494d8cdd82114a9fee6c; episode=4; storage=R2; r2Verified=True','2026-11-10T10:57:43.6626255+00:00');
INSERT INTO audit_logs VALUES('df99d5301cdf46909059d67a3620fcf6','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','655b3ad1def543529c9f1dc9de65d591','story=b68f184a75e9494d8cdd82114a9fee6c; episode=5; storage=R2; r2Verified=True','2026-11-10T11:46:49.8970688+00:00');
INSERT INTO audit_logs VALUES('91d3cd434c1246ac854b185a63612e8c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','200b2da792a745b88aebd3b29d4602ab','story=b68f184a75e9494d8cdd82114a9fee6c; episode=6; storage=R2; r2Verified=True','2026-11-11T00:15:05.3892316+00:00');
INSERT INTO audit_logs VALUES('ed93ea311bd8404197e5bdab4367de24','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','episode.upload','d8f1298107af4982beb08f9ac36d167f','story=b68f184a75e9494d8cdd82114a9fee6c; episode=7; storage=R2; r2Verified=True','2026-11-11T00:27:45.1377378+00:00');
INSERT INTO audit_logs VALUES('c771240343a94c0c9d70a0d223b7378c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','order.reject','16b936d6972e453793abc59149a419f2','Lỗi','2026-11-25T06:49:58.9892580+00:00');
INSERT INTO audit_logs VALUES('fa60b81a46ea4a67bce85d6c295d7080','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','order.reject','13ddccc21f754b1e93c0818a7a98a720','Lỗi','2026-11-25T06:50:04.5697758+00:00');
INSERT INTO audit_logs VALUES('b1989e5d4ae8463fa40336805ce2660b','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','order.reject','8b1c17faccbf411499020b1e7f6a4c3b','Lỗi','2026-11-25T06:50:14.8364540+00:00');
INSERT INTO audit_logs VALUES('26b61e4039e746ada4b01c8c5d1668ba','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.disconnect','','Disconnected Google Drive','2026-11-25T06:55:28.4583608+00:00');
INSERT INTO audit_logs VALUES('5a7888bfc71b4946839effd9c0a58344','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','gdrive.connect','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','Connected Google Drive OAuth2','2026-11-25T06:56:13.6516535+00:00');
INSERT INTO audit_logs VALUES('3c3f13d23b154ed7abeef3a885d1dd1c','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong1001-1100.txt','2026-11-25T06:57:02.4200670+00:00');
INSERT INTO audit_logs VALUES('e0ef5ecccc8845f39eea38ee31e05b79','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong1101-1200.txt','2026-11-25T06:57:19.1758374+00:00');
INSERT INTO audit_logs VALUES('f0dacb6916ac41c38b90db53a9c4482e','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong1201-1300.txt','2026-11-25T06:57:39.7340723+00:00');
INSERT INTO audit_logs VALUES('d2ef3d0324b54786ad312cb83a2c7c3d','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','884e4166dd7946c0bbbb3a84c60417d2','target=drive; count=1; files=chuong1301-1543.txt','2026-11-25T06:57:58.4205068+00:00');
INSERT INTO audit_logs VALUES('ed7f30e3b76e4d20a83add209ba553fc','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','sepay.webhook_confirm','766299927fc94c1393b4d3e398a56442','order=LKH261125CB94FF; user=b4c3a0b489264d9488066360b6d05e30; amount=100000; bankRef=SEPAY-102459341-FT26329261606676; gateway=MBBank','2026-11-25T07:08:57.7272133+00:00');
INSERT INTO audit_logs VALUES('425e001d2f0f4e57a0adb259100f1546','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_update','884e4166dd7946c0bbbb3a84c60417d2','textId=cf4201b9e43f47db8c886aeb5e6aedeb; file=chuong1301-1543.txt; size=1129040; target=drive','2026-11-25T07:26:49.5222358+00:00');
INSERT INTO audit_logs VALUES('a8835a8d67b548a7bce7040ff39b580a','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.create','4b51262e153e4f379e024c7dd3eed9c4','Cấu hình Config','2026-11-25T07:35:13.7105569+00:00');
INSERT INTO audit_logs VALUES('7740433da86945aebd533e88f77c9211','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_upload','4b51262e153e4f379e024c7dd3eed9c4','target=drive; count=1; files=client_secret_google_drive.md','2026-11-25T07:37:50.9971475+00:00');
INSERT INTO audit_logs VALUES('e7231889761644f48a3f16fa77897587','e6406994-d2b2-4651-bdbb-5cdf7a0e3202','story.text_create','4b51262e153e4f379e024c7dd3eed9c4','textId=e0105274289f4333a433fd23924c0919; file=ban-thao-20261125-0750.md; size=287; target=drive','2026-11-25T07:51:01.1034823+00:00');
CREATE TABLE storage_cleanup (
 reference TEXT PRIMARY KEY,
 enqueued_at TEXT NOT NULL,
 attempts INTEGER NOT NULL DEFAULT 0,
 last_error TEXT NOT NULL DEFAULT ''
);
CREATE TABLE genres (
 slug TEXT PRIMARY KEY,
 name TEXT NOT NULL UNIQUE COLLATE NOCASE,
 is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0,1)),
 sort_order INTEGER NOT NULL DEFAULT 0
);
INSERT INTO genres VALUES('sac-hiep','Sắc Hiệp',1,0);
INSERT INTO genres VALUES('tien-hiep','Tiên Hiệp',1,1);
INSERT INTO genres VALUES('huyen-huyen','Huyền Huyễn',1,2);
INSERT INTO genres VALUES('kiem-hiep','Kiếm Hiệp',1,3);
INSERT INTO genres VALUES('xuyen-khong','Xuyên Không',1,4);
INSERT INTO genres VALUES('do-thi','Đô Thị',1,5);
INSERT INTO genres VALUES('dong-nhan','Đồng Nhân',1,6);
INSERT INTO genres VALUES('ngon-tinh','Ngôn Tình',1,7);
INSERT INTO genres VALUES('lich-su','Lịch Sử',1,8);
INSERT INTO genres VALUES('khoa-huyen','Khoa Huyễn',1,9);
INSERT INTO genres VALUES('trong-sinh','Trọng Sinh',1,10);
INSERT INTO genres VALUES('khac','Khác',1,11);
INSERT INTO genres VALUES('duong-tinh-voi-nguoi-lon','Dương tính với người lớn',1,100);
CREATE TABLE story_genres (
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 genre_slug TEXT NOT NULL REFERENCES genres(slug),
 PRIMARY KEY (story_id, genre_slug)
);
INSERT INTO story_genres VALUES('28006d5e7067438d9adfd2c690be897d','do-thi');
INSERT INTO story_genres VALUES('28006d5e7067438d9adfd2c690be897d','ngon-tinh');
INSERT INTO story_genres VALUES('28006d5e7067438d9adfd2c690be897d','khac');
INSERT INTO story_genres VALUES('efffe4516265459ca5a0d8e56cf4f69b','huyen-huyen');
INSERT INTO story_genres VALUES('efffe4516265459ca5a0d8e56cf4f69b','sac-hiep');
INSERT INTO story_genres VALUES('efffe4516265459ca5a0d8e56cf4f69b','tien-hiep');
INSERT INTO story_genres VALUES('efffe4516265459ca5a0d8e56cf4f69b','xuyen-khong');
INSERT INTO story_genres VALUES('efffe4516265459ca5a0d8e56cf4f69b','duong-tinh-voi-nguoi-lon');
INSERT INTO story_genres VALUES('dd9a6040fdcd4922b8651b2d1bf3c087','do-thi');
INSERT INTO story_genres VALUES('dd9a6040fdcd4922b8651b2d1bf3c087','duong-tinh-voi-nguoi-lon');
INSERT INTO story_genres VALUES('dd9a6040fdcd4922b8651b2d1bf3c087','khac');
INSERT INTO story_genres VALUES('dd9a6040fdcd4922b8651b2d1bf3c087','sac-hiep');
INSERT INTO story_genres VALUES('93d4ff86b54848a0a4231fe9ef6a5520','do-thi');
INSERT INTO story_genres VALUES('93d4ff86b54848a0a4231fe9ef6a5520','khac');
INSERT INTO story_genres VALUES('93d4ff86b54848a0a4231fe9ef6a5520','ngon-tinh');
INSERT INTO story_genres VALUES('93d4ff86b54848a0a4231fe9ef6a5520','sac-hiep');
INSERT INTO story_genres VALUES('93d4ff86b54848a0a4231fe9ef6a5520','duong-tinh-voi-nguoi-lon');
INSERT INTO story_genres VALUES('95603483874d4db5ba1957bd2cadadc3','do-thi');
INSERT INTO story_genres VALUES('95603483874d4db5ba1957bd2cadadc3','duong-tinh-voi-nguoi-lon');
INSERT INTO story_genres VALUES('95603483874d4db5ba1957bd2cadadc3','khac');
INSERT INTO story_genres VALUES('95603483874d4db5ba1957bd2cadadc3','ngon-tinh');
INSERT INTO story_genres VALUES('884e4166dd7946c0bbbb3a84c60417d2','xuyen-khong');
INSERT INTO story_genres VALUES('884e4166dd7946c0bbbb3a84c60417d2','ngon-tinh');
INSERT INTO story_genres VALUES('884e4166dd7946c0bbbb3a84c60417d2','trong-sinh');
INSERT INTO story_genres VALUES('b68f184a75e9494d8cdd82114a9fee6c','sac-hiep');
INSERT INTO story_genres VALUES('b68f184a75e9494d8cdd82114a9fee6c','tien-hiep');
INSERT INTO story_genres VALUES('b68f184a75e9494d8cdd82114a9fee6c','huyen-huyen');
INSERT INTO story_genres VALUES('b68f184a75e9494d8cdd82114a9fee6c','xuyen-khong');
INSERT INTO story_genres VALUES('b68f184a75e9494d8cdd82114a9fee6c','kiem-hiep');
INSERT INTO story_genres VALUES('b68f184a75e9494d8cdd82114a9fee6c','trong-sinh');
INSERT INTO story_genres VALUES('b68f184a75e9494d8cdd82114a9fee6c','duong-tinh-voi-nguoi-lon');
INSERT INTO story_genres VALUES('de38c2eaa8fa492fbfde8c5a842ac969','do-thi');
INSERT INTO story_genres VALUES('de38c2eaa8fa492fbfde8c5a842ac969','ngon-tinh');
INSERT INTO story_genres VALUES('de38c2eaa8fa492fbfde8c5a842ac969','trong-sinh');
INSERT INTO story_genres VALUES('7ba4eb40d6ee4acb872fc613e2d96ba6','xuyen-khong');
INSERT INTO story_genres VALUES('7ba4eb40d6ee4acb872fc613e2d96ba6','do-thi');
INSERT INTO story_genres VALUES('7ba4eb40d6ee4acb872fc613e2d96ba6','ngon-tinh');
INSERT INTO story_genres VALUES('7ba4eb40d6ee4acb872fc613e2d96ba6','duong-tinh-voi-nguoi-lon');
INSERT INTO story_genres VALUES('4b51262e153e4f379e024c7dd3eed9c4','khac');
CREATE TABLE story_reviews (
 id TEXT PRIMARY KEY,
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 user_id TEXT NOT NULL REFERENCES users(id),
 stars INTEGER NOT NULL CHECK(stars BETWEEN 1 AND 5),
 body TEXT NOT NULL DEFAULT '',
 status TEXT NOT NULL DEFAULT 'Visible' CHECK(status IN ('Visible','Hidden')),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL,
 UNIQUE(story_id, user_id)
);
CREATE TABLE story_listeners (
 user_id TEXT NOT NULL REFERENCES users(id),
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 listened_seconds INTEGER NOT NULL DEFAULT 0,
 qualified_at TEXT,
 updated_at TEXT NOT NULL,
 PRIMARY KEY(user_id,story_id)
);
INSERT INTO story_listeners VALUES('a965eeea99aa4c7eab237ad289a2af3f','efffe4516265459ca5a0d8e56cf4f69b',4732,'2026-10-17T05:13:53.0115030+00:00','2026-10-17T07:22:02.0582850+00:00');
CREATE TABLE playback_sessions (
 user_id TEXT PRIMARY KEY REFERENCES users(id),
 session_id TEXT NOT NULL,
 episode_id TEXT NOT NULL REFERENCES episodes(id) ON DELETE CASCADE,
 last_tick INTEGER NOT NULL
);
INSERT INTO playback_sessions VALUES('a965eeea99aa4c7eab237ad289a2af3f','c9bf21bbf65d44fda616a5fa46b35d01','f345e6e3ea5a4e3f84f487cc1ad5170e',1792563882);
INSERT INTO playback_sessions VALUES('36ca78fc343c4da5aa65a2ad48b3a944','af8a99cfd42149c38a68e67a4c2b241f','f345e6e3ea5a4e3f84f487cc1ad5170e',1792564026);
CREATE TABLE story_text_files (
 id TEXT PRIMARY KEY,
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 title TEXT NOT NULL DEFAULT '',
 file_name TEXT NOT NULL,
 stored_name TEXT NOT NULL,
 extension TEXT NOT NULL CHECK(extension IN ('.md','.txt')),
 size_bytes INTEGER NOT NULL DEFAULT 0,
 created_at TEXT NOT NULL
, updated_at TEXT NOT NULL DEFAULT '', drive_file_id TEXT NOT NULL DEFAULT '', storage_target TEXT NOT NULL DEFAULT 'vps');
INSERT INTO story_text_files VALUES('593a272c0a1c44aa9b3c9d9c50bdac50','95603483874d4db5ba1957bd2cadadc3','1','huyenhoac01-10.txt','593a272c0a1c44aa9b3c9d9c50bdac50.txt','.txt',50579,'2026-11-06T02:48:52.6441962+00:00','2026-11-06T02:48:52.6441962+00:00','1BiN_kp9jcPNmCl8VnuoOBnpCKBCNqvpV','vps');
INSERT INTO story_text_files VALUES('6f615f09f2284da4aad184ec3b10b2ee','95603483874d4db5ba1957bd2cadadc3','2','huyenhoac11-20.txt','6f615f09f2284da4aad184ec3b10b2ee.txt','.txt',47559,'2026-11-06T02:49:03.9092621+00:00','2026-11-06T02:49:03.9092621+00:00','1CNnlzxmtsDT0LCYuoMZHbHKBBJTCWeVU','vps');
INSERT INTO story_text_files VALUES('7759623c8ed14611a3e559aed41595b7','95603483874d4db5ba1957bd2cadadc3','3','huyenhoac21-30.txt','7759623c8ed14611a3e559aed41595b7.txt','.txt',47386,'2026-11-06T02:49:10.3906637+00:00','2026-11-06T02:49:10.3906637+00:00','1HMFJYOJmMjg-88eIbRvl0wQ4Ww_CNZSp','vps');
INSERT INTO story_text_files VALUES('39d552ea969343979e54f3c1b28cca9c','95603483874d4db5ba1957bd2cadadc3','4','huyenhoac31-40.txt','39d552ea969343979e54f3c1b28cca9c.txt','.txt',66381,'2026-11-06T02:49:17.6214040+00:00','2026-11-06T02:49:17.6214040+00:00','1c-ELkruPjHBCqVPD_bXv3SCmp7E0zb8S','vps');
INSERT INTO story_text_files VALUES('154ebda52fea4766a829569bf19d9b3f','95603483874d4db5ba1957bd2cadadc3','5','huyenhoac41-50.txt','154ebda52fea4766a829569bf19d9b3f.txt','.txt',58714,'2026-11-06T02:49:23.9628145+00:00','2026-11-06T02:49:23.9628145+00:00','1jNkIx-5gtVbQ9T3mpWiQAbNljMsK8K2h','vps');
INSERT INTO story_text_files VALUES('13be612495f0412092abf957ca7222c4','95603483874d4db5ba1957bd2cadadc3','6','huyenhoac51-60.txt','13be612495f0412092abf957ca7222c4.txt','.txt',72756,'2026-11-06T02:49:31.8308990+00:00','2026-11-06T02:49:31.8308990+00:00','1L_q3BNVV8sFUZGHTHnNtOMVH6ZuddcnC','vps');
INSERT INTO story_text_files VALUES('8c76de1c3eaf46eb97a39662c8bd26c7','95603483874d4db5ba1957bd2cadadc3','7','huyenhoac61-74.txt','8c76de1c3eaf46eb97a39662c8bd26c7.txt','.txt',155458,'2026-11-06T02:49:39.1736329+00:00','2026-11-06T02:49:39.1736329+00:00','1WeEpvYAppBE7Z_5cyjTie4nqIafdyD6z','vps');
INSERT INTO story_text_files VALUES('d78a7c05e50a4069a07863edd8a87fb8','93d4ff86b54848a0a4231fe9ef6a5520','full','hanh-phuc-mot-gia-dinh.txt','d78a7c05e50a4069a07863edd8a87fb8.txt','.txt',923761,'2026-11-06T02:51:19.2122703+00:00','2026-11-06T02:52:30.7991466+00:00','1VI-n3Z9F84dBZ1-T2k1cVyuFCygIAunZ','vps');
INSERT INTO story_text_files VALUES('4b27bd9b716d4c15842a5cce9c3785be','efffe4516265459ca5a0d8e56cf4f69b','Chương 0-7','amduongtaohoakinh0-7.txt','4b27bd9b716d4c15842a5cce9c3785be.txt','.txt',65589,'2026-11-06T02:53:24.3458392+00:00','2026-11-06T02:53:24.3458392+00:00','1bo8d8d_d_D6ffGCmYZQNIh4vATCnUr30','vps');
INSERT INTO story_text_files VALUES('05d3b6874cb3496787ad68a5f367639b','efffe4516265459ca5a0d8e56cf4f69b','Chương 08-13','amduongtaohoakinh8-13.txt','05d3b6874cb3496787ad68a5f367639b.txt','.txt',55162,'2026-11-06T02:53:37.2651244+00:00','2026-11-06T02:53:37.2651244+00:00','1rUSoI1uLsuIRWOO2RhLNd5yljJKP2wD0','vps');
INSERT INTO story_text_files VALUES('830cdb2e7d6f426f9b63d18e92c25c70','efffe4516265459ca5a0d8e56cf4f69b','Chương 14-18','amduongtaohoakinh14-18.txt','830cdb2e7d6f426f9b63d18e92c25c70.txt','.txt',45046,'2026-11-06T02:53:46.2778990+00:00','2026-11-06T02:53:46.2778990+00:00','1_ZyR3veuphQuoyMU2SVVSqg4LgR152r0','vps');
INSERT INTO story_text_files VALUES('2dad0290c5d34485ab7eb33a2c0f4be8','efffe4516265459ca5a0d8e56cf4f69b','Chương 19-24','amduongtaohoakinh19-24.txt','2dad0290c5d34485ab7eb33a2c0f4be8.txt','.txt',53254,'2026-11-06T02:53:57.6744289+00:00','2026-11-06T02:53:57.6744289+00:00','1nFIPzxO4viIUPAu7-jgYlhvNTst4p1pE','vps');
INSERT INTO story_text_files VALUES('701c35c592d740cfac34d468104e3174','efffe4516265459ca5a0d8e56cf4f69b','Chương 25-35','amduongtaohoakinh25-35.txt','701c35c592d740cfac34d468104e3174.txt','.txt',97625,'2026-11-06T02:54:06.2740250+00:00','2026-11-06T02:54:06.2740250+00:00','1dL2l2KeMmkwerV0Gp0tNmazkciUX58Lc','vps');
INSERT INTO story_text_files VALUES('cf9df196f72841be8aedafd9182e2b6f','884e4166dd7946c0bbbb3a84c60417d2','Chương 01-25','chuong01-25.txt','','.txt',194930,'2026-11-06T08:49:28.1253121+00:00','2026-11-06T08:49:28.1253121+00:00','1697el8k25rGXINGMdN1FK2dGjgGiXFnx','drive');
INSERT INTO story_text_files VALUES('7afaf0c8a52d49c39b5bcbb283381237','884e4166dd7946c0bbbb3a84c60417d2','Chương 26-50','chuong26-50.txt','','.txt',209808,'2026-11-06T08:49:56.6130183+00:00','2026-11-06T08:49:56.6130183+00:00','1APuWvPS_UNQPDDUYJihrldrwlQJnAQcm','drive');
INSERT INTO story_text_files VALUES('919873714af744d6846328e409df5dd7','884e4166dd7946c0bbbb3a84c60417d2','Chương 51-75','chuong51-75.txt','','.txt',181835,'2026-11-06T08:50:29.9287000+00:00','2026-11-06T08:50:29.9287000+00:00','1WuH9IV7smCkCLFVqjQRzZBP8u7pEzZqC','drive');
INSERT INTO story_text_files VALUES('8d78df34f63b4ef1a5cf8493bcb302ca','884e4166dd7946c0bbbb3a84c60417d2','Chương 76-100','chuong76-100.txt','','.txt',206808,'2026-11-06T08:50:48.3898571+00:00','2026-11-06T08:50:48.3898571+00:00','1lu6aOu8PganN9IWUx6LOQs_dyYD3rMc2','drive');
INSERT INTO story_text_files VALUES('e92ed3a8b25447ebb8ca7e42cb0e8762','884e4166dd7946c0bbbb3a84c60417d2','Chương 101-120','chuong101-120.txt','','.txt',171665,'2026-11-06T08:51:05.8094668+00:00','2026-11-06T08:51:05.8094668+00:00','1h3E9YGheYxEGduQ2FXp4wcSNdLTmkW3G','drive');
INSERT INTO story_text_files VALUES('37319ced10c1409c8a7f88fcfd77891b','884e4166dd7946c0bbbb3a84c60417d2','Chương 121-140','chuong121-140.txt','','.txt',168647,'2026-11-06T08:51:19.6491888+00:00','2026-11-06T08:51:19.6491888+00:00','12y78xbQRYkpDlfDC1IdSG7ltRjVqsQmv','drive');
INSERT INTO story_text_files VALUES('0dbdf217b9ed4312839f5e2956c98a7b','884e4166dd7946c0bbbb3a84c60417d2','Chương 141-160','chuong141-160.txt','','.txt',163195,'2026-11-06T08:51:36.0582384+00:00','2026-11-06T08:51:36.0582384+00:00','1TaWOETMxz6v-NwuHTizTp793KWFiqQuL','drive');
INSERT INTO story_text_files VALUES('61b53c5f91834598964e5ac9e0700247','884e4166dd7946c0bbbb3a84c60417d2','Chương 161-180','chuong161-180.txt','','.txt',177708,'2026-11-06T08:51:54.2336095+00:00','2026-11-06T08:51:54.2336095+00:00','1H3Bhn5rKhl6uUngTlEmKQzc3qKPCioZy','drive');
INSERT INTO story_text_files VALUES('5e71cb6b2d5b4422b2befdf00270d057','884e4166dd7946c0bbbb3a84c60417d2','Chương 181-200','chuong181-200.txt','','.txt',154577,'2026-11-06T08:52:08.1075460+00:00','2026-11-06T08:52:08.1075460+00:00','1CSI7aFPInNkANq_Zyk1yRKucVqqmuvjV','drive');
INSERT INTO story_text_files VALUES('bebba7d49a5e42858832198d30c980f7','884e4166dd7946c0bbbb3a84c60417d2','Chương 201-300','chuong201-300.txt','','.txt',862314,'2026-11-06T08:54:39.7719577+00:00','2026-11-06T08:54:39.7719577+00:00','18duSVCZHYg3ADjHjkXyk1mlNgWxPoFMU','drive');
INSERT INTO story_text_files VALUES('33084472ea34447883093d626d0bc352','884e4166dd7946c0bbbb3a84c60417d2','Chương 301-400','chuong301-400.txt','','.txt',762793,'2026-11-06T08:54:58.4390256+00:00','2026-11-07T17:30:38.191Z','1g7-AX6qRjNnggbn3fsYhGHaCXuCwRk43','drive');
INSERT INTO story_text_files VALUES('dec630a7533e43729e775a22b0941712','884e4166dd7946c0bbbb3a84c60417d2','Chương 401-500','chuong401-500.txt','','.txt',921860,'2026-11-06T08:55:11.8253099+00:00','2026-11-06T08:55:11.8253099+00:00','1SXj0UfVxl4t5ydSXRgsBOw8hA9dMjm3Y','drive');
INSERT INTO story_text_files VALUES('0e1199eedee0494f9cdfc80ff710a87c','884e4166dd7946c0bbbb3a84c60417d2','Chương 501-600','chuong501-600.txt','','.txt',889061,'2026-11-06T08:55:27.0922771+00:00','2026-11-06T08:55:27.0922771+00:00','1aFrusM1ftYNQzWf7BjJcCJp8a7LberTx','drive');
INSERT INTO story_text_files VALUES('ff5a6e44dcb44e9da88054159afdea5f','884e4166dd7946c0bbbb3a84c60417d2','Chương 601-700','chuong601-700.txt','','.txt',894417,'2026-11-06T08:55:38.7044544+00:00','2026-11-06T08:55:38.7044544+00:00','1fAQUAq1eh-64lhVW3Lr_LKD7oRwUAlTM','drive');
INSERT INTO story_text_files VALUES('d2ab59df9a734a7a897612dff4116cdd','884e4166dd7946c0bbbb3a84c60417d2','Chương 701-800','chuong701-800.txt','','.txt',871193,'2026-11-07T17:35:46.699Z','2026-11-07T17:35:46.699Z','1I1qO6fNLDKNJy0ln8KMOA8lnBepCyAsA','drive');
INSERT INTO story_text_files VALUES('ea7e1b41e4cd4f7f9856845936bed8f5','884e4166dd7946c0bbbb3a84c60417d2','Chương 801-900','chuong801-900.txt','','.txt',854488,'2026-11-07T17:36:14.795Z','2026-11-07T17:36:14.795Z','1jiAKUBI4AcDOrHdJS9vVHmtGUETLMMyJ','drive');
INSERT INTO story_text_files VALUES('40b0107bd008411fb68ac3e7e98ee0e3','884e4166dd7946c0bbbb3a84c60417d2','Chương 901-1000','chuong901-1000.txt','','.txt',836174,'2026-11-09T06:06:33.954Z','2026-11-09T06:06:33.954Z','1vMQ93Al5QBaZpKYVZDWrV7Ht02hycKGP','drive');
INSERT INTO story_text_files VALUES('4005d13112bf457284ba2ad7848cb3ef','b68f184a75e9494d8cdd82114a9fee6c','kieukieusunuong01-09','kieukieusunuong01-09.txt','','.txt',121056,'2026-11-10T00:34:23.442Z','2026-11-10T00:34:23.442Z','1XfZnmnM0H4ByrL9xBgE8nujBa_-MmcAh','drive');
INSERT INTO story_text_files VALUES('01a758e54f8640488d20490f60e802c5','b68f184a75e9494d8cdd82114a9fee6c','kieukieusunuong10-34','kieukieusunuong10-34.txt','','.txt',361200,'2026-11-10T00:34:42.718Z','2026-11-10T02:27:43.718Z','125WdfE2a6RiAkXujfOOjGYRESC6hUByO','drive');
INSERT INTO story_text_files VALUES('31f218b311d04b4296b6286f66ea7e51','b68f184a75e9494d8cdd82114a9fee6c','kieukieusunuong35-50','kieukieusunuong35-50.txt','','.txt',266633,'2026-11-10T02:33:27.955Z','2026-11-10T02:33:27.955Z','10t0Ns3-DqenIgSr554sHim_PyzmN5Azk','drive');
INSERT INTO story_text_files VALUES('b5e03e5adcb543be908ff28213fb8cbd','b68f184a75e9494d8cdd82114a9fee6c','kieukieusunuong51-70','kieukieusunuong51-70.txt','','.txt',298865,'2026-11-10T02:39:34.660Z','2026-11-10T02:43:54.863Z','17sPt6abrF9Ka3mpG4UgjvSElFgT8pv6_','drive');
INSERT INTO story_text_files VALUES('2286994e718d487096b793ffb10dab66','b68f184a75e9494d8cdd82114a9fee6c','kieukieusunuong71-100','kieukieusunuong71-100.txt','','.txt',441033,'2026-11-10T02:39:50.525Z','2026-11-10T02:39:50.525Z','15rTuRojHzMpsTC4J6CWeFd96m0fPDfl0','drive');
INSERT INTO story_text_files VALUES('67dbf38c80444f67abfad53783382bc3','b68f184a75e9494d8cdd82114a9fee6c','Kiều Kiều Sư Nương style VN','Kiều Kiều Sư Nương_style.txt','','.txt',580,'2026-11-10T02:40:19.586Z','2026-11-10T07:21:09.996Z','1a2LYVdqszZAUUooRWbF7PFcPhgv2zRmn','drive');
INSERT INTO story_text_files VALUES('56f872ff2ef64c9a88a348e8ec873269','7ba4eb40d6ee4acb872fc613e2d96ba6','DNA truyện','ban-thao-20261110-0719.txt','','.txt',26924,'2026-11-10T07:20:22.549Z','2026-11-10T07:52:48.600Z','1oq3oMdF5-OTf7FTrvxZ0qZO7lJrCDodJ','drive');
INSERT INTO story_text_files VALUES('1170adde604e49ce9384ad1afb3bfbb4','7ba4eb40d6ee4acb872fc613e2d96ba6','01: Spam ms','ban-thao-20261110-0730.txt','','.txt',11739,'2026-11-10T07:30:57.271Z','2026-11-10T07:47:15.320Z','1vv3vM4bbG07lHvkcDS9VLqyf5_JXnK0j','drive');
INSERT INTO story_text_files VALUES('19719f30b6da4f85bfd4d960195f5fb9','7ba4eb40d6ee4acb872fc613e2d96ba6','Chương 2: học hành và thay đổi','ban-thao-20261110-0747.txt','','.txt',447,'2026-11-10T07:47:35.965Z','2026-11-10T07:47:35.965Z','1D_t0hkqQ6EwP3O5AgVvH_ukPjBbqr7eF','drive');
INSERT INTO story_text_files VALUES('960cdac95bd74ff79a5a395de97c1792','884e4166dd7946c0bbbb3a84c60417d2','Chương 1001-1100','chuong1001-1100.txt','','.txt',791321,'2026-11-25T06:57:01.620Z','2026-11-25T06:57:01.620Z','1T6g-jSkJztIQa_ME7azG8WGpwwH2qpi9','drive');
INSERT INTO story_text_files VALUES('0e717ab6d2194ddc8b6134feb8954cec','884e4166dd7946c0bbbb3a84c60417d2','Chương 1101-1200','chuong1101-1200.txt','','.txt',635135,'2026-11-25T06:57:18.462Z','2026-11-25T06:57:18.462Z','1eew_4Rpp6RnbZ3Rbz8JKjPqmD_KxClEe','drive');
INSERT INTO story_text_files VALUES('5079d1f85cb949cfad5e5b6d675e796e','884e4166dd7946c0bbbb3a84c60417d2','Chương 1201-1300','chuong1201-1300.txt','','.txt',637736,'2026-11-25T06:57:38.944Z','2026-11-25T06:57:38.944Z','1fk6hganrxBUB2X3WSfFlUV8aiZjW-uU8','drive');
INSERT INTO story_text_files VALUES('cf4201b9e43f47db8c886aeb5e6aedeb','884e4166dd7946c0bbbb3a84c60417d2','Chương 1301-1543 (End)','chuong1301-1543.txt','','.txt',1129040,'2026-11-25T06:57:57.779Z','2026-11-25T07:26:47.721Z','1dpAdXfRtGXwNtNPu1fx2mw9zDnH9cPdZ','drive');
INSERT INTO story_text_files VALUES('8516c5e6d0ac44baa7aa565c6c84264e','4b51262e153e4f379e024c7dd3eed9c4','Google: clip2down.store@gmail.com - Đổi thành file json để sử dụng','client_secret_google_drive.md','','.md',578,'2026-11-25T07:37:50.514Z','2026-11-25T07:37:50.514Z','1lIzqFcBCZ_zJQ_95AvY5hC0UlIWyglz3','drive');
INSERT INTO story_text_files VALUES('e0105274289f4333a433fd23924c0919','4b51262e153e4f379e024c7dd3eed9c4','SePay','ban-thao-20261125-0750.md','','.md',287,'2026-11-25T07:51:00.342Z','2026-11-25T07:51:00.342Z','1O50L926cyXAx3mT8il1e9Dq8q9FdOccO','drive');
CREATE TABLE user_story_views (
 user_id TEXT NOT NULL REFERENCES users(id),
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 viewed_at TEXT NOT NULL,
 PRIMARY KEY(user_id,story_id)
);
INSERT INTO user_story_views VALUES('e6406994-d2b2-4651-bdbb-5cdf7a0e3202','b68f184a75e9494d8cdd82114a9fee6c','2026-11-26T00:27:18.8693321+00:00');
CREATE TABLE sepay_webhook_logs (
 id TEXT PRIMARY KEY,
 sepay_tx_id TEXT NOT NULL DEFAULT '',
 gateway TEXT NOT NULL DEFAULT '',
 transaction_date TEXT NOT NULL DEFAULT '',
 account_number TEXT NOT NULL DEFAULT '',
 transfer_type TEXT NOT NULL DEFAULT '',
 transfer_amount INTEGER NOT NULL DEFAULT 0,
 reference_code TEXT NOT NULL DEFAULT '',
 payment_code TEXT NOT NULL DEFAULT '',
 content TEXT NOT NULL DEFAULT '',
 matched_order_id TEXT NOT NULL DEFAULT '',
 matched_order_code TEXT NOT NULL DEFAULT '',
 process_status TEXT NOT NULL DEFAULT '',
 raw_json TEXT NOT NULL DEFAULT '',
 created_at TEXT NOT NULL
);
INSERT INTO sepay_webhook_logs VALUES('0880e88dd1c042afb2a3492f7da78817','0','SePay','2026-11-25 13:43:52','0000000000','in',10000,'TEST1795589032','SEPAYTEST','SEPAY TEST WEBHOOK','','','OrderNotFound','{"id":0,"gateway":"SePay","transactionDate":"2026-11-25 13:43:52","accountNumber":"0000000000","subAccount":null,"transferType":"in","transferAmount":10000,"accumulated":10000,"code":"SEPAYTEST","content":"SEPAY TEST WEBHOOK","referenceCode":"TEST1795589032","description":"SePay test webhook delivery"}','2026-11-25T06:43:52.3037131+00:00');
INSERT INTO sepay_webhook_logs VALUES('b83cd8e8086046f3aa61d086582ea1b9','102459341','MBBank','2026-11-25 14:08:00','4510100015555','in',100000,'FT26329261606676','','MBVCB.17212192556.121200.LKH261125CB94FF.CT tu 0111000271897 LUU THIEN VAN toi 4510100015555 LUU THIEN VAN tai MB- Ma GD ACSP/ nh121200','766299927fc94c1393b4d3e398a56442','LKH261125CB94FF','Confirmed','{"gateway":"MBBank","transactionDate":"2026-11-25 14:08:00","accountNumber":"4510100015555","subAccount":null,"code":null,"content":"MBVCB.17212192556.121200.LKH261125CB94FF.CT tu 0111000271897 LUU THIEN VAN toi 4510100015555 LUU THIEN VAN tai MB- Ma GD ACSP\/ nh121200","transferType":"in","description":"BankAPINotify MBVCB.17212192556.121200.LKH261125CB94FF.CT tu 0111000271897 LUU THIEN VAN toi 4510100015555 LUU THIEN VAN tai MB- Ma GD ACSP\/ nh121200","transferAmount":100000,"referenceCode":"FT26329261606676","accumulated":0,"id":102459341}','2026-11-25T07:08:57.7303875+00:00');
CREATE INDEX ix_episodes_story ON episodes(story_id,episode_number);
CREATE UNIQUE INDEX ux_bank_receivers_one_default ON bank_receivers(is_default) WHERE is_default=1;
CREATE INDEX ix_orders_user ON orders(user_id,created_at DESC);
CREATE INDEX ix_subscriptions_user ON subscriptions(user_id,ends_at DESC);
CREATE INDEX ix_audit_created ON audit_logs(created_at DESC);
CREATE INDEX ix_story_genres_genre ON story_genres(genre_slug, story_id);
CREATE INDEX ix_reviews_story ON story_reviews(story_id,status,updated_at DESC);
CREATE INDEX ix_story_listeners_qualified ON story_listeners(story_id,qualified_at);
CREATE INDEX ix_story_text_files_story ON story_text_files(story_id, created_at DESC);
CREATE INDEX ix_episodes_story_status ON episodes(story_id,status,episode_number);
CREATE INDEX ix_stories_status_created ON stories(status,created_at DESC);
CREATE INDEX ix_stories_status_updated ON stories(status,updated_at DESC);
CREATE INDEX ix_orders_status_expires ON orders(status,order_type,expires_at);
CREATE INDEX ix_listening_user_updated ON listening_progress(user_id,updated_at DESC);
CREATE INDEX ix_user_story_views_user ON user_story_views(user_id,viewed_at DESC);
CREATE INDEX ix_sepay_webhook_logs_created ON sepay_webhook_logs(created_at DESC);
CREATE INDEX ix_sepay_webhook_logs_tx ON sepay_webhook_logs(sepay_tx_id);
COMMIT;
