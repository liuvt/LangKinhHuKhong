# HƯỚNG DẪN PUBLISH LĂNG KÍNH HƯ KHÔNG LÊN UBUNTU VPS

**Blazor Server .NET 9 · SQLite · Cloudflare R2 · FFmpeg · Nginx/WebSocket · DuckDNS · HTTPS · Google Search Console**

> **Phạm vi:** triển khai website Lăng Kính Hư Không lên Ubuntu VPS 22.04 (Linux x86-64), theo cấu trúc thực tế của dự án. Hướng dẫn áp dụng khi dựng VPS mới hoặc cập nhật bản Publish đã có. Đọc các lưu ý **không xóa dữ liệu** trước khi chạy lệnh.
>
> **Mẫu cấu hình:** `langkinhhukhong.duckdns.org` → `160.191.175.132` → Nginx (80/443) → Blazor Server (`127.0.0.1:5110`). IP này là IP đã được xác nhận trong đợt triển khai trước; **nếu chuyển VPS thì thay bằng IP mới**.

## Mục lục

1. [Cài FFmpeg và các gói cần thiết](#1-cài-ffmpeg-và-các-gói-cần-thiết)
2. [Publish project .NET 9 cho Linux](#2-publish-project-net-9-cho-linux)
3. [Upload lên `/var/www/lkh-web` và phân quyền `/var/www/lkh-lib`](#3-upload-lên-varwwwlkh-web-và-phân-quyền-varwwwlkh-lib)
4. [Tạo systemd service chạy Blazor](#4-tạo-systemd-service-chạy-blazor)
5. [Tạo tên miền miễn phí DuckDNS](#5-tạo-tên-miền-miễn-phí-duckdns)
6. [Nginx reverse proxy + WebSocket](#6-nginx-reverse-proxy--websocket)
7. [Cài HTTPS miễn phí Let's Encrypt](#7-cài-https-miễn-phí-lets-encrypt)
8. [Kết nối Google Search Console bằng TXT Token DuckDNS](#8-kết-nối-google-search-console-bằng-txt-token-duckdns)
9. [Kiểm tra cuối cùng và lỗi thường gặp](#9-kiểm-tra-cuối-cùng-và-lỗi-thường-gặp)

---

## Thông số hệ thống

| Hạng mục | Giá trị |
|---|---|
| Hệ điều hành | Ubuntu Server 22.04 (x86_64) |
| .NET | ASP.NET Core Runtime 9.x (SDK 9 trên máy build) |
| Tên miền | `langkinhhukhong.duckdns.org` |
| IP ví dụ từ VPS trước | `160.191.175.132` |
| Cổng Kestrel | `127.0.0.1:5110` (nội bộ, **không** mở công khai) |
| Web root / bản Publish | `/var/www/lkh-web` |
| Dữ liệu bền vững | `/var/www/lkh-lib/data` |
| Data Protection keys | `/var/www/lkh-lib/keys` |
| Nhật ký ứng dụng (nếu dùng) | `/var/www/lkh-lib/logs` |
| SQLite | `/var/www/lkh-lib/data/langkinhhukhong.db` |
| Service systemd | `lkh-web.service` |
| Biến cấu hình bí mật | `/etc/langkinhhukhong/lkh.env` |
| Tên cấu hình Nginx | `/etc/nginx/sites-available/lkh-web` |

**Quy tắc bảo vệ dữ liệu:**

- **Không xóa** `/var/www/lkh-lib/data`, `/var/www/lkh-lib/keys` hoặc file `/etc/langkinhhukhong/lkh.env` khi cập nhật web.
- **Không xóa** thủ công các file SQLite `*.db-wal`/`*.db-shm` khi dịch vụ đang chạy.
- Không đưa token DuckDNS, khóa R2, mật khẩu Admin vào GitHub, tệp hướng dẫn, ảnh chụp màn hình hoặc cuộc trò chuyện. Nếu đã lộ, thu hồi/đổi khóa.
- Trên VPS có **nhiều website đang sử dụng Nginx**, chỉ tạo cấu hình site `lkh-web`; **không xóa `sites-enabled` của website khác**.
- Các lệnh bên dưới giả định tài khoản SSH có quyền `sudo`. Nếu đăng nhập bằng `root`, có thể giữ nguyên `sudo`.

---

## 1. Cài FFmpeg và các gói cần thiết

### 1.1. Kiểm tra kiến trúc và hệ điều hành

```bash
uname -m
lsb_release -a
```

Kết quả mong muốn: `x86_64`, Ubuntu 22.04 (hoặc Ubuntu tương thích).

### 1.2. Cập nhật package và cài công cụ

```bash
sudo apt update
sudo apt install -y ffmpeg nginx curl ca-certificates dnsutils sqlite3 rsync
```

Để tránh xung đột PATH và quyền thực thi, dùng gói **FFmpeg từ APT** thay vì Snap trong trường hợp này.

### 1.3. Xác nhận FFmpeg/FFprobe chạy được với `www-data`

```bash
ffmpeg -version | head -n 1
ffprobe -version | head -n 1
sudo -u www-data ffmpeg -version | head -n 1
sudo -u www-data ffprobe -version | head -n 1
command -v ffmpeg
command -v ffprobe
```

Nếu `sudo -u www-data` vẫn hiện phiên bản, tài khoản chạy dịch vụ có thể gọi FFmpeg. Một số chức năng upload còn cần quyền ghi vào thư mục tạm và quyền truy cập Cloudflare R2.

### 1.4. Kiểm tra ASP.NET Core Runtime 9

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

## 2. Publish project .NET 9 cho Linux

> Các lệnh này chạy trên **Windows PowerShell** ở máy phát triển, **không phải** SSH Ubuntu.

### 2.1. Mở thư mục project

```powershell
Set-Location 'D:\Dev\LangKinhHuKhong'
dotnet --version
```

Cần .NET SDK 9 phù hợp source. Nếu source giải nén nơi khác, thay đường dẫn.

### 2.2. Xóa thư mục output cũ và Publish Linux x64

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

### 2.3. Kiểm tra file publish

```powershell
Test-Path '.\publish\lkh-linux\LangKinhHuKhong.Web.dll'
Get-ChildItem '.\publish\lkh-linux' -Recurse -Filter 'libe_sqlite3.so'
Get-ChildItem '.\publish\lkh-linux' | Select-Object Name
```

Phải có `LangKinhHuKhong.Web.dll`, `.deps.json`, `.runtimeconfig.json`, `wwwroot/` và thư viện Linux SQLite `libe_sqlite3.so` (trong thư mục gốc hoặc vị trí native asset được Publish cung cấp). Nếu thư viện không xuất hiện, kiểm tra dependency SQLitePCLRaw trong `.csproj` **trước khi upload**.

### 2.4. Chú ý cấu hình HTTPS Forwarded Headers trong `Program.cs`

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

## 3. Upload lên `/var/www/lkh-web` và phân quyền `/var/www/lkh-lib`

### 3.1. Tạo thư mục tách biệt ứng dụng và dữ liệu

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

### 3.2. Khóa Data Protection

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

### 3.3. Upload file bằng WinSCP/SFTP

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

### 3.4. Quyền thư mục

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

### 3.5. Sao lưu SQLite đúng cách (khi đã có dữ liệu)

Trước khi nâng cấp schema hoặc bản Publish:

```bash
sudo mkdir -p /var/backups/lkh
sudo sqlite3 /var/www/lkh-lib/data/langkinhhukhong.db \
  ".backup '/var/backups/lkh/langkinhhukhong-$(date +%Y%m%d-%H%M%S).db'"
```

Lệnh `.backup` dùng SQLite Backup API để tạo bản sao nhất quán ngay cả khi database đang dùng WAL. Sao lưu **cả thư mục `/var/www/lkh-lib/keys`** theo quy trình backup bảo mật của bạn, không công khai các khóa đó.

---

## 4. Tạo systemd service chạy Blazor

### 4.1. Tạo file cấu hình bí mật (Cloudflare R2)

```bash
sudo install -d -o root -g root -m 700 /etc/langkinhhukhong
sudo nano /etc/langkinhhukhong/lkh.env
```

Mẫu nội dung (thay các giá trị ví dụ bằng **khóa R2 thực tế**, không sao chép nguyên dấu `<...>`):

```ini
LKH_AUDIO_STORAGE=R2
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

### 4.2. Tạo service

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

### 4.3. Khởi động và tự chạy khi boot

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

## 5. Tạo tên miền miễn phí DuckDNS

### 5.1. Đăng ký tên miền

1. Truy cập <https://www.duckdns.org/> và đăng nhập.
2. Tại ô *sub domain*, nhập `langkinhhukhong` (chỉ phần đầu, không nhập `.duckdns.org`).
3. Nhấn **add domain**. Tên miền đầy đủ là `langkinhhukhong.duckdns.org`.
4. Tại ô **current ip**, nhập **IP public của VPS** rồi nhấn **update ip**.

Lấy IP thực tế từ VPS:

```bash
curl -4fsS https://api.ipify.org; echo
```

Ví dụ trong đợt triển khai trước: `160.191.175.132`. **Đừng lấy IP mạng gia đình** nếu server chạy trên VPS.

### 5.2. Kiểm tra DNS đã khớp VPS

```bash
dig +short @1.1.1.1 langkinhhukhong.duckdns.org A
dig +short @8.8.8.8 langkinhhukhong.duckdns.org A
dig +short langkinhhukhong.duckdns.org AAAA
```

Nếu đã thêm AAAA/IPv6 thì địa chỉ đó cũng phải trỏ đúng server có hỗ trợ IPv6; nếu không dùng IPv6, tránh giữ bản ghi AAAA sai.

### 5.3. Cập nhật IP qua DuckDNS API (tùy chọn)

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

## 6. Nginx reverse proxy + WebSocket

> Nếu VPS có các site như `n8n`, `chamcong...`, `checker...`, **không xóa** hoặc thay cấu hình của những site đó. Tạo site riêng `lkh-web`.

### 6.1. Cài và khởi động Nginx

```bash
sudo apt install -y nginx
sudo systemctl enable --now nginx
sudo systemctl status nginx --no-pager
```

### 6.2. Mở firewall (nếu cần)

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

### 6.3. Cấu hình WebSocket dùng chung cho site LKHK

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

### 6.4. Tạo server block riêng

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

### 6.5. Kích hoạt cấu hình và kiểm tra

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

## 7. Cài HTTPS miễn phí Let's Encrypt

### 7.1. Điều kiện trước khi cấp SSL

- DNS A `langkinhhukhong.duckdns.org` đã trỏ đúng IP VPS.
- Nginx `sudo nginx -t` trả `syntax is ok` / `test is successful`.
- Cổng TCP **80 và 443** mở tại firewall công khai.
- Có thể truy cập HTTP qua tên miền từ **mạng bên ngoài VPS** (không chỉ curl 127.0.0.1).

### 7.2. Cài Certbot và sao lưu Nginx

```bash
sudo apt update
sudo apt install -y certbot python3-certbot-nginx

sudo cp /etc/nginx/sites-available/lkh-web \
  "/etc/nginx/sites-available/lkh-web.bak-$(date +%Y%m%d-%H%M%S)"
```

Không cần cài Certbot qua Snap nếu đã chọn gói APT và đang sử dụng tốt; tránh trộn hai cách cài cùng lúc.

### 7.3. Cấp và tự cấu hình HTTPS

```bash
sudo certbot --nginx \
  -d langkinhhukhong.duckdns.org \
  --redirect
```

Certbot sẽ xác thực bằng HTTP-01 qua cổng 80, bổ sung cấu hình chứng chỉ và chuyển hướng HTTP → HTTPS cho **server block khớp tên miền**.

### 7.4. Kiểm tra chứng chỉ và tự gia hạn

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

### 7.5. Test WebSocket thật trong trình duyệt

1. Mở `https://langkinhhukhong.duckdns.org`.
2. Chrome → **F12 → Network → WS**.
3. Tải lại trang, vào chức năng tương tác Blazor.
4. Kết nối `/_blazor` khi dùng WebSocket thường có status **101 Switching Protocols**.

Nếu `400`, `502`, hoặc ngắt kết nối, kiểm tra `Upgrade`, `Connection`, `proxy_read_timeout` và log ứng dụng. Một số trường hợp SignalR dùng cơ chế transport dự phòng; status 101 phụ thuộc kết nối WebSocket thực tế.

---

## 8. Kết nối Google Search Console bằng TXT Token DuckDNS

> **Phân biệt:** Token DuckDNS là bí mật để gọi API DNS. Giá trị `google-site-verification=...` là mã xác minh do Google Search Console cung cấp. Hai giá trị **khác nhau**. Google không cần biết token DuckDNS.

### 8.1. Tạo Property trong Google Search Console

1. Truy cập <https://search.google.com/search-console/> và đăng nhập Google.
2. Chọn **Add property**.
3. Chọn **Domain** và nhập `langkinhhukhong.duckdns.org` (không thêm `https://`); hoặc chọn **URL prefix** và nhập `https://langkinhhukhong.duckdns.org/` nếu muốn phương thức xác minh HTML thay thế.
4. Với **Domain**, Google sẽ cung cấp bản ghi TXT, có dạng:

```text
google-site-verification=MA_XAC_MINH_DO_GOOGLE_CAP
```

Không nhập chuỗi mẫu này; copy nguyên văn mã của property bạn vừa tạo.

### 8.2. Đặt TXT qua DuckDNS API từ VPS

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

### 8.3. Kiểm tra DNS TXT đã xuất hiện

```bash
dig +short TXT langkinhhukhong.duckdns.org @1.1.1.1
dig +short TXT langkinhhukhong.duckdns.org @8.8.8.8
```

Khi thấy chính xác `google-site-verification=...`, trở lại Search Console và nhấn **VERIFY**. DNS có thể cần thời gian cập nhật.

**Không xóa TXT xác minh sau khi Verify**; Google có thể kiểm tra lại sau này.

### 8.4. Nộp sitemap sau khi xác minh

Nếu đang sử dụng bản source có sitemap tự động (SEO V1.9.4.4 trở lên), kiểm tra:

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

## 9. Kiểm tra cuối cùng và lỗi thường gặp

### 9.1. Bộ lệnh chẩn đoán nhanh

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

### 9.2. Bảng xử lý sự cố

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

### 9.3. Mẫu lệnh cập nhật phiên bản mới về sau

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

### 9.4. Checklist nghiệm thu

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

## Tài liệu chính thức tham khảo

- [Microsoft — Host ASP.NET Core on Linux with Nginx (.NET 9)](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-9.0)
- [Microsoft — Proxy và Forwarded Headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)
- [DuckDNS — HTTP API, TXT Record API](https://www.duckdns.org/spec.jsp)
- [Ubuntu — Obtain TLS certificates](https://ubuntu.com/server/docs/how-to/security/obtain-tls-certificates/)
- [Google Search Console — Verify ownership](https://support.google.com/webmasters/answer/9008080)
- [Google Search Central — Tạo và gửi sitemap](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap?hl=vi)

---

*Hướng dẫn nội bộ triển khai Lăng Kính Hư Không · cập nhật 17/10/2026 · Ubuntu VPS + DuckDNS.*
