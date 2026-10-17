# Lăng Kính Hư Không — Hướng dẫn Backup, Publish, Deploy và Rollback trên Ubuntu VPS

> **Áp dụng cho hệ thống hiện tại:** ASP.NET Core Blazor Server .NET 9, Nginx + HTTPS + WebSocket, tên miền `langkinhhukhong.duckdns.org`, Cloudflare R2 và SQLite.  
> **Mục tiêu:** Backup tự động trước mỗi lần cập nhật; upload bản Linux vào thư mục tạm; triển khai bằng một lệnh; tự quay về bản cũ nếu ứng dụng không chạy; phục hồi thủ công theo bản sao lưu khi cần.  
> **Lưu ý:** Thực hiện trên VPS qua SSH với tài khoản có quyền `sudo`. Script minh họa dành riêng cho các đường dẫn đã thống nhất của dự án; không áp dụng nguyên xi cho website khác.

## 0. Hiểu đúng các thư mục

| Đường dẫn | Vai trò | Có nên thay/xóa khi cập nhật? |
|---|---|---|
| `/var/www/lkh-web` | **Phiên bản đang chạy** (`lkh-web.service`) | Chỉ thay qua script deploy |
| `/var/www/lkh-web-next` | **Thư mục upload tạm** chứa bản Publish mới | Script sẽ đổi tên thành `lkh-web` sau khi kiểm tra |
| `/var/www/lkh-lib/data` | SQLite WAL và dữ liệu riêng của ứng dụng | **Không đè, không xóa** |
| `/var/www/lkh-lib/keys` | Khóa Data Protection bảo vệ phiên/cookie | **Không đè, không xóa** |
| `/etc/langkinhhukhong/lkh.env` | Tham số triển khai và khóa Cloudflare R2 | **Không đè, không công khai** |
| `/var/backups/lkh/<BACKUP_ID>` | Các snapshot website + SQLite + keys + `.env` | Bảo vệ quyền root, giữ đến khi xác nhận ổn định |

**Chỉ có một website đang hoạt động.** `lkh-web-next` không phải website thứ hai và không có service riêng. Sau khi triển khai thành công, thư mục `lkh-web-next` biến mất vì đã được đổi tên thành `lkh-web`.

Service hiện tại chạy dạng:

```ini
User=www-data
WorkingDirectory=/var/www/lkh-web
ExecStart=/usr/bin/dotnet /var/www/lkh-web/LangKinhHuKhong.Web.dll
Environment=ASPNETCORE_URLS=http://127.0.0.1:5110
Environment=LKH_DATA_ROOT=/var/www/lkh-lib/data
EnvironmentFile=/etc/langkinhhukhong/lkh.env
```

Đây là **đoạn tham chiếu**, không cần ghi đè file service nếu cấu hình đang hoạt động.

## 1. Cài công cụ cần thiết (thực hiện một lần)

```bash
sudo apt update
sudo apt install -y sqlite3 curl
sudo mkdir -p /var/backups/lkh
sudo chmod 700 /var/backups/lkh
sudo systemctl status lkh-web --no-pager -l
curl -fsS http://127.0.0.1:5110/api/health
```

Đảm bảo hệ thống đã có .NET 9 runtime, `libe_sqlite3.so` trong bản Publish và FFmpeg nếu dùng upload/convert MP3. Không cần cài lại Nginx/SSL khi chỉ thay source.

---

## 2. Tạo lệnh backup tự động `lkh-backup`

**Mỗi bản backup lưu:** (1) bản Publish đang chạy, (2) snapshot nhất quán của SQLite (kể cả khi ứng dụng dùng WAL), (3) Data Protection keys, (4) `lkh.env` nếu tồn tại. Cloudflare R2 lưu bên ngoài VPS nên **file audio trên R2 không nằm trong backup này**.

Tạo script:

```bash
sudo nano /usr/local/sbin/lkh-backup
```

Dán toàn bộ:

