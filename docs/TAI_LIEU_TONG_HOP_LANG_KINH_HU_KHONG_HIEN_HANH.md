# LĂNG KÍNH HƯ KHÔNG — TÀI LIỆU DỰ ÁN HỢP NHẤT

**Bản nội dung hiện hành: V1.9.4.5 · Blazor Interactive Server / .NET 9 · Ubuntu VPS**  
**Ngày tổng hợp:** 17/10/2026  
**Phạm vi:** Kiến trúc, chức năng đang áp dụng, bảo mật, vận hành, SEO và hướng dẫn triển khai.

> **Quy tắc hợp nhất:** Tài liệu này thay thế các file Markdown rời trong `docs.zip`. Khi có nhiều mô tả cho cùng một chức năng, chỉ giữ **trạng thái cuối cùng** (ví dụ Premium dùng HLS được bảo vệ, không phải MP3 full trực tiếp). Các hướng dẫn nâng cấp trung gian, sửa lỗi đã hoàn tất, source React/Vite cũ, và triển khai IIS/Windows VPS đã được lược bỏ. Nội dung chức năng chưa bị thay thế vẫn được giữ. Phần triển khai Ubuntu được hợp nhất từ hướng dẫn triển khai hiện hành đã xây dựng cho VPS này.
>
> **Phạm vi xác minh:** Đây là tài liệu hợp nhất từ các file Markdown; không phải kết quả tự động kiểm thử source đang chạy. Cần đối chiếu với code thực tế, chạy `dotnet build/publish` và kiểm thử trên VPS trước khi phát hành.

---

## Mục lục

