# Reborn Music

Музыкальный плеер в стиле Groove Music из Windows 10. Без Microsoft Store и без аккаунтов.
Это самостоятельный проект с собственным кодом: оригинальный Groove не декомпилирован, его код и ресурсы здесь не используются.

| Папка | Платформа | Технологии |
|---|---|---|
| [`Reborn/`](Reborn) | Windows 10/11, обычный .exe | C#, WinUI 3 (Windows App SDK 1.6), .NET 8, TagLibSharp |
| [`RebornAndroid/`](RebornAndroid) | Android 10+, arm64-v8a | C#, Uno Platform 7, .NET 10 ([README](RebornAndroid/README.md)) |

## Windows: сборка и запуск

Нужен .NET 8 SDK. Версия зафиксирована в `global.json`.

```powershell
cd Reborn
dotnet build -c Release
.\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\RebornMusic.exe
```

Чтобы получить папку, которую можно скопировать на другой ПК (всё нужное лежит внутри, ничего ставить не надо):

```powershell
dotnet publish -c Release -o ..\dist
```

### Иконки

Groove рисует иконки своим шрифтом `SegMVR2.ttf`. Это шрифт Microsoft, и в репозитории его нет.
Если на компьютере установлен Groove Music, при первом запуске приложение копирует этот шрифт из установленного пакета в папку `Fonts` рядом с exe и берёт иконки оттуда.
Если Groove не установлен, используются иконки системного шрифта Segoe MDL2.
Папка `Fonts/` в `.gitignore`: не выкладывайте её вместе со сборкой.

### Где что лежит

| Путь | Что там |
|---|---|
| `Reborn/App.xaml` | Тема Windows 10: цвета, острые углы, Segoe UI, стили кнопок, вкладки с подчёркиванием |
| `Reborn/MainWindow.xaml` | Свой заголовок окна, левая панель, нижняя панель плеера |
| `Reborn/Pages/` | «Моя музыка», альбом/исполнитель, «Последние», «Сейчас играет», «Плейлисты», «Настройки» |
| `Reborn/Controls/` | Ползунок в стиле Groove, цвета от акцента Windows, подсветка Reveal, акриловый фон, шрифт иконок |
| `Reborn/Services/LibraryService.cs` | Поиск музыки в папках, теги, обложки, кэш |
| `Reborn/Services/PlayerService.cs` | Очередь, перемешивание, повтор, медиаклавиши Windows |

Данные приложения хранятся в `%LOCALAPPDATA%\RebornMusic` (настройки, кэш библиотеки, обложки).

### Заметка для правок XAML

В словаре ресурсов `<ResourceDictionary.ThemeDictionaries>` должен идти сразу после `MergedDictionaries`, до остальных ресурсов.
Иначе компилятор XAML в WinUI падает с кодом 1 и ничего не объясняет.