```bash
#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

ACTIVE=/var/www/lkh-web
DB=/var/www/lkh-lib/data/langkinhhukhong.db
KEYS=/var/www/lkh-lib/keys
ENV_FILE=/etc/langkinhhukhong/lkh.env
ROOT=/var/backups/lkh

[[ $(id -u) -eq 0 ]] || { echo 'Chay bang sudo'; exit 1; }
[[ -f "$ACTIVE/LangKinhHuKhong.Web.dll" ]] || { echo 'Khong tim thay ban website hien tai'; exit 1; }
[[ -f "$DB" ]] || { echo 'Khong tim thay SQLite'; exit 1; }
command -v sqlite3 >/dev/null || { echo 'Cai sqlite3: sudo apt install sqlite3'; exit 1; }

install -d -m 700 "$ROOT"
DEST=$(mktemp -d "$ROOT/$(date -u +%Y%m%d-%H%M%S)-XXXXXX")
chmod 700 "$DEST"
cp -a "$ACTIVE" "$DEST/lkh-web"
sqlite3 -readonly "$DB" ".backup '$DEST/langkinhhukhong.db'"
[[ $(sqlite3 -readonly "$DEST/langkinhhukhong.db" 'PRAGMA integrity_check;') == ok ]] || {
  echo 'Backup SQLite khong dat integrity_check'; exit 1;
}
[[ ! -d "$KEYS" ]] || cp -a "$KEYS" "$DEST/keys"
[[ ! -f "$ENV_FILE" ]] || cp -a "$ENV_FILE" "$DEST/lkh.env"
chmod 700 "$DEST"
printf 'BACKUP_ID=%s\n' "$(basename "$DEST")"
printf 'BACKUP_PATH=%s\n' "$DEST"
echo 'Backup thanh cong. File bao gom thong tin nhay cam, chi de root truy cap.'
```

Lưu file rồi cấp quyền:

```bash
sudo chmod 700 /usr/local/sbin/lkh-backup
```

Chạy thử tạo backup **không cần dừng website**:

```bash
sudo /usr/local/sbin/lkh-backup
```

Ví dụ kết quả:

```text
BACKUP_ID=20261017-080000-a1B2c3
BACKUP_PATH=/var/backups/lkh/20261017-080000-a1B2c3
Backup thanh cong.
```

Xem các snapshot đã tạo:

```bash
sudo ls -1 /var/backups/lkh
sudo du -sh /var/backups/lkh
```

**Bảo mật:** Thư mục backup có khóa R2 và khóa Data Protection. Không đặt trong `wwwroot`, không chia sẻ ZIP backup và không thay thành `chmod 777`. Nên có thêm bản sao lưu ngoài VPS được mã hóa để phòng hỏng ổ cứng.

---

## 3. Publish source Linux từ Windows

Mở **PowerShell** tại thư mục chứa dự án:

```powershell
cd D:\Dev\LangKinhHuKhong

# Xóa thư mục publish cũ trên Windows, KHÔNG xóa source.
Remove-Item .\publish\lkh-linux -Recurse -Force -ErrorAction SilentlyContinue

dotnet restore .\LangKinhHuKhong.Web\LangKinhHuKhong.Web.csproj -r linux-x64

dotnet publish .\LangKinhHuKhong.Web\LangKinhHuKhong.Web.csproj `
  -c Release `
  -r linux-x64 `
  --self-contained false `
  --no-restore `
  -o .\publish\lkh-linux
```

Kiểm tra file bắt buộc:

```powershell
Test-Path .\publish\lkh-linux\LangKinhHuKhong.Web.dll
Test-Path .\publish\lkh-linux\libe_sqlite3.so
Test-Path .\publish\lkh-linux\wwwroot\js\site.js
Test-Path .\publish\lkh-linux\wwwroot\js\lkh-interop.js
```

Các lệnh kiểm tra cần trả về `True` đối với source hiện hành. Nếu lỗi build hoặc thiếu thư viện Linux, **không triển khai**.

### Upload bản mới vào đâu?

Upload **toàn bộ nội dung bên trong** thư mục Windows:

```text
D:\Dev\LangKinhHuKhong\publish\lkh-linux\
```

Vào thư mục tạm trên VPS:

```text
/var/www/lkh-web-next/
```

Trước khi upload, chuẩn bị thư mục **chỉ dành cho bản mới**:

```bash
# Chỉ thực hiện khi không có phiên upload nào đang dở.
sudo rm -rf -- /var/www/lkh-web-next
sudo install -d -m 755 /var/www/lkh-web-next
```

Có thể dùng WinSCP/SFTP. Nếu SSH bằng root và muốn dùng SCP trên Windows:

```powershell
scp -r .\publish\lkh-linux\* root@160.191.175.132:/var/www/lkh-web-next/
```

**Lưu ý:** Không upload vào `/var/www/lkh-lib` và không chép đè trực tiếp vào `/var/www/lkh-web` khi service đang chạy. Nếu IP VPS thay đổi, thay IP ví dụ trong lệnh SCP.

Cấu trúc đúng trên VPS:

```text
/var/www/lkh-web-next/
  LangKinhHuKhong.Web.dll
  LangKinhHuKhong.Web.deps.json
  LangKinhHuKhong.Web.runtimeconfig.json
  libe_sqlite3.so
  wwwroot/js/site.js
  wwwroot/js/lkh-interop.js
  ...
