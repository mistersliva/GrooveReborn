# Reborn Music для Android

Порт Windows-плеера Reborn Music (`..\Reborn`, WinUI 3) на Android через Uno Platform 7 (WinUI XAML API, рендерер Skia), .NET 10, целевая ABI **arm64-v8a**, Android 10+ (API 29), targetSdk 36.

## Сборка

Нужно: .NET SDK 10.0.4xx, workload `android` (`dotnet workload install android`), JDK 17, Android SDK (platform 36, build-tools 36). SDK лежит в `D:\Android\sdk` и подхватывается из `Directory.Build.props`; другой путь передаётся как `-p:AndroidSdkDirectory=...`.

```powershell
cd D:\GrooveReborn\RebornAndroid\RebornAndroid
dotnet build -f net10.0-android -c Release
```

Сборка кладёт APK в `RebornAndroid\bin\Release\net10.0-android\com.reborn.music-Signed.apk` (подписан отладочным ключом, ставится вручную). Последняя проверенная сборка лежит в корне: `RebornMusic-arm64-v8a.apk`. Установка на телефон с включённой отладкой по USB:

```powershell
D:\Android\sdk\platform-tools\adb.exe install -r D:\GrooveReborn\RebornAndroid\RebornMusic-arm64-v8a.apk
```

Проверено на телефоне с Android 15: поиск музыки, вкладки, сортировка, страницы альбома, «Сейчас играет», воспроизведение, медиауведомление, кнопки гарнитуры, кнопка «Назад», светлая и тёмная темы.

## Что изменилось по сравнению с Windows-версией

| Windows | Android |
|---|---|
| `Windows.Media.Playback.MediaPlayer` + SMTC | `Android.Media.MediaPlayer` в `PlayerService`, фоновый `PlaybackService` (foreground service типа mediaPlayback), `MediaSession`, медиа-уведомление, аудиофокус, пауза при отключении наушников |
| Обход папок + TagLibSharp | MediaStore (`READ_MEDIA_AUDIO`; на Android 10–12 `READ_EXTERNAL_STORAGE`); теги берутся из системного индекса, обложки из `ContentResolver.LoadThumbnail` в кэш JPEG. TagLibSharp не нужен |
| Окно, свой заголовок, Acrylic | Нет. Боковая панель 320 px — выдвижное меню (SplitView Overlay) с гамбургером; на экранах от 1008 px панель закреплена |
| Панель плеера 90 px | На экране ≥ 720 px та же раскладка, что в Windows; на телефоне — две строки (перемотка с перемешиванием и повтором сверху, обложка, название и кнопки снизу), громкость во всплывающем окне |
| Двойной щелчок по строке | Одно касание |
| Segoe MDL2 / SegMVR2 | Uno Fluent UI Assets (Apache 2.0, те же коды глифов, что у MDL2), текст — Open Sans (OFL). Шрифты Microsoft в APK не входят |
| Акцент Windows | Синий акцент Windows 10 по умолчанию (#0078D7) |

Звуки короче 30 секунд (игровые ресурсы, рингтоны, голосовые) по умолчанию скрыты; это отключается в настройках.

Плитки альбомов и исполнителей — это строки по N плиток в обычном ListView: стандартная панель GridView в Uno на Android раскладывает их в один столбец, а явная `ItemsWrapGrid` не показывает ничего.

Плейлисты пока не перенесены (в Windows-версии их добавляют параллельно).
