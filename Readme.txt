
sc.exe create __PDF2PNG_IN_1000 binpath="D:\tt\build\pdf2png\pdf2png.exe --1000"
sc.exe create __PDF2PNG_IN_1000 binpath="D:\tt\build\pdf2png\pdf2png.exe"
sc.exe delete __PDF2PNG_IN_1000


D:\tt\build\pdf2png\pdf2png.exe --service-install --service-name __PDF2PNG_IN_1000 --1000
pdf2png.exe install -servicename "__PDF2PNG_IN" -displayname "__PDF2PNG_IN" -description "Convert PDF to bitmap and store in Redis"


C:\windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe D:\tt\build\pdf2png\pdf2png.exe

C:\windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /port=6379 D:\tt\build\pdf2png\pdf2png.exe