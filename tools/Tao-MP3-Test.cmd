@echo off
REM Tao file MP3 6 phut 5 giay de kiem thu preview 5 phut.
ffmpeg -y -f lavfi -i "sine=frequency=440:duration=365" -c:a libmp3lame -b:a 48k test-6-phut.mp3
echo Xong: test-6-phut.mp3
pause
