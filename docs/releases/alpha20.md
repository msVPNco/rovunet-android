# Rovunet 0.3.0-alpha20 Full · code43

## فارسی

آلفا۲۰ Full، کد ۴۳: صفحهٔ آغاز برند Rovunet در سه تم و چهار زبان، کاهش کار اضافی هنگام خواندن پروفایل‌های ذخیره‌شده، دریافت مستقیم و رمزگشایی منبع مهسا، پنهان‌شدن نشانی خام منابع عمومی، دانلود امن آپدیت از تونل فعال، واردسازی فایل قابل لغو و محدود، نمایش قرمز خطا و متن حقوقی کوچک انگلیسی. اصلاح طول دامنه در SOCKS موتور کمکی نیز اعمال شده است. قابلیت‌های آلفا۱۹ حفظ شده‌اند؛ این نسخه پروتکل تازه‌ای اضافه نمی‌کند.

- مهسا مستقیماً از منبع اصلی دریافت می‌شود؛ نشانی‌های قدیمی پیش از درخواست مهاجرت می‌کنند. فقط configهای mci/mtn خوانده، تکراری‌ها حذف و ورودی‌ها اعتبارسنجی می‌شوند؛ ads_url دریافت نمی‌شود. نام و دکمهٔ دریافت/آپدیت مهسا و انونیموس باقی است؛ نشانی خام در نمایش و کپی پنهان است.
- جایگزینی موفقِ تأییدشده فقط موارد قدیمی و تغییرنکردهٔ همان منبع عمومی را حذف می‌کند. ویرایش محلی، پروفایل دستی، سایر منابع، قفل‌ها و ارجاع زنجیره حفظ می‌شوند. خطای دریافت/رمزگشایی یا خروجی خالی دادهٔ قبلی را پاک نمی‌کند؛ نتیجهٔ ترکیبیِ معتبر/ناسازگار نیازمند بازبینی است.
- دانلود آپدیت در اتصال پشتیبانی‌شده از تونل فعال، با بررسی TLS، هش، بسته، نسخه و امضا و ادامهٔ دانلود محدود انجام می‌شود؛ قطع یا تغییر نشست دانلود را متوقف می‌کند و به مسیر مستقیم برنمی‌گردد. آپدیت از زنجیره تا تکمیل مالکیت endpoint پشتیبانی نمی‌شود و خطای روشن دارد. این مسیر برای دانلودهای بعدی از نسخهٔ نصب‌شدهٔ جدید مؤثر است.
- صفحهٔ آغاز فقط تا آماده‌شدن واقعی پروفایل و انتخاب ذخیره‌شده می‌ماند؛ تایمر اجباری ندارد. شبکهٔ اختیاری بعد از آمادگی شروع می‌شود؛ اعتبارسنجی و رمزگذاری داده ضعیف نشده‌اند. بهبود درصدی سرعت یا باتری اندازه‌گیری نشده است.

## English

Alpha20 Full / code43 adds a themed Rovunet startup cover and removes redundant startup digest work; direct encrypted Mahsa fetching, public-source URL privacy, secure active-tunnel update downloads, bounded cancellable file import, red failure labels and small English legal attribution. It also fixes SOCKS domain-length handling in the constrained auxiliary engine. Alpha19 capabilities are retained; this version adds no new protocol.

Mahsa legacy aliases migrate before requests; only mci/mtn config values are decoded, deduplicated and validated. Ads and nested remote URLs are not fetched. Successful reviewed public refresh removes only eligible unchanged retired rows of that source; manual, edited, locked, pinned, chain-referenced and other-source data remain protected. Download/decryption/empty-feed failure preserves saved data. Mixed supported/unsupported results require review.

Updater metadata, checksum and APK requests use a supported active authenticated tunnel with remote DNS, HTTPS, bounded resume/retry and package/version/signer/hash verification. Route/session changes or cancellation stop the download, with no silent direct fallback. Chain updater endpoints remain explicitly unsupported. Startup has no minimum timer, restores saved selection before controls, defers optional networking and preserves encrypted storage/certificate checks. No phone speedup percentage is claimed.

## Русский

