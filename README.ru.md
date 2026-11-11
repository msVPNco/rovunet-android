<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="140"></p>

<h1 align="center">Rovunet</h1>

<p align="center"><b>Несколько путей к вашему серверу — на любом устройстве</b><br>DNS-туннели, SSH, HTTPS-прокси и профили Xray в именованных группах, с прямым подключением и настоящими тестами HTTPS.</p>

<p align="center">
  <a href="https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha19"><img alt="Android release" src="https://img.shields.io/github/v/release/msVPNco/rovunet-app?sort=date&display_name=tag&filter=v*&label=android&color=0E7C86"></a>
  <img alt="Platforms" src="https://img.shields.io/badge/platforms-Android%20%C2%B7%20Windows%20%C2%B7%20Linux%20%C2%B7%20macOS-14304D">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0-6D4AFF"></a>
  <a href="https://t.me/RovunetVPN"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-RovunetVPN-2F6FEB?logo=telegram&logoColor=white"></a>
</p>

<p align="center"><a href="README.md">English</a> · <a href="README.fa.md">فارسی</a> · <b>Русский</b> · <a href="README.zh-CN.md">简体中文</a></p>

<p align="center">📥 <a href="#apps">Скачать</a> · 📘 <a href="docs/guides/Rovunet-App-Guide-RU.pdf">Полное руководство (PDF)</a> · <a href="docs/USER_GUIDE.ru.md">Руководство</a> · <a href="https://github.com/msVPNco/rovunet-panel">Серверная панель</a></p>

---

**Rovunet — семейство VPN-клиентов для доступа к собственному серверу разными путями.** DNS-туннели, SSH, HTTPS-прокси и профили на базе Xray собраны в одном интерфейсе. Связанные пути остаются вместе в именованных группах: подключайте выбранный путь напрямую или отдельно измеряйте HTTPS через настоящий туннель. Все клиенты находятся в одном репозитории; серверная часть — [Rovunet Panel](https://github.com/msVPNco/rovunet-panel).

<a id="apps"></a>

## Приложения

| Платформа | Состояние | Загрузка и подробности |
| --- | --- | --- |
| **Android** | Публичный выпуск: **0.3.0-alpha19 Full, код 40** | [Скачать](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha19) · [Страница Android](docs/android/README.ru.md) |
| **Windows** | **0.1.1 Beta · x64 · предварительный выпуск** | [Скачать](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.1-beta) · [Страница Windows](windows/README.md) |
| **Linux** | Планируется | Выпусков пока нет |
| **macOS** | **0.1.6 Beta · Apple Silicon / Intel · предварительный выпуск** | [Скачать](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.6-beta) · [Страница Mac](docs/macos/README.ru.md) · Intel: ограниченное тестирование |

У каждой платформы свои теги, файлы, контрольные суммы и подпись. «Latest» на GitHub относится ко всему репозиторию и не является каналом конкретной платформы, поэтому скачивайте из выпуска, в названии которого указана ваша платформа. Правила тегов и обновлений: [Платформы и каналы выпусков](docs/PLATFORMS.md). Пока выпуск прямо не сообщает об обратном, ничто в этой таблице не означает поддержку протоколов или проверенную установку на десктопе.

## Что делает Rovunet

Описание пока относится к Android; другие платформы появятся в списке, когда у них будет выпуск.

- **Группы**: связанные пути объединяются по серверу и учётной записи; явный выбор и завершённые измерения запоминаются.
- **Прямое подключение**: первое нажатие выбирает путь, второе подключает. Обязательного пинга или сканирования перед подключением нет.
- **Настоящие тесты HTTPS**: реальный запрос HTTPS/204 через туннель с проверкой сертификата, долей успехов и разбросом задержки. Неудачное прежнее измерение не мешает ручному подключению.
- **Много протоколов**: SSH, DNS-туннели (DNSTT, NoizDNS, VayDNS, Slipstream, Soar), NaiveProxy, SOCKS5, DoH, VLESS WebSocket, REALITY, XHTTP, Hysteria2, WireGuard и Tor.
- **Подписки и обмен**: HTTPS-подписки, ссылки `rovunet://`, QR-коды, файлы и резервные копии с паролем.
- **Четыре языка**: персидский, английский, русский и упрощённый китайский.

Полные списки, ограничения и оговорки — на [странице Android](docs/android/README.ru.md). Нужны сервер и действующие учётные данные; приложения не выдают бесплатных аккаунтов, и ни один протокол не гарантирует доступ через любую сеть.

## Быстрый старт на Android

1. **Скачайте** Full APK со [страницы выпуска](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha19) и установите. Публичные alpha14 и новее обновляются поверх, без удаления.
2. **Откройте** Rovunet, выберите язык и тему и дайте разрешение Android VPN.
3. **Импорт**: Подписки → вставьте HTTPS-ссылку подписки → **Импорт**, либо **+ → Импорт** для ссылки, QR-кода или файла.
4. **Подключение**: раскройте группу, нажмите путь для выбора и нажмите ещё раз для подключения.

## Руководства

| | |
|---|---|
| 📘 Полное руководство (PDF) | [English](docs/guides/Rovunet-App-Guide-EN.pdf) · [فارسی](docs/guides/Rovunet-App-Guide-FA.pdf) · [Русский](docs/guides/Rovunet-App-Guide-RU.pdf) · [简体中文](docs/guides/Rovunet-App-Guide-ZH.pdf) |
| [Руководство пользователя](docs/USER_GUIDE.ru.md) | Установка, импорт, подключение, проверки, Soar, учётные данные и диагностика |
| [Страница Android](docs/android/README.ru.md) | Состояние выпуска, возможности, протоколы и ограничения |
| [Платформы](docs/PLATFORMS.md) · [Текущее состояние](docs/STATUS.md) · [Обновление](docs/UPGRADE.md) | Каналы выпусков, состояние выпуска, подпись и миграция |

PDF сохраняют оформление и редакцию alpha15.4; новые статус, меню и режим простоя описаны в текущем руководстве и [заметках alpha19](docs/releases/alpha19.md).

## Исходники и поддержка

[Сборка](docs/BUILDING.md) · [Выпуск](docs/RELEASING.md) · [Безопасность](SECURITY.md).

Для обращения укажите версии приложения и системы, протокол, шаги воспроизведения и локальный безопасный отчёт. Не публикуйте ссылки конфигураций, пароли, закрытые ключи и идентификаторы устройства. Сообщество: [t.me/RovunetVPN](https://t.me/RovunetVPN).

## Добровольная поддержка

Если хотите помочь развитию, пожертвование добровольно и не требуется для использования. **USDT, только Tron/TRC20:**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## Лицензия и авторство

AGPL-3.0: [LICENSE](LICENSE), [NOTICE](NOTICE.md). Xray-core — MPL-2.0; версии и уведомления сохранены.

На основе [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full автора Anonvector. Rovunet — независимый проект, не связанный официально со SlipNet.

