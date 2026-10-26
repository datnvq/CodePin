@echo off
chcp 65001 >nul
title CodePin - Gỡ cài đặt
cls
echo ========================================================
echo   🗑️ GỠ CÀI ĐẶT CODEPIN
echo ========================================================
echo.

set "INSTALL_DIR=%LocalAppData%\Programs\CodePin"
set /p CONFIRM="Bạn có chắc chắn muốn gỡ cài đặt CodePin không? (Y/N): "
if /i not "%CONFIRM%"=="Y" exit /b 0

taskkill /F /IM CodePin.exe >nul 2>&1

echo [*] Đang xóa lối tắt Desktop...
del /F /Q "%USERPROFILE%\Desktop\CodePin.lnk" >nul 2>&1

echo [*] Đang xóa lối tắt Start Menu...
del /F /Q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\CodePin.lnk" >nul 2>&1

echo [*] Đang xóa thư mục cài đặt...
if exist "%INSTALL_DIR%" (
    rmdir /S /Q "%INSTALL_DIR%" >nul 2>&1
)

echo.
echo ✅ Đã gỡ cài đặt CodePin thành công!
timeout /t 3 >nul
exit /b 0
