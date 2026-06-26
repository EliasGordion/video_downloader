# Чек-лист релиза

Перед публикацией ZIP нужно проверить:

- `Clip.exe` лежит в верхнем уровне архива;
- рядом есть `Start Clip.cmd`;
- папка `Resources/bin` содержит `yt-dlp.exe`, `ffmpeg.exe` и `ffprobe.exe`;
- приложение запускается после распаковки в новую папку;
- README объясняет, что `Code > Download ZIP` не подходит для обычного запуска.

Для сборки релиза:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-PortableRelease.ps1
```