```

**Sai:** `/var/www/lkh-web-next/lkh-linux/LangKinhHuKhong.Web.dll` (lồng thêm một cấp thư mục).

---

## 4. Tạo lệnh tự động cập nhật `lkh-deploy`

Script này **(1)** kiểm tra file Publish mới, **(2)** gọi `lkh-backup`, **(3)** dừng service rồi đổi bản bằng cách đổi tên thư mục, **(4)** khởi động và kiểm tra cả `api/health` lẫn trang chủ HTTPS, **(5)** tự khôi phục bản trước nếu các kiểm tra thất bại.

Tạo file:

```bash
sudo nano /usr/local/sbin/lkh-deploy
```

Dán:

```bash
#!/usr/bin/env bash
set -Eeuo pipefail
ACTIVE=/var/www/lkh-web
STAGE=/var/www/lkh-web-next
PREV=/var/www/lkh-web-old-swap
HEALTH=http://127.0.0.1:5110/api/health
PUBLIC=https://langkinhhukhong.duckdns.org/
STAMP=$(date -u +%Y%m%d-%H%M%S)
NEED_ROLLBACK=0

[[ $(id -u) -eq 0 ]] || { echo 'Chay bang sudo'; exit 1; }
[[ -d "$ACTIVE" ]] || { echo "STOP: thieu $ACTIVE"; exit 1; }
[[ -d "$STAGE" ]] || { echo "STOP: thieu $STAGE, chua upload ban moi"; exit 1; }
[[ ! -e "$PREV" ]] || { echo "STOP: $PREV dang ton tai, can xu ly truoc"; exit 1; }
for f in LangKinhHuKhong.Web.dll LangKinhHuKhong.Web.deps.json LangKinhHuKhong.Web.runtimeconfig.json libe_sqlite3.so wwwroot/js/site.js wwwroot/js/lkh-interop.js; do
  [[ -f "$STAGE/$f" ]] || { echo "STOP: thieu $STAGE/$f"; exit 1; }
done

recover() {
  code=$1
  trap - ERR INT TERM
  set +e
  if [[ $NEED_ROLLBACK == 1 ]]; then
    echo 'Deploy that bai, dang phuc hoi ban cu...'
    systemctl stop lkh-web
    if [[ -d "$PREV" ]]; then
      if [[ -d "$ACTIVE" ]]; then mv "$ACTIVE" "/var/www/lkh-web-failed-$STAMP"; fi
      mv "$PREV" "$ACTIVE"
    fi
    systemctl start lkh-web
  fi
  exit "$code"
}
trap 'recover $?' ERR
trap 'recover 130' INT
trap 'recover 143' TERM

echo '1/4: Sao luu website + SQLite + keys + env...'
/usr/local/sbin/lkh-backup

echo '2/4: Thay ban publish...'
systemctl stop lkh-web
NEED_ROLLBACK=1
mv "$ACTIVE" "$PREV"
mv "$STAGE" "$ACTIVE"
chown -R root:root "$ACTIVE"
chmod -R a+rX "$ACTIVE"

echo '3/4: Khoi dong va kiem tra health...'
systemctl start lkh-web
OK=0
for i in $(seq 1 30); do
  if systemctl is-active --quiet lkh-web && curl -fsS --max-time 3 "$HEALTH" >/dev/null \
    && curl -fsS --max-time 5 "$PUBLIC" >/dev/null; then
    OK=1
    break
  fi
  sleep 1
done
if [[ $OK != 1 ]]; then
  echo 'Health check that bai. Xem: journalctl -u lkh-web -n 100 --no-pager'
  recover 1
fi

echo '4/4: Hoan thanh. Giu ban backup o /var/backups/lkh.'
NEED_ROLLBACK=0
rm -rf -- "$PREV"
echo 'DEPLOY_OK'
```

Cấp quyền:

```bash
sudo chmod 700 /usr/local/sbin/lkh-deploy
```

### Thao tác mỗi khi có bản Publish mới

Sau khi upload xong, **kiểm tra đủ file**, rồi chạy một lệnh:

```bash
ls -lh /var/www/lkh-web-next/LangKinhHuKhong.Web.dll
ls -lh /var/www/lkh-web-next/wwwroot/js/lkh-interop.js
find /var/www/lkh-web-next -name libe_sqlite3.so

