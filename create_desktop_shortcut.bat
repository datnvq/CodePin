@echo off
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "$WshShell = New-Object -ComObject WScript.Shell; $d = [Environment]::GetFolderPath('Desktop'); $s = $WshShell.CreateShortcut(\"$d\CodePin.lnk\"); $s.TargetPath = (Resolve-Path '.\publish\CodePin.exe').Path; $s.WorkingDirectory = (Resolve-Path '.\publish').Path; $s.Description = 'CodePin - Ghim Ảnh & Cửa Sổ Nổi Cho Coder'; $s.IconLocation = (Resolve-Path '.\publish\app.ico').Path + ', 0'; $s.Save(); Write-Host 'Đã tạo Shortcut CodePin ngoài Desktop thành công!' -ForegroundColor Green"
pause
