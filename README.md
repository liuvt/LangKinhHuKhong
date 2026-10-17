# V1.9.3 — Premium của tôi & lịch sử Admin cấp/gia hạn

Tài khoản Premium còn hiệu lực chỉ thấy ngày hết hạn và nút gia hạn; Free vẫn thấy bảng giá. Mọi lần Admin cấp/gia hạn Premium được lưu như hồ sơ đơn hàng có mã, lý do, thời gian, người cấp, 0đ và **không tạo thanh toán giả**. Đơn tự mua VietQR được gắn nhãn riêng. Có chuyển đổi lịch sử Admin cấp từ các bản cũ. Chi tiết tại [docs/PREMIUM_ORDER_HISTORY_V1.9.3.md](docs/PREMIUM_ORDER_HISTORY_V1.9.3.md).

> Chưa chạy `dotnet build` trên môi trường đóng gói; hãy build và kiểm thử trên máy Windows trước khi đưa vào sử dụng.

---

# V1.9.2 — Quản lý tài khoản nhận tiền VietQR

Bổ sung **Admin → Tài khoản QR**: nhiều tài khoản ngân hàng, lựa chọn từ danh sách VietQR, thêm/sửa, QR xem trước, đặt tài khoản mặc định, ngừng sử dụng và kích hoạt lại. Các đơn đã tạo giữ **snapshot** người thụ hưởng, kể cả khi thay đổi ngân hàng mặc định. CSDL tự nâng cấp một lần từ tài khoản cũ. Xem [hướng dẫn](docs/BANK_RECEIVING_ACCOUNTS_V1.9.2.md).

> Source cần build và kiểm thử thực tế trên máy Windows có .NET 9 SDK; môi trường đóng gói hiện không có .NET SDK.

---

# V1.9.1 — Sửa lỗi biên dịch Razor (CS1525, CS1026)

Sửa 2 sự kiện `@onclick` gây lỗi ở `Home.razor` (lọc thể loại) và `AdminPanel.razor` (xóa thể loại). Xem `docs/FIX_RAZOR_V1.9.1.md`. Không thay đổi CSDL, nội dung R2, Premium/HLS hay API.

---

> Giao diện trang chủ cập nhật: V1.8.1. Xem `docs/HOME_CLEAN_V1.8.1.md`.

# LĂNG KÍNH HƯ KHÔNG — V1.8 · PREMIUM HLS / FREE MP3

**Điểm mới:** User Free chỉ phát MP3 preview 5 phút (không mã hóa HLS). User Premium/Admin phát audio toàn bộ qua HLS AES-128, xác thực từng playlist/segment/key trên server. `/api/episodes/{id}/stream` giờ **không bao giờ** cung cấp MP3 đầy đủ. Hệ thống tự tạo HLS khi upload hoặc thay MP3; Admin có nút `Tạo HLS` để xử lý tập cũ. Các cấu hình Audio R2, Tailwind Light, SQLite, VietQR và phân quyền giữ lại.

**Quan trọng:** Trước khi thay source, sao lưu `LangKinhHuKhong.Web/data` và `R2-Local.ps1`. Xem [docs/HLS_PREMIUM_ONLY_V1.8.md](docs/HLS_PREMIUM_ONLY_V1.8.md) để hướng dẫn nâng cấp/kiểm thử. Chưa xác nhận build .NET thực tế ở môi trường tạo ZIP; chạy `dotnet build` trên Windows.

---

# LĂNG KÍNH HƯ KHÔNG — Blazor Server / .NET 9.0.1 · Tailwind Light + Cloudflare R2 · V1.7

## V1.7 — Mô tả truyện rút gọn + Player riêng (16/10/2026)