sudo /usr/local/sbin/lkh-deploy
```

Kết quả thành công:

```text
BACKUP_ID=...
...
DEPLOY_OK
```

Kiểm tra sau Deploy:

```bash
sudo systemctl status lkh-web --no-pager -l
curl -i http://127.0.0.1:5110/api/health
curl -I https://langkinhhukhong.duckdns.org/
curl -I https://langkinhhukhong.duckdns.org/js/lkh-interop.js
sudo journalctl -u lkh-web -n 60 --no-pager -o cat
```

Sau đó mở trang bằng trình duyệt ẩn danh, kiểm tra đăng nhập, nghe tập, tự phát tập tiếp theo, Admin upload MP3, HLS Premium và SEO. Kiểm tra HTTP không phát hiện được tất cả lỗi JavaScript phía trình duyệt.

Nếu `lkh-deploy` báo lỗi và tự rollback, thư mục `/var/www/lkh-web-failed-...` có thể còn được giữ lại để xem xét; không được xóa vội khi đang điều tra lỗi.

---

## 5. Tạo lệnh khôi phục `lkh-rollback`

Dùng khi bản Publish mới đã triển khai nhưng phát sinh lỗi nghiệp vụ hoặc lỗi JavaScript mà kiểm tra HTTP không phát hiện được.

Tạo file:

```bash
sudo nano /usr/local/sbin/lkh-rollback
```

Dán:

```bash
#!/usr/bin/env bash
set -Eeuo pipefail
[[ $(id -u) -eq 0 ]] || { echo 'Chay bang sudo'; exit 1; }
ID=${1:-}
[[ "$ID" =~ ^[0-9]{8}-[0-9]{6}-[[:alnum:]]{6}$ ]] || {
  echo 'Cach dung: sudo lkh-rollback BACKUP_ID'
  echo 'Danh sach: sudo ls -1 /var/backups/lkh'
  exit 1
}
FROM="/var/backups/lkh/$ID/lkh-web"
ACTIVE=/var/www/lkh-web
STAGE=/var/www/lkh-web-restore
PREV=/var/www/lkh-web-before-rollback
HEALTH=http://127.0.0.1:5110/api/health
PUBLIC=https://langkinhhukhong.duckdns.org/

[[ -f "$FROM/LangKinhHuKhong.Web.dll" ]] || { echo 'Khong tim thay backup hop le'; exit 1; }
[[ -d "$ACTIVE" ]] || { echo 'Thieu website hien tai'; exit 1; }
[[ ! -e "$STAGE" && ! -e "$PREV" ]] || { echo 'Con thu muc restore/swap cu, dung lai de kiem tra'; exit 1; }

echo 'Sao luu ban dang chay truoc khi rollback...'
/usr/local/sbin/lkh-backup
cp -a "$FROM" "$STAGE"
chown -R root:root "$STAGE"
chmod -R a+rX "$STAGE"

systemctl stop lkh-web
mv "$ACTIVE" "$PREV"
mv "$STAGE" "$ACTIVE"
systemctl start lkh-web
OK=0
for i in $(seq 1 30); do
  if systemctl is-active --quiet lkh-web && curl -fsS --max-time 3 "$HEALTH" >/dev/null \
    && curl -fsS --max-time 5 "$PUBLIC" >/dev/null; then
    OK=1
    break
  fi
  sleep 1
done
if [[ $OK == 1 ]]; then
  rm -rf -- "$PREV"
  echo "ROLLBACK_OK: $ID"
else
  echo 'Restore chua hoat dong, phuc hoi lai ban vua su dung...'
  systemctl stop lkh-web || true
  mv "$ACTIVE" "/var/www/lkh-web-failed-rollback-$(date -u +%Y%m%d-%H%M%S)"
  mv "$PREV" "$ACTIVE"
  systemctl start lkh-web
  exit 1