1. [Tổng quan kiến trúc hiện hành](#1-tổng-quan-kiến-trúc-hiện-hành)
2. [Tài khoản, xác thực và phân quyền](#2-tài-khoản-xác-thực-và-phân-quyền)
3. [Quản lý truyện, thể loại, ảnh và tập MP3](#3-quản-lý-truyện-thể-loại-ảnh-và-tập-mp3)
4. [Cloudflare R2, preview và Premium HLS](#4-cloudflare-r2-preview-và-premium-hls)
5. [Trình phát, Auto Next và tiến độ nghe](#5-trình-phát-auto-next-và-tiến-độ-nghe)
6. [Đánh giá, xu hướng và thư viện](#6-đánh-giá-xu-hướng-và-thư-viện)
7. [Premium, VietQR, ngân hàng và lịch sử](#7-premium-vietqr-ngân-hàng-và-lịch-sử)
8. [SEO tự động, JSON-LD, sitemap và logo](#8-seo-tự-động-json-ld-sitemap-và-logo)
9. [API, dữ liệu và cấu trúc mã nguồn](#9-api-dữ-liệu-và-cấu-trúc-mã-nguồn)
10. [Bảo mật, giới hạn và kiểm thử nghiệp vụ](#10-bảo-mật-giới-hạn-và-kiểm-thử-nghiệp-vụ)
11. [Triển khai Ubuntu VPS đầy đủ](#11-triển-khai-ubuntu-vps-đầy-đủ)
12. [Kiểm tra sau cập nhật và quản trị tài liệu](#12-kiểm-tra-sau-cập-nhật-và-quản-trị-tài-liệu)

---

## 1. Tổng quan kiến trúc hiện hành

### 1.1. Công nghệ

| Thành phần | Cấu hình hiện hành |
|---|---|
| Giao diện | Blazor Interactive Server, Razor Components, Tailwind / giao diện Light responsive |
| Backend | ASP.NET Core .NET 9, Minimal API dùng chung process với Blazor |
| Authentication | Cookie HttpOnly, ASP.NET Core Identity `PasswordHasher<T>`, CSRF/Antiforgery |
| Cơ sở dữ liệu | SQLite, WAL, trên ổ cục bộ VPS |
| Media | Cloudflare R2 bucket **Private**, object keys theo từng truyện |
| Xử lý MP3 | FFmpeg + FFprobe trên server |
| Free | Nghe thử MP3 preview tối đa **300 giây/tập** |
| Premium/Admin | Phát toàn tập bằng **HLS AES-128**, kiểm tra quyền mỗi request |
| Proxy | Nginx hỗ trợ WebSocket/SignalR, HTTPS Let's Encrypt |
| Tên miền | `https://langkinhhukhong.duckdns.org` |
| SEO | SSR dữ liệu công khai, metadata per-page, JSON-LD, sitemap động |

**Luồng truy cập:** Người dùng → HTTPS/Nginx → ASP.NET Core (`127.0.0.1:5110`) → SQLite/R2. MP3 Full, HLS key, các segment và ảnh bìa riêng tư **không** được phục vụ như thư mục công khai trong `wwwroot`.

### 1.2. Cấu trúc thư mục trên Ubuntu

```text
/var/www/lkh-web/                 # Bản Publish Linux, ứng dụng có quyền đọc
/var/www/lkh-lib/
  data/                          # SQLite, audio-private và dữ liệu bền vững
    langkinhhukhong.db
    audio-private/
    dataprotection-keys -> /var/www/lkh-lib/keys   # Nếu đang dùng symlink
  keys/                          # ASP.NET Core Data Protection keys
  logs/                          # Log ứng dụng nếu cấu hình
/etc/langkinhhukhong/lkh.env     # Biến môi trường R2 / cấu hình riêng
/etc/systemd/system/lkh-web.service
/etc/nginx/sites-available/lkh-web
```

Cấu hình hiện tại dùng tên miền DuckDNS thay vì EU.org đang chờ duyệt. **Không hardcode IP public** trong mã ứng dụng; khi chuyển VPS, trỏ DuckDNS về IP mới và giữ `LKH_PUBLIC_ORIGIN` phù hợp.

---

## 2. Tài khoản, xác thực và phân quyền

- Người dùng có thể đăng ký/đăng nhập/đăng xuất; Admin có màn hình quản trị riêng.
- Tài khoản được lưu trong bảng SQLite `users` với các trường cốt lõi: `id`, `username`, `password_hash`, `full_name`, `phone`, `address`, `role`, `status`, `created_at`, `updated_at`.
- Mật khẩu dùng **hash có salt** qua ASP.NET Core Identity `PasswordHasher<T>`; không lưu mật khẩu plaintext trong SQL, log, source hoặc tài liệu.
- Cookie đăng nhập HttpOnly; khi có request cần xác thực, hệ thống đối chiếu trạng thái/role hiện tại từ SQLite. Tài khoản `Locked`/`Deleted` không tiếp tục dùng quyền cũ.
- POST/PUT/PATCH/DELETE từ client được bảo vệ bằng anti-CSRF theo API hiện có.
- Việc tạo/xóa cookie diễn ra qua request HTTP riêng, không cố tạo cookie giữa circuit Blazor đã bắt đầu gửi response.
- Có thao tác **đổi mật khẩu** từ Hồ sơ; sau khi đổi mật khẩu, phiên hiện tại được đăng xuất.
- Trên Production không dùng mật khẩu Admin phát triển. Nếu cần khởi tạo Admin ban đầu, kiểm tra cơ chế bootstrap hiện có trong source và chỉ dùng biến khởi tạo tạm thời; xóa secrets sau khi tạo tài khoản.
- Nếu Admin báo sai mật khẩu, xác minh `users.username`, `role`, `status` và thuật toán băm thực tế. **Không ghi trực tiếp mật khẩu văn bản vào `password_hash`**.

---

## 3. Quản lý truyện, thể loại, ảnh và tập MP3

### 3.1. Thông tin truyện

Từ **Quản trị → Kho truyện**, Admin tạo/sửa truyện với tên, slug, tác giả, thể loại (có thể chọn nhiều), mô tả, ảnh bìa và trạng thái xuất bản. Thể loại quản lý riêng tại **Quản trị → Thể loại**; có thể tạo/sửa/ẩn/hiện và chỉ xóa khi không được sử dụng.

Slug tự tạo từ tên tiếng Việt → chữ ASCII thường nối bằng dấu `-`; trùng slug thì thêm hậu tố số. Sửa tên truyện **không tự đổi slug** để tránh hỏng link SEO và đường dẫn object R2. Nếu cần đổi slug công khai về sau, phải thiết kế redirect 301 riêng.

### 3.2. Ảnh bìa và MP3

- Ảnh bìa chấp nhận JPG/PNG/WebP, theo tài liệu giới hạn **8 MB**, lưu riêng trong bucket R2 hoặc dữ liệu private.
- File MP3: theo UI/API mô tả giới hạn **250 MB/tập**. Đây là giới hạn ứng dụng; Nginx, ASP.NET Core và xử lý request có giới hạn độc lập cần cấu hình khớp.
- Upload tập mới: nhập **số tập**, **tên tập**, chọn MP3; server kiểm tra quyền Admin + CSRF, xử lý FFmpeg, tạo preview/HLS, upload private và lưu tham chiếu trong SQLite.
- Sửa tên/số tập, ẩn/hiện tập hoặc **Thay MP3** trên cùng bộ truyện. Khi thay âm thanh, preview, thời lượng và HLS được tạo lại.
- Xóa tập sau khi xác nhận: dọn metadata/tiến độ liên quan và lập công việc xóa object. Không xóa object cũ trước khi giao dịch DB cập nhật tham chiếu thành công.

### 3.3. Đồng bộ danh sách sau upload

Sau khi API thông báo hoàn tất **bao gồm lưu SQLite**, giao diện Admin tự:

1. Làm mới API danh sách tập và đối chiếu ID tập mới; có thể thử đọc lại ngắn nếu dữ liệu chưa hiện.
2. `InvokeAsync(StateHasChanged)` và render dòng theo `@key` để cập nhật ổn định.
3. Đánh dấu tập vừa upload, cập nhật tổng tập/thống kê.
4. Hiện trạng thái “Đang tải MP3 và xử lý Preview / HLS” trong quá trình request chạy.
5. Cung cấp nút **Làm mới danh sách** nếu lần tải lại chưa đồng bộ.

**File xuất hiện trên R2 chưa có nghĩa là toàn bộ upload đã hoàn tất**: bước tạo HLS hoặc transaction SQLite có thể đang diễn ra. Không bấm upload lại liên tiếp gây trùng số tập.

### 3.4. Hàng đợi dọn object

Bảng `storage_cleanup` lưu việc xóa các object không còn sử dụng. Sau khi đổi ảnh, thay MP3 hoặc xóa tập, server ghi công việc dọn trong transaction; nếu R2 lỗi, Admin có thể bấm **Dọn file cũ** để thử lại (tối đa khoảng 50 công việc/lần theo tài liệu). Hệ thống phải kiểm tra object không còn được tham chiếu trước khi xóa. File orphan từ thời kỳ di chuyển cũ có thể cần xử lý thủ công và đối chiếu kỹ trước khi xóa.

---

## 4. Cloudflare R2, preview và Premium HLS

### 4.1. Bucket và object key

Bucket ví dụ trong tài liệu: `langkinhhukhong-audio`. Bucket luôn ở trạng thái **Private**; không bật `r2.dev` hoặc custom public domain cho dữ liệu âm thanh.

```text
langkinhhukhong-audio/                # Bucket private (tên thực tế lấy từ biến môi trường)
└── stories/
    └── <slug>--<story-id>/
        ├── Image/
        │   └── cover-<id>.webp
        ├── Episodes/
        │   └── Tap-000001-<id>.mp3
        ├── Previews/
        │   └── Tap-000001-<id>.mp3
        └── HLS/
            └── Tap-000001-<id>/
                ├── index.m3u8
                ├── key.bin
                ├── segment-000000.ts
                └── ...
```

Đây là các **tiền tố object key**, không phải thư mục vật lý. `id` được tạo mới khi upload/thay file; sửa số tập không tự đổi tên object đang lưu. SQLite giữ tham chiếu `r2:stories/...` thay vì blob bytes hay URL public.

### 4.2. Quyền phát nội dung — áp dụng mô hình mới nhất

| Đối tượng | Phương thức phát | Quyền |
|---|---|---|
| Free đăng nhập | MP3 **preview tối đa 300 giây** qua `GET /api/episodes/{id}/stream` | Chỉ tập Published được phép nghe |
| Premium còn hạn | HLS AES-128 toàn tập qua `GET /api/episodes/{id}/hls/index.m3u8` và các segment/key | Kiểm tra phiên, trạng thái và Premium ở mỗi request |
| Admin Active | HLS toàn tập tương tự Premium | Role Admin hợp lệ |
| Premium hết hạn | Trở về preview Free; request HLS mới bị từ chối | Không dựa vào UI để cấp quyền |

**Không sử dụng mô tả cũ “Premium nhận MP3 full qua `/stream`”.** MP3 gốc được giữ để Admin xử lý lại; client không nhận đường dẫn tải MP3 gốc qua API phát thông thường. HLS dùng AES-128 **không phải DRM**, không đảm bảo chống sao chép tuyệt đối.

Playlist HLS dùng đường dẫn tương đối tới `index.m3u8`, `key.bin`, segment; API xác minh quyền trước khi đọc từ R2, không đưa khóa R2 vào JS/HTML. Các trình duyệt cần hls.js thì thư viện được tải khi Premium bắt đầu phát; Safari/iOS có thể dùng Native HLS.

### 4.3. Quy trình tạo Preview và HLS

- FFmpeg tạo preview MP3 **300 giây**.
- HLS AES-128 tạo trước khi phát, với segment khoảng **4 giây** theo tài liệu, không mã hóa trực tiếp lúc người dùng Play.
- Upload/Thay MP3 mới sẽ tái tạo đầy đủ Preview + HLS.
- Tập cũ chưa có HLS: Admin dùng chức năng **Tạo HLS** để xử lý từ MP3 gốc đang lưu, không bắt buộc upload lại từ máy cá nhân.
- Do tạo HLS trong request quản trị có thể lâu, phải đợi xử lý hoàn thành; về dài hạn có thể chuyển sang background job + retry.

### 4.4. Di chuyển media đang lưu kiểu cũ

Chỉ với các object thực tế chưa chuyển sang tiền tố truyện: chức năng **Vào thư mục truyện** có thể chuyển file Local/R2 cũ sang cấu trúc theo truyện. Không xóa bản gốc trước khi sao lưu và kiểm thử Free/Premium. Không tự động xóa hoặc di chuyển toàn bộ đối tượng khi khởi động ứng dụng.

---

## 5. Trình phát, Auto Next và tiến độ nghe

### 5.1. Trình phát

Trang chi tiết truyện có ảnh bìa, phần giới thiệu rút gọn và nút **Xem chi tiết nội dung / Thu gọn**. Trình phát tùy biến (không dùng thanh native controls) hỗ trợ Phát/Tạm dừng, tua timeline, ±10 giây, tốc độ phát, âm lượng và lưu tiến độ; UI responsive trên desktop/mobile.

Ẩn nút Download chỉ là thay đổi UI; bảo vệ thực chất là R2 private, cookie/role, phân quyền stream trên server. Phần media được quản lý qua `wwwroot/js/site.js` và trang Razor, không dùng JS làm lớp kiểm soát quyền truy cập.

### 5.2. Tự phát tập tiếp theo

- Mặc định bật **Tự phát tiếp**; người dùng có thể bật/tắt trên thanh audio.
- Khi media phát event `ended`, chọn **tập Published kế tiếp trong cùng bộ truyện** theo `episode_number`, cập nhật thông tin tập và bắt đầu phát.
- Hết tập cuối thì dừng; **không** vòng về tập đầu.
- Giữ playlist khi chuyển giữa Trang chủ, Thư viện, trang chi tiết hoặc trang khác.
- Giữ tốc độ và âm lượng giữa các tập trong phiên trình duyệt.
- Không tự nhảy tập khi người dùng tạm dừng, gặp lỗi stream hoặc đóng player.
- Free chỉ auto-play sang **preview** tập kế tiếp; Premium/Admin sang HLS theo quyền hiện tại.
- Trình duyệt, đặc biệt iOS/Safari, có thể chặn autoplay sau khi đổi source: lúc đó người dùng cần nhấn Phát lại.

### 5.3. Ghi nhận tiến độ và lượt nghe hợp lệ

Lưu vị trí nghe để tiếp tục. **Vị trí tua (`listening_progress.seconds`) không dùng để xếp hạng phổ biến.** Hệ thống tạo playback session khi audio phát thực sự và gửi heartbeat khoảng 10 giây; backend đo theo thời gian server, chỉ chấp nhận khoảng 3–25 giây giữa các lượt hợp lệ; không cộng khoảng thời gian tab treo/mất kết nối.

Khi tài khoản nghe trong một bộ truyện tích lũy **lớn hơn 300 giây**, một bản ghi được đánh dấu `qualified_at`. Mỗi tài khoản chỉ đóng góp một lượt hợp lệ/truyện; Admin không được tính. Đây là cơ chế hạn chế thao túng bằng seek, **không phải chứng minh tuyệt đối đã nghe thật**.

---

## 6. Đánh giá, xu hướng và thư viện

- Thành viên đăng nhập có thể chấm **1–5 sao**, bình luận tối đa **1.200 ký tự** ở trang truyện.
- Mỗi user một review mỗi truyện; có thể sửa/xóa.
- Admin quản lý ẩn/hiện; đánh giá bị ẩn không tính vào điểm trung bình; nội dung bình luận HTML-encode khi hiển thị.
- Trang chủ ưu tiên **Truyện mới cập nhật**; khu vực **Nghe tiếp** chỉ hiện khi có lịch sử. Giao diện gọn, không dùng khối hero lớn hoặc banner bán Premium cuối trang (mục Premium vẫn từ điều hướng).
- **Truyện mới** dựa theo thời điểm tạo `stories.created_at`; **Xem nhiều nhất** dựa số người dùng có lượt nghe đạt ngưỡng thực tế `qualified_at`.
- Thư viện cho phép lọc nhiều thể loại, đổi sắp xếp **Mới nhất / Xem nhiều nhất**.
- Các đường dẫn như `/`, `/thu-vien`, `/lich-su`, `/premium`, `/truyen/{slug}`, `/the-loai/{slug}` dùng điều hướng thống nhất và vẫn cập nhật đúng nội dung khi Back/Forward hoặc tải lại trực tiếp.

---

## 7. Premium, VietQR, ngân hàng và lịch sử

### 7.1. Giao diện người dùng

- User Free/Premium hết hạn: xem gói, đăng ký Premium, lịch sử đơn.
- User đang Premium: hiển thị **ngày hết hạn**, trạng thái hoạt động, nút **Gia hạn thêm**; hạn mới nối từ hạn còn hiệu lực khi gia hạn trước hạn.
- Tại `/premium`, lịch sử phân biệt **Tự mua VietQR** và **Admin cấp/gia hạn**; hiển thị đúng loại hồ sơ, thời điểm tạo, số tháng, thời hạn sau gia hạn. Hồ sơ Admin không hiển thị QR hoặc mã giao dịch ngân hàng.

### 7.2. Tài khoản nhận tiền

Tại **Quản trị → Tài khoản QR**, Admin có thể thêm/sửa nhiều tài khoản ngân hàng: tên gợi nhớ, BIN, ngân hàng, số tài khoản và chủ tài khoản. Hệ thống hỗ trợ ngân hàng mặc định duy nhất; tài khoản đầu tiên tự được chọn làm mặc định. Đổi mặc định chỉ ảnh hưởng **đơn mới**; đơn cũ giữ snapshot thông tin thụ hưởng, số tiền và nội dung chuyển khoản.

Có thể ngừng/kích hoạt lại tài khoản; không được ngừng tài khoản mặc định khi chưa chuyển mặc định sang tài khoản khác. Danh sách ngân hàng lấy từ `GET https://api.vietqr.io/v2/banks` có cache, cho phép nhập BIN thủ công khi API không khả dụng. API ngân hàng **không xác thực danh tính chủ tài khoản**.

QR đơn mua sử dụng VietQR Quicklink động chứa số tiền và mã đơn riêng. Không lưu ảnh QR vào SQLite/R2 và không tự nhận diện đã nhận tiền chỉ từ người dùng bấm “Đã chuyển khoản”.

### 7.3. Mua Premium và đối soát

- User tạo đơn và gửi yêu cầu đối soát sau khi chuyển khoản.
- Admin kiểm tra **tiền thực nhận** từ ngân hàng, số tiền, mã giao dịch duy nhất, trạng thái đơn trước khi duyệt.
- Chỉ giao dịch đã đối soát hợp lệ mới ghi `payments`, cập nhật `orders` và kéo dài `subscriptions` trong transaction.
- Không tự duyệt khi người dùng chỉ gửi xác nhận bằng giao diện.

### 7.4. Admin cấp/gia hạn Premium

**Quản trị → Thành viên → Quản lý → Admin cấp/gia hạn Premium**: nhập **1–36 tháng**, lý do bắt buộc tối đa 300 ký tự.

Một thao tác hợp lệ lưu nhất quán:

- `orders`: hồ sơ `order_type='AdminGrant'`, `amount_vnd=0`, người cấp, lý do, thời hạn;
- `subscriptions`: dòng `source='Admin'`, liên kết order;
- `audit_logs`: dấu vết thao tác.

**Không có dòng `payments` và không phát sinh QR ngân hàng.** Để tương thích schema lưu trữ, hồ sơ Admin có thể mang `status='Paid'`; **không bao giờ coi `status='Paid'` một mình là bằng chứng đã chuyển tiền**. Các báo cáo doanh thu chỉ cộng số tiền thật từ `payments`. Việc cấp trùng hoặc backfill lịch sử phải tránh cộng hạn hai lần.

---

## 8. SEO tự động, JSON-LD, sitemap và logo

### 8.1. SEO gắn thương hiệu (nội dung mới nhất)

**Trọng tâm từ khóa:** “Nghe truyện…”, “Nghe Audio…”, “Truyện Audio…”, “Đọc Truyện Audio…” gắn nhãn **Lăng Kính Hư Không**, kết hợp các thể loại và tên tác phẩm thực tế. Không nhồi từ khóa vào từng trang; Google không dùng `meta keywords` như tín hiệu xếp hạng.

| Trang | Title mẫu |
|---|---|
| Trang chủ `/` | `Nghe Truyện Audio Online \| Lăng Kính Hư Không` |
| Thư viện `/thu-vien` | `Truyện Audio – Nghe Audio Theo Thể Loại \| Lăng Kính Hư Không` |
| Thể loại `/the-loai/{slug}` | `Nghe truyện {Tên thể loại} audio \| Lăng Kính Hư Không` |
| Truyện `/truyen/{slug}` | `{Tên truyện} – Nghe truyện audio \| Lăng Kính Hư Không` |

Meta description trang truyện được tạo riêng từ tên, tác giả và **mô tả có thật**, lược HTML và rút độ dài phù hợp. Nội dung công khai của truyện/thể loại được render HTML phía server để bot đọc được kể cả trước khi JS tải dữ liệu. Có canonical, Open Graph, Twitter Card và bìa/ảnh chia sẻ phù hợp.

### 8.2. JSON-LD và dữ liệu có cấu trúc

- `Organization`: tên **Lăng Kính Hư Không**, tên rút gọn `LKHK`, logo mới.
- `WebSite`: định danh website, URL canonical.
- `WebPage`, `CollectionPage`: mô tả và đường dẫn tương ứng trang.
- `Audiobook`: chỉ phản ánh dữ liệu truyện thực tế như tên, tác giả, thể loại, mô tả, ảnh bìa.
- `BreadcrumbList`: breadcrumb công khai theo trang.
- `AggregateRating`: chỉ khi có đánh giá thật phù hợp và được hiển thị; không tạo sao, số lượt, FAQ, ISBN hoặc hành động mua/sách giả.

JSON-LD hợp lệ không đồng nghĩa Google sẽ hiển thị Rich Result. Nên kiểm tra bằng validator.schema.org và công cụ Google phù hợp với loại schema thực tế.

### 8.3. Sitemap và robots

```text
/robots.txt
/sitemap.xml                  # Sitemap index
/sitemap-static.xml           # Trang chủ, thư viện, Premium, thể loại đủ điều kiện
/sitemap-stories-1.xml        # Truyện Published, phân trang khi nhiều
/sitemap-stories-2.xml        # Chỉ có khi số lượng đủ lớn
```

Sitemap truyện chỉ gồm `Published`; `/the-loai/{slug}` chỉ được index/sitemap khi có truyện Published. Mỗi sitemap truyện tối đa khoảng **10.000** tác phẩm theo tài liệu. `lastmod` lấy từ thời gian cập nhật có thật của truyện/tập; sitemap tự sinh từ SQLite, không cần cron. Không lộ URL Admin hoặc API nhạy cảm.

Trang slug không tồn tại có thể được render route 200 kèm `noindex` theo ghi chú tài liệu; đây là **giới hạn cần kiểm thử/cải thiện để trả HTTP 404 thực** chứ không nên coi đã xử lý hoàn toàn.

### 8.4. Logo chính thức

Dùng **ảnh logo do chủ dự án cung cấp**, giữ nguyên hình nhân vật và chữ. Bản hiện hành bao gồm:

| Tệp/URL | Công dụng |
|---|---|
| `/images/lkh-logo.png` (512×512) | Logo Organization cho SEO |
| `/images/lkh-logo.webp` (360×360) | Header/sidebar/mobile |
| `/images/lkh-social.png` (1200×630) | Open Graph / mạng xã hội |
| `/favicon-48.png`, `/favicon-96.png` | Favicon |
| `/apple-touch-icon.png` | Biểu tượng Apple |
| `/images/lkh-logo.svg`, `/favicon.svg` | **SVG wrapper chứa ảnh raster**, không phải vector thuần; giữ tương thích URL |

Lưu ý chất lượng nhận diện: ảnh gốc có chữ **“Review Truyện Tranh”**, khác với nội dung website truyện audio; logo được giữ nguyên theo yêu cầu. Khi muốn sửa chữ trên ảnh, cần tạo một phiên bản logo mới thay vì đổi metadata một cách giả tạo.

### 8.5. Google Search Console

Xác minh DuckDNS bằng bản ghi TXT `google-site-verification=...` hoặc URL Prefix và nộp **một sitemap chính** `https://langkinhhukhong.duckdns.org/sitemap.xml`. Dùng URL Inspection cho trang chủ, truyện Published và thể loại. Theo dõi Search Console Performance/Google Trends để điều chỉnh nội dung theo dữ liệu có thật; **không có cam kết Top Google** nhờ thẻ meta đơn thuần.

---

## 9. API, dữ liệu và cấu trúc mã nguồn

### 9.1. Các endpoint chính được mô tả trong tài liệu

| API | Mục đích / kiểm soát |
|---|---|
| `GET /api/health` | Health-check |
| `GET /api/genres` | Thể loại |
| `GET /api/stories?sort=new\|popular&genre=<slug>` | Danh sách/lọc truyện |
| `GET /api/stories/{slug}/reviews` | Đánh giá công khai |
| `PUT/DELETE /api/stories/{slug}/reviews/me` | Review bản thân, yêu cầu auth + CSRF |
| `POST /api/episodes/{id}/playback/start` | Bắt đầu phiên thống kê nghe |
| `POST /api/episodes/{id}/playback/ping` | Heartbeat phiên nghe |
| `GET /api/episodes/{id}/stream` | Preview MP3 đã kiểm tra quyền |
| `GET /api/episodes/{id}/hls/{filename}` | Manifest/key/segments HLS, kiểm tra quyền Premium/Admin |
| `POST /api/admin/stories` | Tạo truyện |
| `PUT /api/admin/stories/{id}` | Sửa thông tin truyện |
| `POST /api/admin/stories/{id}/cover` | Upload/thay bìa |
| `POST /api/admin/stories/{id}/episodes` | Upload tập, tạo preview/HLS |
| `GET /api/admin/stories/{id}/episodes` | Lấy lại danh sách tập |
| `PUT /api/admin/episodes/{id}` | Sửa tập |
| `PUT /api/admin/episodes/{id}/audio` | Thay MP3 |
| `DELETE /api/admin/episodes/{id}` | Xóa tập/lập hàng đợi dọn |
| `GET /api/admin/storage` | Trạng thái storage / dọn object |
| `POST /api/admin/storage/cleanup` | Thử dọn object cũ |
| `/api/admin/users/{id}/premium` | Admin cấp/gia hạn Premium |
| `/api/admin/genres/...`, `/api/admin/reviews/...` | Quản trị thể loại/đánh giá |

API chi tiết và verb của một số tác vụ Admin cần đối chiếu trực tiếp `Program.cs` ở source hiện hành khi tích hợp bên ngoài; đây là danh mục từ tài liệu, không phải OpenAPI tự sinh.

### 9.2. SQLite

Các bảng đã được xác nhận trên môi trường VPS trong quá trình vận hành:

```text
app_migrations      genres              storage_cleanup     subscription_plans
audit_logs          listening_progress  stories             subscriptions
bank_accounts       orders              story_genres        users
bank_receivers      payments            story_listeners
episodes            playback_sessions   story_reviews
```

- `users` giữ mật khẩu dạng hash, role/status.
- `stories`/`episodes` giữ thông tin truyện/tập và object references; không nhét MP3 vào SQLite.
- `story_genres`, `genres`, `story_reviews` phục vụ phân loại/đánh giá.
- `playback_sessions`, `story_listeners`, `listening_progress` phục vụ phát và thống kê.
- `orders`, `payments`, `subscriptions` phục vụ Premium/đối soát; `bank_receivers` chứa nhiều tài khoản nhận tiền, `bank_accounts` là dữ liệu tương thích.
- `storage_cleanup` quản lý các file cần xóa; `app_migrations` theo dõi các lần cập nhật schema.

Khi nâng cấp, phải **backup SQLite bằng SQLite Online Backup API**. Không xóa `.db-shm/.db-wal` đang mở và không tự reset database để chạy migration.

### 9.3. Source hiện hành

```text
LangKinhHuKhong.Web/
  Program.cs                         # API, auth, middleware, DI, endpoints SEO
  Database.cs                        # SQLite + khởi tạo/migration
  Components/
    App.razor                        # Head / script / CSS
    Pages/
      Home.razor                     # SSR trang công khai, thư viện, chi tiết, player
      AdminPanel.razor               # Quản trị, MP3 và ngân hàng
    Shared/                           # BrowserApi, SEO head ...
  wwwroot/
    css/site.css
    js/site.js
    images/lkh-logo.png
    images/lkh-logo.webp
    images/lkh-social.png
    ...
```

Các thành phần SEO chuyên dụng được mô tả là `SeoService.cs` và `SeoHead.razor`; vị trí cụ thể cần xem cấu trúc source thực tế khi tìm file. Hệ thống không dùng React/Vite để render giao diện, không cần Node.js khi chạy trên VPS.

---

## 10. Bảo mật, giới hạn và kiểm thử nghiệp vụ

### 10.1. Quy tắc bảo mật bắt buộc

1. R2 audio private, token chỉ quyền Object Read & Write trên bucket; khóa nằm ở file env được bảo vệ; **không đưa Access Key/Secret hoặc DuckDNS token vào JS, HTML, Git hay tin nhắn**.
2. `ASPNETCORE_ENVIRONMENT=Production`; cookie Secure/CSRF giữ nguyên; dùng `UseForwardedHeaders` đúng thứ tự trước Authentication/Antiforgery; chỉ tin cậy proxy đã biết.
3. Kiểm tra quyền User Free/Premium/Admin **tại API**, không chỉ ẩn nút trên giao diện.
4. Người dùng Premium hợp lệ vẫn có thể trích xuất audio; HLS AES-128 không phải DRM.
5. SQLite chạy trên ổ local, một instance ghi chính; kiểm thử tải khi có nhiều người dùng đồng thời.
6. Lưu log và audit phù hợp, không lộ thông tin ngân hàng hoặc khóa riêng; sao lưu thử phục hồi định kỳ.
7. Nginx phục vụ nhiều website: chỉ sửa cấu hình vhost của Lăng Kính Hư Không, không xóa site khác.

### 10.2. Checklist chức năng sau mỗi lần Publish

- [ ] Trang chủ, thư viện, thể loại, chi tiết truyện tải được khi mở URL trực tiếp và dùng Back/Forward.
- [ ] Login/logout, Cookie, CSRF, đổi mật khẩu, tài khoản khóa và vai trò Admin hoạt động.
- [ ] Admin tạo/sửa/ẩn/xuất bản truyện, nhiều thể loại, ảnh bìa.
- [ ] Upload MP3 tạo Preview + HLS, danh sách tập tự refresh ngay sau hoàn tất.
- [ ] Thay MP3/Xóa tập/R2 cleanup không làm mất object đang được sử dụng.
- [ ] Free chỉ nghe preview 300 giây; không mở được `/hls/index.m3u8` hoặc key HLS.
- [ ] Premium/Admin nghe HLS toàn tập, seek và phát tự động sang tập kế tiếp.
- [ ] Hết hạn Premium/khóa User: request HLS mới bị từ chối.
- [ ] Review 1–5 sao, ẩn review, tính xu hướng >300 giây chính xác.
- [ ] Nhiều tài khoản VietQR; đổi mặc định không sửa đơn cũ; duyệt chỉ sau đối soát tiền thật.
- [ ] Admin cấp Premium và lịch sử không bị tính nhầm doanh thu.
- [ ] SEO canonical/JSON-LD theo trang, robots/sitemap và logo tải đúng.
- [ ] Chrome Network → WS có WebSocket nếu điều kiện kết nối cho phép; UI không mất circuit.

### 10.3. Giới hạn kỹ thuật cần lưu ý

- Tạo HLS trong request Admin có thể quá lâu khi MP3 lớn hoặc mạng R2 chậm; có thể cần tác vụ nền.
- Không thể bảo đảm Google index/rank từ meta/schema; phải theo dõi content, trải nghiệm và Search Console.
- Nên xem xét nâng .NET lên LTS mới phù hợp trước khi .NET 9 hết vòng đời hỗ trợ; **không chỉ sửa TargetFramework mà bỏ qua nâng package/test**.
- Nếu ứng dụng dùng JS hls.js từ CDN thì trình duyệt hoàn toàn offline sẽ cần phương án tự host thư viện.

---

## 11. Triển khai Ubuntu VPS đầy đủ

> Phần này thay thế các tài liệu triển khai Windows/IIS cũ. Làm lần lượt khi cài VPS mới; khi **nâng cấp** chỉ thực hiện bước Publish + sao lưu + thay phiên bản, không cài lại DNS/Nginx/SSL. Đường dẫn và tên miền là cấu hình thực tế của dự án, IP minh họa cần xác nhận lại nếu chuyển VPS.

### 1. Cài FFmpeg và các gói cần thiết

#### 1.1. Kiểm tra kiến trúc và hệ điều hành

```bash
uname -m
lsb_release -a
```

Kết quả mong muốn: `x86_64`, Ubuntu 22.04 (hoặc Ubuntu tương thích).

#### 1.2. Cập nhật package và cài công cụ

```bash
sudo apt update
sudo apt install -y ffmpeg nginx curl ca-certificates dnsutils sqlite3 rsync
```

Để tránh xung đột PATH và quyền thực thi, dùng gói **FFmpeg từ APT** thay vì Snap trong trường hợp này.

#### 1.3. Xác nhận FFmpeg/FFprobe chạy được với `www-data`

```bash
ffmpeg -version | head -n 1
ffprobe -version | head -n 1
sudo -u www-data ffmpeg -version | head -n 1
sudo -u www-data ffprobe -version | head -n 1
command -v ffmpeg
command -v ffprobe
```

Nếu `sudo -u www-data` vẫn hiện phiên bản, tài khoản chạy dịch vụ có thể gọi FFmpeg. Một số chức năng upload còn cần quyền ghi vào thư mục tạm và quyền truy cập Cloudflare R2.

#### 1.4. Kiểm tra ASP.NET Core Runtime 9

```bash
dotnet --info
dotnet --list-runtimes
```

Cần có `Microsoft.AspNetCore.App 9.x` và `Microsoft.NETCore.App 9.x`. Nếu chưa có runtime, cài **ASP.NET Core Runtime 9** theo hướng dẫn chính thức phù hợp phiên bản Ubuntu (có thể cần nguồn gói .NET được cấu hình trước), sau đó kiểm tra lại:

```bash
sudo apt install -y aspnetcore-runtime-9.0
dotnet --list-runtimes
```

Nếu APT báo không tìm thấy gói, **không dùng runtime phiên bản khác thay thế**; xem: <https://learn.microsoft.com/dotnet/core/install/linux-ubuntu>.

---

### 2. Publish project .NET 9 cho Linux

> Các lệnh này chạy trên **Windows PowerShell** ở máy phát triển, **không phải** SSH Ubuntu.

#### 2.1. Mở thư mục project

```powershell
Set-Location 'D:\Dev\LangKinhHuKhong'
dotnet --version
```

Cần .NET SDK 9 phù hợp source. Nếu source giải nén nơi khác, thay đường dẫn.

#### 2.2. Xóa thư mục output cũ và Publish Linux x64

```powershell
Remove-Item '.\publish\lkh-linux' -Recurse -Force -ErrorAction SilentlyContinue

dotnet restore '.\LangKinhHuKhong.Web\LangKinhHuKhong.Web.csproj' -r linux-x64

dotnet publish '.\LangKinhHuKhong.Web\LangKinhHuKhong.Web.csproj' `
  -c Release `
  -r linux-x64 `
  --self-contained false `
  --no-restore `
  -o '.\publish\lkh-linux'
```

**Không** dùng bản `win-x64` hay thư mục publish của IIS để chạy Ubuntu.

#### 2.3. Kiểm tra file publish

```powershell
Test-Path '.\publish\lkh-linux\LangKinhHuKhong.Web.dll'
Get-ChildItem '.\publish\lkh-linux' -Recurse -Filter 'libe_sqlite3.so'
Get-ChildItem '.\publish\lkh-linux' | Select-Object Name
```

Phải có `LangKinhHuKhong.Web.dll`, `.deps.json`, `.runtimeconfig.json`, `wwwroot/` và thư viện Linux SQLite `libe_sqlite3.so` (trong thư mục gốc hoặc vị trí native asset được Publish cung cấp). Nếu thư viện không xuất hiện, kiểm tra dependency SQLitePCLRaw trong `.csproj` **trước khi upload**.

#### 2.4. Chú ý cấu hình HTTPS Forwarded Headers trong `Program.cs`

Nginx kết thúc SSL/TLS, sau đó kết nối HTTP nội bộ đến Kestrel. Trong source phải có middleware Forwarded Headers **trước Authentication, Authorization, Antiforgery, HTTPS redirection và middleware tùy chỉnh đọc `Request.Scheme`**:

```csharp
using Microsoft.AspNetCore.HttpOverrides;

// Sau: var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});
```

**Chỉ thêm nếu chưa có**, không nhân đôi middleware. Nginx và Kestrel cùng VPS, proxy đi qua `127.0.0.1` vốn được ASP.NET Core tin cậy theo mặc định; không bỏ giới hạn trusted proxies. Nếu thiếu, trang chủ có thể trả 500 với thông báo `AntiforgeryOptions.Cookie.SecurePolicy = Always ... current request is not an SSL request`.

---

### 3. Upload lên `/var/www/lkh-web` và phân quyền `/var/www/lkh-lib`

#### 3.1. Tạo thư mục tách biệt ứng dụng và dữ liệu

Chạy trên **Ubuntu VPS**:

```bash
sudo install -d -o root -g root -m 755 /var/www/lkh-web
sudo install -d -o root -g www-data -m 750 /var/www/lkh-lib
sudo install -d -o www-data -g www-data -m 750 /var/www/lkh-lib/data
sudo install -d -o www-data -g www-data -m 750 /var/www/lkh-lib/keys
sudo install -d -o www-data -g www-data -m 750 /var/www/lkh-lib/logs
sudo install -d -o www-data -g www-data -m 750 /var/www/lkh-lib/data/audio-private
```

> Nếu các thư mục đã tồn tại, `install -d` không xóa nội dung. Không `chown -R` trên `/var/www` vì VPS có nhiều site khác.

#### 3.2. Khóa Data Protection

Dự án từng sử dụng đường dẫn `data/dataprotection-keys`; trên VPS có thể dùng **symlink đến thư mục keys riêng**. Chỉ tạo liên kết nếu đường dẫn đó chưa tồn tại:

```bash
if [ ! -e /var/www/lkh-lib/data/dataprotection-keys ] && \
   [ ! -L /var/www/lkh-lib/data/dataprotection-keys ]; then
  sudo ln -s /var/www/lkh-lib/keys \
    /var/www/lkh-lib/data/dataprotection-keys
fi

namei -l /var/www/lkh-lib/data/dataprotection-keys
sudo -u www-data test -w /var/www/lkh-lib/data/dataprotection-keys \
  && echo 'DATA PROTECTION OK'
```

Nếu có thư mục keys cũ, **không thay nó bằng symlink tùy tiện**. Giữ nguyên khóa đang dùng vì mất khóa có thể làm mất hiệu lực cookie/phiên cũ.

#### 3.3. Upload file bằng WinSCP/SFTP

Từ Windows, upload **toàn bộ nội dung** của:

```text
D:\Dev\LangKinhHuKhong\publish\lkh-linux\
```

đến VPS:

```text
/var/www/lkh-web/
```

**Khi nâng cấp bản đã hoạt động**, nên upload vào `/var/www/lkh-web-stage/`, kiểm tra đủ file rồi **dừng service và đổi thư mục** để tránh trộn file cũ/mới. Ví dụ:

```bash
# CHỈ khi đã upload đầy đủ /var/www/lkh-web-stage
sudo test -f /var/www/lkh-web-stage/LangKinhHuKhong.Web.dll \
  || { echo 'THIEU DLL - DUNG LAI'; exit 1; }

sudo systemctl stop lkh-web
sudo mv /var/www/lkh-web "/var/www/lkh-web-backup-$(date +%Y%m%d-%H%M%S)"
sudo mv /var/www/lkh-web-stage /var/www/lkh-web
```

Nếu nâng cấp trên máy có nhiều bản backup, ghi lại đúng tên thư mục backup vừa tạo để có thể rollback.

#### 3.4. Quyền thư mục

```bash
sudo chown -R root:root /var/www/lkh-web
sudo chmod -R a+rX /var/www/lkh-web

sudo chown root:www-data /var/www/lkh-lib
sudo chmod 750 /var/www/lkh-lib

sudo chown -R www-data:www-data /var/www/lkh-lib/data \
  /var/www/lkh-lib/keys /var/www/lkh-lib/logs
sudo chmod 750 /var/www/lkh-lib/data /var/www/lkh-lib/keys \
  /var/www/lkh-lib/logs /var/www/lkh-lib/data/audio-private

sudo -u www-data test -r /var/www/lkh-web/LangKinhHuKhong.Web.dll \
  && echo 'APP READ OK'
sudo -u www-data test -w /var/www/lkh-lib/data \
  && echo 'DATA WRITE OK'
sudo -u www-data test -w /var/www/lkh-lib/data/audio-private \
  && echo 'AUDIO WRITE OK'
```

**Không dùng `chmod 777`**. Bản web chỉ cần quyền đọc; tài khoản `www-data` cần quyền ghi ở thư mục dữ liệu, không phải toàn bộ source.

#### 3.5. Sao lưu SQLite đúng cách (khi đã có dữ liệu)

Trước khi nâng cấp schema hoặc bản Publish:

```bash
sudo mkdir -p /var/backups/lkh
sudo sqlite3 /var/www/lkh-lib/data/langkinhhukhong.db \
  ".backup '/var/backups/lkh/langkinhhukhong-$(date +%Y%m%d-%H%M%S).db'"
```

Lệnh `.backup` dùng SQLite Backup API để tạo bản sao nhất quán ngay cả khi database đang dùng WAL. Sao lưu **cả thư mục `/var/www/lkh-lib/keys`** theo quy trình backup bảo mật của bạn, không công khai các khóa đó.

---

### 4. Tạo systemd service chạy Blazor

#### 4.1. Tạo file cấu hình bí mật (Cloudflare R2)

```bash
sudo install -d -o root -g root -m 700 /etc/langkinhhukhong
sudo nano /etc/langkinhhukhong/lkh.env
```

Mẫu nội dung (thay các giá trị ví dụ bằng **khóa R2 thực tế**, không sao chép nguyên dấu `<...>`):

```ini
LKH_AUDIO_STORAGE=R2
LKH_PUBLIC_ORIGIN=https://langkinhhukhong.duckdns.org
R2_ACCOUNT_ID=<CLOUDFLARE_ACCOUNT_ID>
R2_BUCKET_NAME=<R2_BUCKET_NAME>
R2_ACCESS_KEY_ID=<ACCESS_KEY_ID>
R2_SECRET_ACCESS_KEY=<SECRET_ACCESS_KEY>
```

Lưu ý: nếu source đang dùng tên biến R2 khác, ưu tiên tên biến trong mã nguồn hiện tại. Tránh thêm bình luận vào cuối dòng giá trị; nếu có dấu/khoảng trắng đặc biệt cần dùng cú pháp EnvironmentFile hợp lệ.

Cấp quyền:

```bash
sudo chown root:root /etc/langkinhhukhong/lkh.env
sudo chmod 600 /etc/langkinhhukhong/lkh.env
```

`systemd` sẽ đọc EnvironmentFile trước khi chạy ứng dụng dưới quyền `www-data`. **Không in nội dung file env ra chat/log**.

#### 4.2. Tạo service

```bash
sudo nano /etc/systemd/system/lkh-web.service
```

Nội dung:

```ini
[Unit]
Description=Lang Kinh Hu Khong - Blazor Server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=www-data
Group=www-data
WorkingDirectory=/var/www/lkh-web
ExecStart=/usr/bin/dotnet /var/www/lkh-web/LangKinhHuKhong.Web.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5110
Environment=LKH_DATA_ROOT=/var/www/lkh-lib/data
EnvironmentFile=/etc/langkinhhukhong/lkh.env
Restart=always
RestartSec=5
TimeoutStopSec=30
UMask=0027

[Install]
WantedBy=multi-user.target
```

#### 4.3. Khởi động và tự chạy khi boot

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now lkh-web
sudo systemctl status lkh-web --no-pager -l
```

Kiểm tra Backend nội bộ:

```bash
curl -i http://127.0.0.1:5110/api/health
```

Kỳ vọng:

```text
HTTP/1.1 200 OK
{"status":"ok", ...}
```

Nếu không chạy:

```bash
sudo journalctl -u lkh-web -n 100 --no-pager -o cat
sudo systemctl reset-failed lkh-web
```

**Chưa cấu hình Nginx nếu Backend còn lỗi startup.**

---

### 5. Tạo tên miền miễn phí DuckDNS

#### 5.1. Đăng ký tên miền

1. Truy cập <https://www.duckdns.org/> và đăng nhập.
2. Tại ô *sub domain*, nhập `langkinhhukhong` (chỉ phần đầu, không nhập `.duckdns.org`).
3. Nhấn **add domain**. Tên miền đầy đủ là `langkinhhukhong.duckdns.org`.
4. Tại ô **current ip**, nhập **IP public của VPS** rồi nhấn **update ip**.

Lấy IP thực tế từ VPS:

```bash
curl -4fsS https://api.ipify.org; echo
```

Ví dụ trong đợt triển khai trước: `160.191.175.132`. **Đừng lấy IP mạng gia đình** nếu server chạy trên VPS.

#### 5.2. Kiểm tra DNS đã khớp VPS

```bash
dig +short @1.1.1.1 langkinhhukhong.duckdns.org A
dig +short @8.8.8.8 langkinhhukhong.duckdns.org A
dig +short langkinhhukhong.duckdns.org AAAA
```

Nếu đã thêm AAAA/IPv6 thì địa chỉ đó cũng phải trỏ đúng server có hỗ trợ IPv6; nếu không dùng IPv6, tránh giữ bản ghi AAAA sai.

#### 5.3. Cập nhật IP qua DuckDNS API (tùy chọn)

**Token DuckDNS là bí mật.** Nếu token đã từng bị dán trong chat/screenshot, nên đổi/rotate token trước và cập nhật mọi script cũ.

```bash
read -rsp 'DuckDNS Token: ' DUCK_TOKEN; echo
VPS_IP="$(curl -4fsS https://api.ipify.org)"

curl -fsSG 'https://www.duckdns.org/update' \
  --data-urlencode 'domains=langkinhhukhong' \
  --data-urlencode "token=$DUCK_TOKEN" \
  --data-urlencode "ip=$VPS_IP" \
  --data-urlencode 'verbose=true'

unset DUCK_TOKEN VPS_IP
```

Kết quả: `OK` là thành công; `KO` thường do token/domain không hợp lệ. Khi dùng token trên dòng lệnh, giá trị có thể xuất hiện rất ngắn trong danh sách tiến trình; tránh chạy trong shell đang bật debug `set -x`, và tuyệt đối không ghi token trực tiếp vào lịch sử lệnh.

VPS có IP cố định thì thường **không cần** lịch tự cập nhật. Nếu IP VPS thay đổi theo thời gian, mới cấu hình updater định kỳ một cách an toàn.

---

### 6. Nginx reverse proxy + WebSocket

> Nếu VPS có các site như `n8n`, `chamcong...`, `checker...`, **không xóa** hoặc thay cấu hình của những site đó. Tạo site riêng `lkh-web`.

#### 6.1. Cài và khởi động Nginx

```bash
sudo apt install -y nginx
sudo systemctl enable --now nginx
sudo systemctl status nginx --no-pager
```

#### 6.2. Mở firewall (nếu cần)

```bash
sudo ufw status
```

- Nếu `Status: inactive`: các quy tắc `ufw allow` chưa có hiệu lực; **không bật UFW vội** trên VPS nhiều dịch vụ. Cần xác nhận SSH và cổng các dịch vụ khác được phép trước.
- Nếu UFW **đang active**: cho phép cổng 80/443:

```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
```

Đồng thời mở TCP 80/443 ở firewall/security group của nhà cung cấp VPS. **Không mở cổng 5110 ra Internet**.

#### 6.3. Cấu hình WebSocket dùng chung cho site LKHK

```bash
sudo nano /etc/nginx/conf.d/lkh-websocket.conf
```

```nginx
map $http_upgrade $lkh_connection_upgrade {
    default upgrade;
    ''      close;
}
```

Nginx sẽ dùng biến `$lkh_connection_upgrade` riêng của website này, tránh trùng tên map của website khác.

#### 6.4. Tạo server block riêng

```bash
sudo nano /etc/nginx/sites-available/lkh-web
```

```nginx
server {
    listen 80;
    server_name langkinhhukhong.duckdns.org;

    # Giới hạn phía Nginx, không thay giới hạn upload bên .NET
    client_max_body_size 1024m;
    client_body_timeout 600s;

    access_log /var/log/nginx/lkh-access.log;
    error_log  /var/log/nginx/lkh-error.log warn;

    location / {
        proxy_pass http://127.0.0.1:5110;
        proxy_http_version 1.1;

        # Blazor Server / SignalR / WebSocket
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $lkh_connection_upgrade;

        # Host, IP và HTTPS gốc
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_connect_timeout 60s;
        proxy_send_timeout 600s;
        proxy_read_timeout 3600s;

        # Chuyển tiếp luồng upload lớn; vẫn chịu giới hạn .NET
        proxy_request_buffering off;
    }
}
```

> Nếu một phiên bản site đã có SSL do Certbot tạo, **không dán đè nguyên file này lên cấu hình HTTPS đang chạy**. Chỉ sửa những dòng cần thiết và sao lưu trước.

#### 6.5. Kích hoạt cấu hình và kiểm tra

```bash
sudo ln -s /etc/nginx/sites-available/lkh-web \
  /etc/nginx/sites-enabled/lkh-web
sudo nginx -t
sudo systemctl reload nginx
```

Nếu `ln` báo **File exists**, kiểm tra liên kết đang có thay vì tạo lặp.

Kiểm tra reverse proxy **trước khi có SSL**:

```bash
curl -i -H 'Host: langkinhhukhong.duckdns.org' \
  http://127.0.0.1/api/health
```

Kết quả mong muốn `200 OK`. Nếu `502 Bad Gateway`, kiểm tra `lkh-web.service`, cổng 5110 và log Nginx.

---

### 7. Cài HTTPS miễn phí Let's Encrypt

#### 7.1. Điều kiện trước khi cấp SSL

- DNS A `langkinhhukhong.duckdns.org` đã trỏ đúng IP VPS.
- Nginx `sudo nginx -t` trả `syntax is ok` / `test is successful`.
- Cổng TCP **80 và 443** mở tại firewall công khai.
- Có thể truy cập HTTP qua tên miền từ **mạng bên ngoài VPS** (không chỉ curl 127.0.0.1).

#### 7.2. Cài Certbot và sao lưu Nginx

```bash
sudo apt update
sudo apt install -y certbot python3-certbot-nginx

sudo cp /etc/nginx/sites-available/lkh-web \
  "/etc/nginx/sites-available/lkh-web.bak-$(date +%Y%m%d-%H%M%S)"
```

Không cần cài Certbot qua Snap nếu đã chọn gói APT và đang sử dụng tốt; tránh trộn hai cách cài cùng lúc.

#### 7.3. Cấp và tự cấu hình HTTPS

```bash
sudo certbot --nginx \
  -d langkinhhukhong.duckdns.org \
  --redirect
```

Certbot sẽ xác thực bằng HTTP-01 qua cổng 80, bổ sung cấu hình chứng chỉ và chuyển hướng HTTP → HTTPS cho **server block khớp tên miền**.

#### 7.4. Kiểm tra chứng chỉ và tự gia hạn

```bash
sudo nginx -t
sudo systemctl reload nginx

curl -I http://langkinhhukhong.duckdns.org
curl -i https://langkinhhukhong.duckdns.org/api/health
curl -sS -o /dev/null -w 'HOME: %{http_code}\n' \
  https://langkinhhukhong.duckdns.org/

sudo certbot certificates
sudo certbot renew --dry-run
systemctl list-timers --all | grep -i certbot || true
```

Mong đợi: HTTP tự chuyển sang HTTPS (`301/308`), HTTPS API trả `200`, trang chủ cũng trả `200` nếu Blazor hoạt động bình thường. **Không dùng `curl -k`** để che lỗi chứng chỉ.

#### 7.5. Test WebSocket thật trong trình duyệt

1. Mở `https://langkinhhukhong.duckdns.org`.
2. Chrome → **F12 → Network → WS**.
3. Tải lại trang, vào chức năng tương tác Blazor.
4. Kết nối `/_blazor` khi dùng WebSocket thường có status **101 Switching Protocols**.

Nếu `400`, `502`, hoặc ngắt kết nối, kiểm tra `Upgrade`, `Connection`, `proxy_read_timeout` và log ứng dụng. Một số trường hợp SignalR dùng cơ chế transport dự phòng; status 101 phụ thuộc kết nối WebSocket thực tế.

---

### 8. Kết nối Google Search Console bằng TXT Token DuckDNS

> **Phân biệt:** Token DuckDNS là bí mật để gọi API DNS. Giá trị `google-site-verification=...` là mã xác minh do Google Search Console cung cấp. Hai giá trị **khác nhau**. Google không cần biết token DuckDNS.

#### 8.1. Tạo Property trong Google Search Console

1. Truy cập <https://search.google.com/search-console/> và đăng nhập Google.
2. Chọn **Add property**.
3. Chọn **Domain** và nhập `langkinhhukhong.duckdns.org` (không thêm `https://`); hoặc chọn **URL prefix** và nhập `https://langkinhhukhong.duckdns.org/` nếu muốn phương thức xác minh HTML thay thế.
4. Với **Domain**, Google sẽ cung cấp bản ghi TXT, có dạng:

```text
google-site-verification=MA_XAC_MINH_DO_GOOGLE_CAP
```

Không nhập chuỗi mẫu này; copy nguyên văn mã của property bạn vừa tạo.

#### 8.2. Đặt TXT qua DuckDNS API từ VPS

DuckDNS có TXT API, nhưng **không có ô nhập TXT trên giao diện DuckDNS**. Dùng lệnh sau trên Ubuntu:

```bash
read -rsp 'Nhap DuckDNS Token: ' DUCK_TOKEN; echo
read -rp 'Dan nguyen Google TXT (google-site-verification=...): ' GOOGLE_TXT

curl -fsSG 'https://www.duckdns.org/update' \
  --data-urlencode 'domains=langkinhhukhong' \
  --data-urlencode "token=$DUCK_TOKEN" \
  --data-urlencode "txt=$GOOGLE_TXT" \
  --data-urlencode 'verbose=true'

unset DUCK_TOKEN GOOGLE_TXT
```

Kết quả mong đợi bắt đầu bằng `OK`. Nếu `KO`, kiểm tra token và tên miền. Không đưa token vào ngoặc kép của câu nhắc `read -rp`, vì như vậy không nhập dữ liệu vào biến.

**Chú ý:** DuckDNS có cách quản lý TXT đơn giản, thao tác cập nhật có thể **ghi đè TXT đang được dùng**. Nếu tên miền đã dùng TXT cho mục đích khác, cần kiểm tra trước khi thay.

#### 8.3. Kiểm tra DNS TXT đã xuất hiện

```bash
dig +short TXT langkinhhukhong.duckdns.org @1.1.1.1
dig +short TXT langkinhhukhong.duckdns.org @8.8.8.8
```

Khi thấy chính xác `google-site-verification=...`, trở lại Search Console và nhấn **VERIFY**. DNS có thể cần thời gian cập nhật.

**Không xóa TXT xác minh sau khi Verify**; Google có thể kiểm tra lại sau này.

#### 8.4. Nộp sitemap sau khi xác minh

Với bộ source hiện hành có sitemap tự động, kiểm tra:

```bash
curl -i https://langkinhhukhong.duckdns.org/robots.txt
curl -i https://langkinhhukhong.duckdns.org/sitemap.xml
```

Trong Search Console → **Sitemaps** → nhập:

```text
sitemap.xml
```

URL đầy đủ: `https://langkinhhukhong.duckdns.org/sitemap.xml`.

Nếu 404, cần xác nhận bạn đã triển khai đúng bản website có endpoint sitemap; Nginx và token DNS không tự tạo sitemap. Sau khi gửi, Google cần thời gian xử lý và **không đảm bảo trang sẽ lập tức lên Top**.

---

### 9. Kiểm tra cuối cùng và lỗi thường gặp

#### 9.1. Bộ lệnh chẩn đoán nhanh

```bash
# Service và cổng
sudo systemctl status lkh-web --no-pager -l
sudo systemctl status nginx --no-pager -l
sudo ss -lntp | grep ':5110' || true

# Backend / Nginx / HTTPS
curl -i http://127.0.0.1:5110/api/health
curl -i -H 'Host: langkinhhukhong.duckdns.org' http://127.0.0.1/api/health
curl -i https://langkinhhukhong.duckdns.org/api/health
curl -sS -o /dev/null -w 'HOME: %{http_code}\n' https://langkinhhukhong.duckdns.org/

# DNS
curl -4fsS https://api.ipify.org; echo
dig +short @1.1.1.1 langkinhhukhong.duckdns.org

# FFmpeg
sudo -u www-data ffmpeg -version | head -n 1
sudo -u www-data ffprobe -version | head -n 1

# Quyền dữ liệu
sudo -u www-data test -w /var/www/lkh-lib/data && echo 'DATA OK'
sudo -u www-data test -w /var/www/lkh-lib/data/dataprotection-keys && echo 'KEYS OK'

# Nhật ký
sudo journalctl -u lkh-web -n 100 --no-pager -o cat
sudo tail -n 60 /var/log/nginx/lkh-error.log
```

#### 9.2. Bảng xử lý sự cố

| Lỗi | Nguyên nhân thường gặp | Kiểm tra / xử lý |
|---|---|---|
| `502 Bad Gateway` | Service chết, sai cổng, không nghe `5110` | `systemctl status lkh-web`, `journalctl`, `curl 127.0.0.1:5110` |
| `e_sqlite3` / `libe_sqlite3.so not found` | Publish sai Windows hoặc thiếu native SQLite Linux | Publish lại `-r linux-x64`, kiểm tra `libe_sqlite3.so` |
| `UnauthorizedAccessException` ở `dataprotection-keys` | `/var/www/lkh-lib` không cho `www-data` đi qua hoặc keys không ghi được | `namei -l`, `chown root:www-data /var/www/lkh-lib`, `chmod 750`, kiểm tra keys |
| `500` trang chủ, health `200`; log có `AntiforgeryOptions.Cookie.SecurePolicy = Always` | App không nhận HTTPS từ Nginx | Kiểm tra `X-Forwarded-Proto`, `UseForwardedHeaders()` chạy sớm |
| `413 Request Entity Too Large` | Nginx giới hạn upload | `client_max_body_size` trong server block HTTPS và `nginx -t` |
| MP3 không upload / không tạo preview | FFmpeg/ffprobe, .NET `InputFile`/Kestrel limit, quyền tmp, R2 | `ffmpeg -version`, log ứng dụng, quyền `/tmp`, R2 config |
| MP3 có trong R2 nhưng không hiển thị Admin | UI chưa refresh sau ghi DB hoặc DB lưu thất bại | Đọc log upload, kiểm tra bảng `episodes`, bản vá tự refresh |
| Chứng chỉ Let's Encrypt thất bại | DNS A/AAAA sai; firewall cổng 80; Nginx vhost sai | `dig`, thử HTTP từ Internet, `sudo nginx -t` |
| Google Verify TXT thất bại | `txt` chưa lan truyền, token sai, bị TXT khác ghi đè | `dig TXT @1.1.1.1`, DuckDNS API `OK` |
| WebSocket không kết nối | Thiếu Upgrade/Connection, proxy timeout | DevTools Network → WS và log Nginx |
| Login báo sai mật khẩu | Tài khoản hoặc hash trong bảng `users` không đúng | Kiểm tra `users` **chỉ đọc**, reset bằng thuật toán ứng dụng, không ghi plaintext vào SQL |

#### 9.3. Mẫu lệnh cập nhật phiên bản mới về sau

**Windows:** Publish vào `publish/lkh-linux` như bước 2.

**VPS:** Upload lên `/var/www/lkh-web-stage`, sau đó:

```bash
sudo test -f /var/www/lkh-web-stage/LangKinhHuKhong.Web.dll \
  || { echo 'CHUA UPLOAD DAY DU'; exit 1; }

sudo systemctl stop lkh-web
sudo mv /var/www/lkh-web "/var/www/lkh-web-backup-$(date +%Y%m%d-%H%M%S)"
sudo mv /var/www/lkh-web-stage /var/www/lkh-web
sudo chown -R root:root /var/www/lkh-web
sudo chmod -R a+rX /var/www/lkh-web
sudo systemctl start lkh-web
sudo systemctl status lkh-web --no-pager -l
curl -i http://127.0.0.1:5110/api/health
```

> Chỉ chạy đoạn `mv` trên máy **đã có bản ứng dụng**, và sau khi upload stage đầy đủ. Không thay thế thư mục dữ liệu `/var/www/lkh-lib`.

#### 9.4. Checklist nghiệm thu

- [ ] `ffmpeg`, `ffprobe` chạy được dưới `www-data`.
- [ ] `dotnet --list-runtimes` có ASP.NET Core 9.
- [ ] Bản publish là `linux-x64`, có thư viện SQLite native.
- [ ] `/var/www/lkh-lib/data` và Data Protection keys được phép ghi.
- [ ] Service `lkh-web` đang `active (running)`.
- [ ] `http://127.0.0.1:5110/api/health` trả 200.
- [ ] DuckDNS trỏ đúng IP public VPS.
- [ ] Nginx site `lkh-web` không gây ảnh hưởng website khác.
- [ ] HTTPS hợp lệ, HTTP chuyển hướng HTTPS.
- [ ] Trang chủ và API qua HTTPS trả 200.
- [ ] WebSocket Blazor hoạt động (kiểm tra DevTools).
- [ ] Admin đăng nhập được; MP3 upload và phát được (bao gồm preview/HLS nếu có).
- [ ] Google Search Console xác minh TXT thành công.
- [ ] `sitemap.xml` hoạt động, đã gửi Search Console.
- [ ] Khóa DuckDNS/R2 không bị lộ; có backup SQLite và Data Protection.

---

## 12. Kiểm tra sau cập nhật và quản trị tài liệu

### 12.1. Kiểm tra ngắn sau mỗi bản Publish

```bash
sudo systemctl status lkh-web --no-pager -l
sudo nginx -t
curl -fsS https://langkinhhukhong.duckdns.org/api/health
curl -sS -o /dev/null -w 'HOME: %{http_code}\n' https://langkinhhukhong.duckdns.org/
curl -fsS https://langkinhhukhong.duckdns.org/robots.txt | head -n 20
curl -fsS https://langkinhhukhong.duckdns.org/sitemap.xml | head -n 20
```

Mở trình duyệt và kiểm thử thực tế: đăng nhập Admin, upload MP3, thay MP3, phát Free/Premium và tự chuyển tập. `GET /api/health = 200` **không đủ** để kết luận trang chủ, upload, R2, HLS và SEO đều chạy bình thường.

### 12.2. Quy tắc duy trì tài liệu hợp nhất

- Giữ **một file Markdown này** làm tài liệu chuẩn; khi có tính năng mới, cập nhật trực tiếp chương liên quan thay vì tạo lại chuỗi hướng dẫn phiên bản lịch sử.
- Đối chiếu code đang chạy trước khi ghi trạng thái “đã triển khai”; ghi chú rõ khi chỉ là thiết kế/chưa kiểm thử.
- Nếu thay cơ chế phát, cập nhật đồng thời phần API, bảo mật, UI, vận hành và checklist; không giữ hướng dẫn trái ngược.
- Nếu thay tên miền, điền lại `LKH_PUBLIC_ORIGIN`, Nginx `server_name`, Certbot, Google Search Console; đừng thay cứng địa chỉ trong từng phần mã nguồn.
- Không đưa file chứa secrets hay bản SQLite thật vào tài liệu công khai; lưu phiên bản đã rà soát trong repository riêng hoặc nội bộ.

### 12.3. Những gì chủ ý không đưa vào file này

- Các file hướng dẫn chỉ dùng để **nâng từ phiên bản lịch sử** hoặc các bản sửa lỗi nguồn đã được tích hợp.
- Cấu hình IIS/Windows VPS và `win-x64` vì môi trường vận hành mục tiêu là Ubuntu/Nginx (`linux-x64`).
- Mô hình Premium tải MP3 full trực tiếp qua `/stream`, vì mô hình hiện tại là HLS bảo vệ theo phiên.
- Hướng dẫn React/Vite/Node.js hoặc giao diện cũ đã được thay bằng Blazor Server.
- Thông tin đăng nhập, token, Access Key/Secret Key hoặc mật khẩu từng sử dụng thực tế.

---

*Tài liệu tổng hợp từ 19 file Markdown trong `docs.zip` và hướng dẫn Publish Ubuntu/DuckDNS hiện hành. Ưu tiên trạng thái chức năng mới nhất tại thời điểm 17/10/2026. Không phải tài liệu tự sinh từ code/kiểm thử runtime.*