Alpha20 Full / код43: фирменный стартовый экран Rovunet в трёх темах и четырёх языках, меньше лишних вычислений при загрузке профилей, прямое получение зашифрованной Mahsa, скрытые URL публичных источников, безопасное обновление через активный туннель, ограниченный отменяемый импорт файлов, красные ошибки и небольшой английский текст лицензий. Исправлена обработка длины домена SOCKS вспомогательного движка. Возможности alpha19 сохранены; новых протоколов нет.

Старые адреса Mahsa перенаправляются до запроса. Читаются только config из mci/mtn, без рекламы и вложенных удалённых URL; выполняются удаление дубликатов и проверка импорта. После успешного подтверждённого обновления удаляются лишь неизменённые устаревшие записи этого публичного источника. Ручные/изменённые, защищённые и используемые цепочкой записи сохраняются; ошибка или пустой результат сохраняет прежние данные. Смешанный результат требует просмотра.

Обновлятор использует поддерживаемый активный туннель, удалённый DNS, HTTPS и проверку хеша, пакета, версии и подписи. Смена маршрута/сеанса останавливает загрузку без прямого обхода. Обновление через цепочку пока явно не поддерживается. Стартовый экран исчезает при готовности данных без минимального таймера; выбранный профиль и шифрование сохраняются. Измеренного ускорения телефона не заявлено.

## 简体中文

Alpha20 Full / 代码43：三主题、四语言 Rovunet 启动封面，减少配置加载时重复摘要计算，直接获取并解密 Mahsa，隐藏公共来源原始地址，通过活动隧道安全下载更新，可取消且有大小限制的文件导入，红色失败文字和小号英文法律署名。修复辅助引擎 SOCKS 域名长度处理。保留 alpha19 功能，没有新增协议。

请求前迁移 Mahsa 旧地址；仅读取 mci/mtn 的 config，去重并验证，不请求广告或嵌套远程地址。成功且经审核的公共订阅更新，仅删除同来源未修改的过期条目；保留手动、已编辑、受保护、被链引用和其他来源的数据。下载、解密失败或空结果保留原数据；混合兼容/不兼容结果需审核。

更新元数据、校验和及 APK 经支持的活动隧道和远程 DNS 下载，验证 HTTPS、哈希、包名、版本和签名。路线或会话变化会停止下载，不静默直连；链式连接更新暂明确不支持。启动不设最短等待时间，数据和已保存选择就绪后显示首页，延后可选网络工作，保留加密与证书校验。不声称已测得手机启动提速。

## Upgrade, evidence and limitations

Owner accepted this exact code43 APK for publication on 2026-11-13. It is byte-identical to delivered review03, without rebuilding or re-signing. Install over the existing production app without uninstalling or clearing data. Package `app.veyra.client`, schema50 and production certificate remain unchanged. VersionCode increases from public40 and review41/42 to43. No new Lite or desktop binary.

[Source and provenance](alpha20-provenance.json): application commit `4714442846226c2c9c44444d43794ef25953761b`; [passing exact-source PR CI](https://github.com/msVPNco/rovunet-app/actions/runs/43372323547): 154 portable/JUnit cases, Gradle unit task, 86 Debug emulator and two minified Release JNI cases, plus local encrypted transport fixtures. All 12 language/theme startup combinations checked; synthetic previews are not phone captures. Signatures verified for APIs24–36, same production certificate, 39 native libraries and 25 aligned 64-bit ELF files.

Independent push run43372317753 failed an unchanged local Mieru TCP relay fixture and an existing onboarding-persistence 15s deadline. Verify-only retry passed unchanged; its Android failure remains recorded. The exact-source PR run passed. Neither intermittent failure is claimed fixed. Physical carrier/startup timing, idle/screen-off battery/data and own-panel WireGuard/Hy2 remain unmeasured/unresolved. Some native engines are inherited checksum-pinned binaries; exact dnstt-mobile source remains unavailable, so no complete fresh native rebuild/reproducibility claim. Telegram “group”→“channel” wording remains deferred as requested.

APK SHA-256: `930687063ffd7109cddff9ec798d988d1c2f129a806a8f861ff2da55d5d82d12`.

Full/light explicit source archives, Persian guide, R8 mapping, public certificate, signature log, CI evidence, source manifest and SHA256SUMS accompany the APK. Private signing material and private/unsigned test APKs are excluded.
