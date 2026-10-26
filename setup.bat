@echo off
chcp 65001 >nul
title CodePin - Cài đặt ứng dụng
cls
echo ========================================================
echo   📌 CÀI ĐẶT CODEPIN - ALWAYS ON TOP CHO CODER
echo ========================================================
echo.

set "SOURCE_DIR=%~dp0publish"
if not exist "%SOURCE_DIR%\CodePin.exe" (
    set "SOURCE_DIR=%~dp0"
)

if not exist "%SOURCE_DIR%\CodePin.exe" (
    echo [ERROR] Không tìm thấy file CodePin.exe!
    echo Vui lòng đảm bảo bạn đã tải đầy đủ thư mục.
    pause
    exit /b 1
)

set "INSTALL_DIR=%LocalAppData%\Programs\CodePin"
echo [*] Đang cài đặt vào: %INSTALL_DIR%
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

echo [*] Đang sao chép tệp chương trình...
copy /Y "%SOURCE_DIR%\CodePin.exe" "%INSTALL_DIR%\CodePin.exe" >nul
if exist "%SOURCE_DIR%\app.ico" (
    copy /Y "%SOURCE_DIR%\app.ico" "%INSTALL_DIR%\app.ico" >nul
) else if exist "%~dp0app.ico" (
    copy /Y "%~dp0app.ico" "%INSTALL_DIR%\app.ico" >nul
)

echo [*] Đang tạo lối tắt ngoài màn hình chính (Desktop)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$WshShell = New-Object -ComObject WScript.Shell; $d = [Environment]::GetFolderPath('Desktop'); $s = $WshShell.CreateShortcut(\"$d\CodePin.lnk\"); $s.TargetPath = '%INSTALL_DIR%\CodePin.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'CodePin - Ghim Ảnh & Cửa Sổ Nổi Cho Coder'; if (Test-Path '%INSTALL_DIR%\app.ico') { $s.IconLocation = '%INSTALL_DIR%\app.ico, 0' }; $s.Save()"

echo [*] Đang tạo lối tắt trong Menu Start...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$WshShell = New-Object -ComObject WScript.Shell; $sm = [Environment]::GetFolderPath('Programs'); $s = $WshShell.CreateShortcut(\"$sm\CodePin.lnk\"); $s.TargetPath = '%INSTALL_DIR%\CodePin.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'CodePin - Ghim Ảnh & Cửa Sổ Nổi Cho Coder'; if (Test-Path '%INSTALL_DIR%\app.ico') { $s.IconLocation = '%INSTALL_DIR%\app.ico, 0' }; $s.Save()"

echo.
echo ========================================================
echo   ✅ CÀI ĐẶT HOÀN TẤT!
echo   - Lối tắt Desktop: Đã tạo (icon 📌)
echo   - Start Menu: Tìm kiếm 'CodePin' là thấy ngay
echo ========================================================
echo.
set /p RUN="Bạn có muốn khởi động CodePin ngay bây giờ không? (Y/N, mặc định Y): "
if /i "%RUN%"=="N" goto end

start "" "%INSTALL_DIR%\CodePin.exe"

:end
echo.
echo Cảm ơn bạn đã sử dụng CodePin!
timeout /t 3 >nul
exit /b 0