fi
```

Cấp quyền:

```bash
sudo chmod 700 /usr/local/sbin/lkh-rollback
```

### Cách rollback về bản cũ

**Bước 1 — Xem danh sách backup:**

```bash
sudo ls -1 /var/backups/lkh
```

Ví dụ:

```text
20261017-080000-a1B2c3
20261017-110000-d4E5f6
```

**Bước 2 — Chọn đúng `BACKUP_ID` của bản muốn khôi phục:**

```bash
sudo /usr/local/sbin/lkh-rollback 20261017-080000-a1B2c3
```

ID ở trên chỉ là ví dụ; dùng ID thực tế mà `lkh-backup` đã in ra. Script sẽ tự sao lưu bản đang chạy trước khi rollback, lấy code cũ từ backup, dừng service, đổi thư mục rồi chạy lại.

**Bước 3 — Kiểm tra:**

```bash
sudo systemctl status lkh-web --no-pager -l
curl -i http://127.0.0.1:5110/api/health
curl -I https://langkinhhukhong.duckdns.org/
```

**Mặc định rollback chỉ trả lại chương trình, không đụng dữ liệu SQLite, keys, R2 hoặc các cấu hình Nginx.** Trường hợp code cũ không tương thích schema database đã bị migration bởi bản mới, không thể bảo đảm rollback ứng dụng riêng lẻ hoạt động; lúc đó cần đánh giá khả năng phục hồi SQLite từ snapshot, chấp nhận mất các thay đổi dữ liệu sau thời điểm backup. Không tự động đè database trong script.

---

## 6. Cứu hộ nếu vô tình đổi thư mục thủ công và website ngừng chạy

Ví dụ, đã chạy `mv /var/www/lkh-web ...backup...` nhưng **chưa có** `/var/www/lkh-web-next`, khiến service không tìm được DLL.

```bash
sudo systemctl stop lkh-web
ls -ld /var/www/lkh-web*
```

Nếu `/var/www/lkh-web` **không tồn tại** nhưng bạn thấy thư mục backup ứng dụng cũ ở `/var/www/lkh-web-backup-...`, dùng đường dẫn thực tế (không sao chép ví dụ mù quáng):

```bash
# Ví dụ - thay bằng đúng tên backup đang có trên máy
sudo mv /var/www/lkh-web-backup-YYYYMMDD-HHMMSS /var/www/lkh-web
sudo systemctl start lkh-web
sudo systemctl status lkh-web --no-pager -l
```

Nếu có `/var/www/lkh-web-old-swap` do script bị ngắt ngang, **đừng chạy lại deploy ngay**. Kiểm tra vị trí `lkh-web` và các thư mục trước rồi khôi phục thư mục cũ khi service đã dừng.

---

## 7. Checklist an toàn và xử lý lỗi

- **Thiếu `/var/www/lkh-web-next`:** Chưa upload bản mới; deploy sẽ từ chối thay bản đang chạy.
- **Thiếu `libe_sqlite3.so`:** Publish sai runtime. Chạy lại `dotnet publish -r linux-x64`.
- **`Access denied` với SQLite/keys:** Xem quyền `/var/www/lkh-lib` và user `www-data`; không dùng `chmod 777`.
- **HTTP 502:** Kiểm tra `lkh-web.service`, `journalctl`, và Kestrel `127.0.0.1:5110`.
- **HTTP 500 nhưng `/api/health` trả 200:** Kiểm tra exception ứng dụng và `ForwardedHeaders`, Razor/Antiforgery.
- **`Could not find lkh.api`:** Kiểm tra bản DLL đang chạy, `wwwroot/js/lkh-interop.js`, Chrome Console và cache JS; deploy mới chỉ bảo đảm tải được tệp, không bảo đảm toàn bộ JS Interop chạy đúng.
- **Upload MP3 lỗi:** Kiểm tra `ffmpeg`, `ffprobe`, hạn mức tải file, R2, quyền thư mục và log ứng dụng.
- **VPS có website khác:** Không xóa `sites-enabled` của website khác, không tùy tiện bật UFW hoặc thay service Nginx đang dùng chung.
- **Backup không phải phương án dự phòng hoàn chỉnh:** Muốn khôi phục toàn bộ sau sự cố VPS cần thêm chiến lược backup nội dung trên R2 và sao lưu ngoài VPS, được mã hóa.

### Lệnh kiểm tra nhanh

```bash
sudo systemctl status lkh-web --no-pager -l
sudo systemctl show lkh-web -p ExecStart -p WorkingDirectory
sudo journalctl -u lkh-web -n 100 --no-pager -o cat
sudo ls -1 /var/backups/lkh
ls -ld /var/www/lkh-web /var/www/lkh-lib
curl -i https://langkinhhukhong.duckdns.org/api/health
```

## 8. Quy trình sử dụng chuẩn (mỗi lần cập nhật)

1. **Windows:** Publish `linux-x64` vào `publish/lkh-linux` và kiểm tra đủ file.
2. **WinSCP/SCP:** Upload **nội dung** `publish/lkh-linux` vào `/var/www/lkh-web-next`.
3. **VPS:** Chạy `sudo lkh-deploy` (tự tạo backup, triển khai và rollback nếu health không đạt).
4. **Trình duyệt:** Kiểm tra các chức năng chính. Nếu lỗi ngoài phạm vi health check, chọn snapshot và chạy `sudo lkh-rollback BACKUP_ID`.

**Không bao giờ cần có hai website chạy song song.** Chỉ service `lkh-web` phục vụ phiên bản đang nằm ở `/var/www/lkh-web`.