- **Trang chi tiết truyện**: giới hạn nội dung mô tả ở vài dòng bên cạnh ảnh bìa, nhất là trên mobile; nút **Xem chi tiết nội dung** mở toàn bộ giới thiệu ở khu vực riêng phía dưới, nút **Thu gọn** đóng lại. Không thay đổi dữ liệu mô tả trong SQLite.
- **Player**: bỏ thuộc tính native `controls` của thẻ `audio` (không còn menu Download của trình duyệt); bổ sung play/pause, tua lùi/tiến 10 giây, thanh thời gian, tốc độ 0.75–2x và âm lượng ở desktop.
- **Quyền nghe**: Giữ nguyên API `/api/episodes/{episodeId}/stream`, cookie authentication, quyền Free chỉ nhận MP3 Preview 300 giây; Premium/Admin nhận MP3 Full. R2 vẫn là bucket private.
- **Quan trọng**: **ẩn nút Download không thể ngăn tuyệt đối việc người dùng lưu lại âm thanh họ được quyền nghe**. Browser vẫn tải các byte để phát, nên người có kiến thức kỹ thuật có thể lấy lại. Nếu muốn bảo vệ mạnh hơn, cần HLS phân đoạn/mã hóa, watermark và cân nhắc DRM, nhưng cũng không loại bỏ được ghi âm đầu ra. Không được coi UI tùy biến là DRM.
- **Dữ liệu**: Không đổi schema, database, file cũ, cấu hình Cloudflare R2 hay tài khoản Admin.
- **Kiểm tra trong gói**: `node --check` thành công; script mô phỏng player đã kiểm tra Play/Pause, tua, seek, tốc độ và âm lượng; không có .NET SDK ở môi trường tạo ZIP nên **chưa xác nhận `dotnet build` chạy thực tế**.

**Kiểm tra trên Windows:**

```powershell
cd D:\Dev\LangKinhHuKhong
dotnet clean
dotnet build
.\Chay-Local.cmd
```

Mở website trên điện thoại/DevTools ở chiều ngang khoảng 330–390px, truy cập `/truyen/{slug}`. Kiểm tra mô tả cạnh ảnh bìa, nút mở/thu gọn nội dung, phát/tạm dừng, tua, tốc độ, và đảm bảo không có menu Download. Thử cả Free (không được phát vượt 300 giây) và Premium.

**Bản V1.6:** Sửa sidebar không đồng bộ khi điều hướng (`/lich-su` nhưng Trang chủ active), xử lý trang chủ không nạp dữ liệu lần đầu, đồng bộ tiêu đề tab/header và route khi Back/Forward. Xem [docs/NAVIGATION_FIX_V1.6.md](docs/NAVIGATION_FIX_V1.6.md). Giữ nguyên tài khoản/Admin, R2, Free/Premium và VietQR của V1.5.

Phiên bản **chuyển đổi React + ASP.NET Core 8 thành một web app Blazor Interactive Server trên .NET 9**. Không cần cài Node.js, Vite hay chạy hai cổng.

> TRẠNG THÁI: Source local được chuyển đổi; chưa thể thực hiện `dotnet restore/build` trong môi trường tạo gói do không có SDK .NET và không truy cập NuGet. Cần build trên Windows để xác nhận; không coi là bản phát hành production.


## V1.6.1 — Sửa Razor RZ9986 (16/10/2026)

- `Components/Pages/Home.razor`: thay thuộc tính `class="nav-item @(page == ... )"` của **component** `NavLink` bằng một biểu thức Razor duy nhất.
- Giữ trạng thái active của mục **Thư viện truyện** khi mở `/truyen/{slug}`; `NavLink` vẫn tự xử lý active tại `/thu-vien`.
- Không thay đổi schema SQLite, dữ liệu người dùng, R2 hoặc giao diện Tailwind.
- Chưa xác nhận build .NET 9 trong môi trường tạo ZIP vì chưa có SDK .NET. Cần `dotnet build` trên Windows.

## 1. Phiên bản cụ thể

- **Target framework:** `net9.0`.
- **UI:** Blazor Web App `InteractiveServer` (mô hình Blazor Server tương đương trong .NET 9).
- **NuGet SQLite:** `Microsoft.Data.Sqlite` **9.0.1** trong file `.csproj`.
- **SDK:** yêu cầu SDK .NET 9 (`global.json` định hướng 9.0.100/latestFeature). Số **9.0.1** là phiên bản bản vá Runtime/NuGet, không phải tên Target Framework hay SDK. .NET 9 khi triển khai sẽ dùng phiên bản patch 9.0.x mới nhất sẵn có.
- **Database:** SQLite, tự tạo lần đầu.
- **FFmpeg/ffprobe:** tạo preview 300 giây cho MP3.

**Bảo mật vòng đời:** Microsoft công bố **.NET 9 kết thúc hỗ trợ 10/11/2026**. Để deploy lên Internet lâu dài, nên nâng cấp sang **.NET 10 LTS**; giữ .NET 9 tại đây theo yêu cầu của bạn.

## 2. Cài đặt Windows

1. Tải và cài [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (SDK, không chỉ Hosting Bundle).
2. Cài [FFmpeg](https://ffmpeg.org/download.html), đảm bảo CMD chạy được `ffmpeg -version` và `ffprobe -version`.
3. Giải nén toàn bộ thư mục ZIP, ví dụ vào `D:\Dev\LangKinhHuKhong-Blazor`.
4. Chạy file **`Chay-Local.cmd`** hoặc mở PowerShell chạy `./Chay-Local.ps1`.
5. Khi chạy Local lần đầu, tài khoản `admin` được tạo tự động nếu SQLite chưa có Admin. Dùng mật khẩu khởi tạo đã cung cấp khi yêu cầu thay đổi V1.5. Không còn hộp thoại hỏi Admin.
6. Truy cập **http://127.0.0.1:5110**.

Các lệnh chạy thủ công:

```powershell
cd .\LangKinhHuKhong.Web
dotnet restore
dotnet build
dotnet run --urls http://127.0.0.1:5110
```

**V1.5 — Tài khoản Admin Development:** khi chạy bằng `Chay-Local.cmd` hoặc `dotnet run` với `ASPNETCORE_ENVIRONMENT=Development`, ứng dụng sẽ tự tạo `admin` (nếu chưa có tài khoản Admin). Password khởi tạo chỉ được đóng gói dưới dạng ASP.NET Identity PBKDF2 hash; không lưu plaintext trong source. Bạn có thể tùy chỉnh bằng cặp biến môi trường `LKH_ADMIN_USERNAME` và `LKH_ADMIN_PASSWORD` trước lần khởi tạo. Sau đăng nhập, vào **Hồ sơ cá nhân → Đổi mật khẩu**. Chức năng tự seed **không chạy trong Production**; nhưng nếu đưa cơ sở dữ liệu đã seed từ Local lên Production, PHẢI đổi mật khẩu mặc định trước khi công khai.

### Nếu `global.json` báo không tìm thấy SDK

Chạy `dotnet --list-sdks`, kiểm tra đã cài .NET 9 SDK. Đây là SDK 9.0.1xx hoặc tương thích 9.0.100; không sử dụng SDK 8. Tùy cấu hình phát triển có thể thay đổi `rollForward` hoặc xóa `global.json` nếu chỉ dùng SDK 9 mới hơn.

## 3. Luồng kiểm thử

1. Mở website, đăng nhập Admin đã tạo.
2. Vào **Quản trị hệ thống → Kho truyện**. Tạo truyện với status `Published` và slug không dấu.
3. Chọn **Tập MP3**, nhập tên, số tập, chọn MP3; nhấn upload. FFmpeg sẽ tạo bản preview 300 giây tự động.
4. Mở trình duyệt ẩn danh, đăng ký User, nghe tập đầu: stream API chỉ đọc bản preview.
5. Admin → **Cấu hình gói**, nhập BIN ngân hàng, số tài khoản, tên ngân hàng, tên chủ tài khoản (xác minh thủ công).
6. User → **Gói Premium**, tạo đơn. VietQR chứa số tiền, mã đơn riêng; nhấn **Tôi đã chuyển khoản** chỉ chuyển trạng thái `Reviewing`.
7. Sau khi đối chiếu **tiền thực nhận**, Admin → **Thanh toán**, nhập **mã giao dịch ngân hàng thực tế** và duyệt. Đơn chuyển sang Paid, tạo Payment và Subscription trong cùng giao dịch SQLite.
8. User tải lại trình duyệt, nghe trọn tập. Gia hạn trước hạn cộng từ ngày hết hạn; hết hạn tự trở về Free.
9. Admin thử khóa User: request API kế tiếp của User đó bị từ chối.

Tạo file kiểm tra 6 phút 5 giây: chạy `tools\Tao-MP3-Test.cmd` (yêu cầu ffmpeg).

## 4. Cấu trúc solution

```
LangKinhHuKhong-BlazorServer-9.0.1/
├── LangKinhHuKhong.sln
├── global.json
├── Chay-Local.cmd
├── Chay-Local.ps1
├── README.md
├── LangKinhHuKhong.Web/
│   ├── LangKinhHuKhong.Web.csproj
│   ├── Program.cs              # Blazor InteractiveServer + toàn bộ protected APIs
│   ├── Database.cs             # SQLite / ASP.NET Core PasswordHasher
│   ├── schema.sql
│   ├── Components/
│   │   ├── App.razor           # HTML root / stylesheet / Blazor boot
│   │   ├── Routes.razor
│   │   ├── _Imports.razor
│   │   ├── Pages/Home.razor    # Home, Library, Player, Profile, Premium, QR
│   │   ├── Pages/AdminPanel.razor
│   │   └── Shared/{BrowserApi.cs,Ui.cs}
│   ├── wwwroot/css/site.css
│   ├── wwwroot/js/site.js     # Chỉ API fetch, upload, gắn audio listeners
│   └── data/                  # SQLite và private audio; tự tạo, KHÔNG commit
└── tools/Tao-MP3-Test.cmd
```

**Khác biệt so với V1 React:** Không còn `client/`, `npm install`, `npm run dev`, Vite proxy. Một server phục vụ Blazor UI, API, cookie session, audio và DB.

## 5. Bảo mật & kinh doanh (giữ nguyên)

- Username/password, ASP.NET Core `PasswordHasher` (PBKDF2), cookie `HttpOnly` + `SameSite=Strict`, CSRF cho mọi lệnh ghi API.
- Rate-limit login/đăng ký; role `User/Admin` tách riêng khỏi Free/Premium.
- Backend xác nhận lại trạng thái tài khoản và Role ở mỗi API request; Locked/Deleted bị chặn.
- Stream audio riêng `/api/episodes/{episodeId}/stream` kiểm tra quyền mỗi request. Audio full/preview nằm trong `data/audio-private/` **ngoài wwwroot**. `Range` được hỗ trợ.
- Gói Free nghe bản preview 300 giây, không phải file full bị dừng bởi trình phát. Premium/Admin phát bản full.
- Mua 1 tháng có giá mặc định 100.000đ, Admin được chỉnh giá. Đơn đã tạo giữ snapshot giá/tài khoản nhận tiền.
- Gia hạn nối tiếp ngày hết hạn cũ (nếu còn hạn), hết hạn tự về Free dựa trên timestamp UTC.
- VietQR sinh QR chuyển khoản, **không** xác nhận đã nhận tiền. Admin duyệt thủ công với mã ngân hàng unique, trong transaction.
- Admin cấp thêm tháng Premium có lý do; ghi Audit log, không tạo payment giả.

## 6. Lưu ý trước khi deploy

- Chưa kiểm chứng biên dịch/tích hợp đầu cuối bởi thiếu .NET SDK trong môi trường tạo source.
- Blazor Interactive Server cần WebSockets/SignalR ổn định; IIS/Plesk phải hỗ trợ ASP.NET Core Hosting Bundle, WebSockets và app luôn chạy.
- SQLite đủ cho MVP local, chuyển SQL Server và cấu hình đồng thời trước khi scale.
- Upload FFmpeg xử lý đồng bộ nên giới hạn 250MB và 5 phút xử lý; production nên có queue và kiểm tra nguồn file kỹ hơn.
- Cookie tại localhost cho phép HTTP để dev (`SameAsRequest`). Khi deploy bắt buộc HTTPS, `SecurePolicy.Always`, HTTPS redirect, HSTS, forward headers chính xác.
- Chưa có đối soát tự động, email reset password, MFA Admin, hệ thống hoàn tiền, bảo vệ nội dung trước ghi lại âm thanh phía người nghe.
- Cần chạy kiểm thử bảo mật, hiệu năng SignalR và chính sách bản quyền audio trước khi mở đăng ký công khai.

## 7. Di chuyển dữ liệu từ V1 React trước đó (nếu có)

Database và file audio vẫn tương thích với schema hiện tại. Sau khi **tắt ứng dụng cũ** và sao lưu toàn bộ thư mục, sao chép:

```
V1-old/server/data/langkinhhukhong.db  -> Blazor/LangKinhHuKhong.Web/data/langkinhhukhong.db
V1-old/server/data/audio-private/*   -> Blazor/LangKinhHuKhong.Web/data/audio-private/*
```

Nếu SQLite đang dùng WAL, sao lưu nhất quán sau khi đóng mọi kết nối và kiểm tra thêm các file `-wal`/`-shm` còn tồn tại; tốt nhất dùng SQLite backup API trước khi copy. Không chép database đang được ghi và không công khai thư mục này dưới `wwwroot`.


## Bản vá 2026-10-16 – sửa lỗi CS0117 khi build .NET 9

Đã thay `Results.PhysicalFile(...)` không tồn tại trong Minimal API bằng `Results.File(path: absolute, contentType: "audio/mpeg", enableRangeProcessing: true)` tại `LangKinhHuKhong.Web/Program.cs` để giữ hỗ trợ HTTP Range khi phát MP3.
Sau khi giải nén chạy `dotnet build`. Chưa xác minh được việc biên dịch trong môi trường tạo file vì không có .NET SDK.


## 8. Cập nhật giao diện V1.1 — Tailwind CSS 4 Light Theme

- Giao diện mới: nền sáng trắng/xám #f7f9fd, điểm nhấn tím lavender #6557dc, trang Premium sắc vàng nhạt.
- Sidebar desktop, menu dạng drawer cho điện thoại, thanh tìm kiếm, hero minh họa CSS, thư viện dạng lưới bìa, player cố định, biểu mẫu, modal, bảng Admin Dashboard.
- **Tailwind CSS v4.1.10**: utility classes được biên dịch thành `LangKinhHuKhong.Web/wwwroot/css/tailwind.generated.css`. `css/site.css` là semantic component stylesheet để thống nhất thiết kế Razor; cả hai đều được self-hosted, **không dùng Tailwind CDN**.
- Chạy website local vẫn chỉ cần .NET 9 SDK và FFmpeg cho upload audio. **Không cần cài Node.js/npm** để xem giao diện vì CSS đã có sẵn trong ZIP.
- Muốn chỉnh Tailwind và biên dịch lại CSS trong tương lai: cài Node.js 20+, mở PowerShell tại thư mục root, chạy:

  ```powershell
  npm install
  npm run css:build
  dotnet build
  ```

- Source Tailwind: `styles/tailwind.input.css` và `tools/build-tailwind.mjs`. Chỉnh giao diện chung tại `LangKinhHuKhong.Web/wwwroot/css/site.css`, nội dung màn hình tại `Components/Pages/Home.razor`, `Components/Pages/AdminPanel.razor`, icon tại `Components/Shared/AppIcon.razor`.
- `docs/UI_PREVIEW.html` là bản xem trước tĩnh với **dữ liệu minh họa**, không truy vấn database, không phải website đang chạy. Mở file HTML để xem bố cục trước khi nhập truyện.
- Luồng login, cookie/CSRF, quản lý người dùng, upload MP3, nghe Free 300 giây, Premium và VietQR **giữ nguyên** từ bản vá CS0117.
- Sau khi thay source: tắt app cũ, sao lưu thư mục `data/` (nếu đã có), giải nén ZIP mới, giữ lại database và MP3 private bằng cách chép `data/` trở lại đúng vị trí. Không xóa hay đưa `data/` vào `wwwroot`.
- Kiểm thử giao diện tại `/`, `/thu-vien`, `/truyen/{slug}`, `/lich-su`, `/tai-khoan`, `/premium`, `/quan-tri` với quyền User và Admin; thu nhỏ màn hình kiểm tra menu mobile.
- Lưu ý: bản giao diện đã được kiểm tra tĩnh (CSS/JS và đường dẫn), nhưng chưa chạy `dotnet build` hoặc kiểm thử Blazor thực tế vì môi trường đóng gói không có .NET SDK. Nếu có lỗi Razor, gửi log `dotnet build` để tiếp tục sửa.

## 9. Cloudflare R2 — Audio Storage (V1.2)

Đã bổ sung **Cloudflare R2 private bucket** cho toàn bộ audio upload mới: `full.mp3` + `preview.mp3`. Phát audio qua ASP.NET Core kiểm tra Free/Premium, hỗ trợ HTTP Range 206. Các file Local phiên bản cũ vẫn phát được. Admin có nút **Lên R2** để chuyển tập cũ lên Cloudflare (giữ lại local làm bản sao).

- Tạo bucket **Standard** riêng tư trên Cloudflare.
- Tạo token R2 **Object Read & Write** chỉ áp dụng cho bucket.
- Sao chép `R2-Local.example.ps1` thành `R2-Local.ps1`, điền `R2_ACCOUNT_ID`, `R2_BUCKET_NAME`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`.
- Chạy `Chay-Local.cmd`. Script tự đọc `R2-Local.ps1` trước khi mở Blazor.
- Không cần npm/Vite; giữ nguyên giao diện Tailwind Light.
- NuGet mới: `AWSSDK.S3` v3.7.202. Lần build đầu cần Internet để restore package.

**Hướng dẫn chi tiết và lưu ý về bảo mật/chi phí:** [`docs/CLOUDFLARE_R2.md`](docs/CLOUDFLARE_R2.md).

> Nếu chưa có key Cloudflare, ứng dụng vẫn chạy Local (mặc định); chỉ chuyển sang R2 khi đặt `LKH_AUDIO_STORAGE=R2`. Sau khi đã có tập trên R2, không nên tắt R2 khi chưa khôi phục file về Local.


## 10. Cloudflare R2 theo thư mục bộ truyện (V1.3)

- Mỗi truyện có một `storage_prefix` riêng, tạo từ slug lúc tạo truyện và ID bất biến. Đổi tên/slug **không đổi prefix**, tránh đứt liên kết MP3.
- Vào Admin → Kho truyện → chọn truyện. Upload ảnh bìa vào `Image/`; MP3 được lưu vào `Episodes/`, bản nghe thử vào `Previews/`.
- R2 sử dụng **prefix ảo** chứ không có thư mục thực. Các thư mục xuất hiện trong R2 Dashboard khi có file.
- Tập mới tự động theo layout V1.3. Với tập cũ Local hoặc R2 V1.2, dùng nút **Vào thư mục truyện** để copy lên cấu trúc mới (giữ bản cũ làm backup).
- Hệ thống tự `ALTER TABLE` bổ sung `storage_prefix` và `cover_file` cho database V1.2. Sao lưu `data/` trước lần chạy đầu.
- Ảnh bìa `JPG/PNG/WebP` tối đa 8MB, lưu private tại R2 và công khai có điều kiện qua `/api/stories/{id}/cover` (chỉ Published, Admin xem Draft/Hidden).
- Endpoint `/api/episodes/{id}/stream` tiếp tục kiểm tra Premium/Free và giữ hỗ trợ HTTP Range.
- Cần chạy `dotnet build` và kiểm thử R2 thực tế trên Windows trước khi đưa lên Internet.

Xem `docs/R2_STORY_FOLDERS.md` để biết cấu trúc và hướng dẫn migrate.


## 10. Quản lý truyện và file MP3 — V1.4

- Luồng Admin 3 bước: **Tạo thông tin truyện → Upload ảnh bìa → Upload các tập MP3**.
- Bảng tập thêm chức năng **sửa tên/số tập/trạng thái**, **thay file MP3** (tạo lại preview 300 giây), và **xóa tập** có xác nhận.
- Thay ảnh bìa: lưu ảnh mới, cập nhật tham chiếu trong SQLite rồi xóa ảnh cũ ở Cloudflare R2 hoặc Local.
- Thay MP3 hoặc xóa tập: xóa file cũ Full/Preview sau khi commit; xóa tập còn dọn tiến độ nghe.
- Bảng SQLite `storage_cleanup` giữ lại yêu cầu xóa file để retry khi Cloudflare R2 lỗi. Nút **Dọn file cũ** trong Admin hiển thị số file còn chờ.
- **Quan trọng:** xóa đường dẫn ảnh khỏi SQLite không giải phóng dung lượng R2. Vì vậy chức năng này xóa object cũ thực tế trên R2; thao tác xóa được kiểm tra để không xóa object còn tham chiếu.
- Hướng dẫn: [`docs/ADMIN_STORY_MANAGEMENT_V1.4.md`](docs/ADMIN_STORY_MANAGEMENT_V1.4.md).


## 11. V1.5 — Admin tự tạo sẵn (Development only)

- Admin đầu tiên: username `admin`; mật khẩu do chủ dự án chỉ định và lưu dưới dạng hash có salt, không xuất hiện trong mã nguồn plaintext.
- Hệ thống tự khởi tạo khi `ASPNETCORE_ENVIRONMENT=Development` **và** chưa có bất kỳ Admin nào.
- Khởi động lại **không đặt lại mật khẩu**, không tạo trùng Admin, không nâng quyền User trùng tên.
- Nếu SQLite đã có Admin trước đó, tài khoản/mật khẩu cũ vẫn giữ nguyên.
- Đổi mật khẩu trong **Hồ sơ cá nhân**, sau đó đăng nhập lại.
- Đừng mở cổng local ra Internet khi vẫn sử dụng mật khẩu khởi tạo.
- Seed bị vô hiệu hóa trong môi trường Production.

## Nâng cấp V1.9 — Thể loại / đánh giá / thống kê

- Một bộ truyện có nhiều thể loại qua bảng `genres`, `story_genres`.
- Tên truyện tự tạo slug tiếng Việt không dấu (chỉ lần khởi tạo); giữ slug khi sửa để tránh hỏng liên kết.
- Thành viên đăng nhập chấm 1–5 sao, sửa/xóa một nhận xét trên mỗi truyện; Admin ẩn/hiện đánh giá.
- Trang chủ có **Truyện mới** theo ngày tạo và **Truyện xem nhiều nhất** dựa trên tài khoản nghe tích lũy thực tế **> 300 giây**, không dựa vào seek.
- Xem hướng dẫn `docs/CATEGORIES_REVIEWS_TRENDS_V1.9.md` và thử `python tests/v19_sqlite_smoke.py`.
- Trước khi nâng cấp, sao lưu `LangKinhHuKhong.Web/data/` và thông tin R2. Không cần xóa SQLite.


## V1.9.4 — VPS / IIS Production deployment kit

- Mở `docs/DEPLOY_WINDOWS_IIS.md` để cấu hình VPS Windows/IIS.
- Chạy `tools/Publish-VPS-IIS.ps1` trên Windows máy dev để tạo thư mục publish.
- `LKH_DATA_ROOT` giúp đưa SQLite và Data Protection keys ra khỏi thư mục site và giữ dữ liệu khi cập nhật.
- Production bật cookie HTTPS-only và cho phép khởi tạo Admin **tạm thời bằng biến môi trường riêng**, tuyệt đối không dùng mật khẩu Development trên VPS.
- Không thay đổi logic Premium/VietQR/R2. Chưa build được trong môi trường tạo gói, phải test `dotnet build` ở máy người dùng.
