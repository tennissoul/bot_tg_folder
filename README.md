# Merchant Telegram VPN Bot

Telegram-бот на C#/.NET 10 для управления пользователями и VPN-подписками. В проекте есть русская и английская локализация, административные команды, реферальные ссылки, хранение состояния в JSON, генерация тестовых ключей, создание CryptoPay invoice-ссылок и необязательный AI-ответчик через OpenRouter.

> **Оплата:** production-покупки используют CryptoPay webhook с проверкой HMAC-подписи, статуса invoice, суммы и идемпотентным учётом заказа. Для работы нужны публичный HTTPS reverse proxy и `CRYPTOPAY_WEBHOOK_URL`. Тестовый режим выдаёт ключ без оплаты и предназначен только для разработки.
>
> **Хранилище:** приложение использует локальный JSON-файл, а не PostgreSQL/SQLite. Это подходит только для одного экземпляра бота с локальным диском; файл не обеспечивает транзакции между несколькими экземплярами и не является защищённым аудит-журналом.

## Возможности

- Telegram long polling через пакет `Telegram.Bot`.
- Главное меню и обработчики на русском и английском языках.
- Команды и кнопки для профиля, подписок, поддержки и тарифов.
- Административные операции: выдача/удаление подписки, бан, статистика, рассылка и остановка.
- Создание CryptoPay invoice и выдача подписки после проверенного `invoice_paid` webhook.
- Тестовая выдача подписки по явному флагу `ALLOW_TEST_PAYMENTS=true`, когда `CRYPTOPAY_TOKEN` не задан.
- Реферальные ссылки и экран статистики; начисление бонусов не подключено к обработчику успешной оплаты.
- Необязательные ответы AI через OpenRouter.
- Опциональное подключение к Telegram через HTTP-прокси.
- JSON-хранилище пользователей, транзакций, ключей и реферальных данных.

## Требования

- .NET SDK 10 для разработки и публикации.
- .NET Runtime 10 для framework-dependent production-сборки.
- Linux с `systemd` для `install.sh`.
- Telegram bot token и как минимум один Telegram user ID администратора.
- CryptoPay API token и webhook URL необязательны; без них реальные платежи отключены. OpenRouter API key также необязателен.
- PostgreSQL и SQLite не используются.

## Разработка

1. Установите .NET 10 SDK.
2. Скопируйте пример и укажите собственные значения:

   ```sh
   cp env.пример .env
   ```

3. Заполните `BOT_TOKEN` и `ADMIN_IDS`. Не помещайте реальные секреты в `env.пример`, исходный код, логи или репозиторий.
4. Запустите бота:

   ```sh
   dotnet run --project KeyBot.csproj
   ```

5. Сборка без запуска:

   ```sh
   dotnet build KeyBot.csproj
   ```

## Переменные окружения

`BotConfig` читает переменные процесса и, при локальной разработке, `.env` из текущей директории или директории приложения. Уже установленные системные переменные имеют приоритет над `.env`.

| Переменная | Обязательность | Назначение |
|---|---|---|
| `BOT_TOKEN` | Да | Токен Telegram-бота. Хранить только в локальном `.env`, Secret Store или защищённой конфигурации сервиса. |
| `ADMIN_IDS` | Да | Положительные Telegram user ID администраторов через запятую, например `123456789`. |
| `CRYPTOPAY_TOKEN` | Нет | CryptoPay API token. При заполнении включает production-создание invoice и требует `CRYPTOPAY_WEBHOOK_URL`. |
| `CRYPTOPAY_WEBHOOK_URL` | При заданном токене | Публичный HTTPS URL ровно с путём `/webhooks/cryptopay`, например `https://bot.example.com/webhooks/cryptopay`. Направьте его reverse proxy на локальный listener. |
| `CRYPTOPAY_WEBHOOK_LISTEN_URL` | Нет | Локальный HTTP listener, по умолчанию `http://127.0.0.1:8081`. Разрешены только loopback-адреса; TLS завершается на reverse proxy. |
| `ALLOW_TEST_PAYMENTS` | Нет, по умолчанию `false` | `true` без `CRYPTOPAY_TOKEN` включает бесплатную тестовую выдачу ключей. Не включать в production. |
| `OPENROUTER_API_KEY` | Нет | Ключ OpenRouter для AI-команды. |
| `BOT_USERNAME` | Нет | Имя бота без `@`, используется в реферальной ссылке. По умолчанию `bot`. |
| `SUPPORT_USERNAME` | Нет | Имя аккаунта поддержки. |
| `TERMS_URL` | Нет | Базовый URL с условиями; по умолчанию `https://telegra.ph`. |
| `DATA_PATH` | Нет | Путь к JSON-файлу базы; по умолчанию `data/bot_data.json`. Укажите отдельный каталог с ограниченными правами. |

`env.пример` содержит демонстрационные значения. `.env`, файлы базы и логи исключены из Git через `.gitignore`.

## Production на Linux

`install.sh` устанавливает уже собранное приложение как systemd-службу. Скрипт проверяет .NET 10 Runtime и systemd; SQLite/PostgreSQL необязательны и не используются. Потребуется root-доступ для создания системного пользователя и установки службы.

1. Соберите framework-dependent пакет:

   ```sh
   dotnet publish KeyBot.csproj -c Release -r linux-x64 --self-contained false -o publish
   ```

2. Установите сервис из терминала:

   ```sh
   sudo ./install.sh
   ```

   Скрипт запросит токен бота, ID администраторов и, если указан CryptoPay token, публичный HTTPS webhook URL. Секреты вводятся без отображения. Создаётся системный пользователь `merchant-bot`, конфигурация записывается в `/etc/merchant-bot/merchant-bot.env` с правами `0600`, состояние хранится в `/var/lib/merchant-bot/`. Существующие файлы конфигурации при повторном запуске сохраняются.

3. Проверяйте состояние и журнал:

   ```sh
   sudo systemctl status merchant-bot
   sudo journalctl -u merchant-bot -f
   ```

4. Настройте reverse proxy с TLS для маршрута `/webhooks/cryptopay` на `http://127.0.0.1:8081/webhooks/cryptopay`. Не публикуйте порт 8081 напрямую в интернет. В URL из `CRYPTOPAY_WEBHOOK_URL` используйте тот же публичный HTTPS маршрут.

Служба настроена с `Restart=always`, `NoNewPrivileges=true`, `PrivateTmp=true`, `ProtectSystem=strict` и доступом на запись только к каталогу состояния. Установщик не включает бесплатный тестовый режим.

### Обновление

Соберите новую версию в `publish/`, затем повторно запустите `sudo ./install.sh`. Скрипт сохраняет существующий env-файл, заменяет приложение и перезапускает службу; при ошибке установки пытается восстановить предыдущую версию.

### Остановка и резервное копирование

```sh
sudo systemctl stop merchant-bot
sudo install -m 0600 /var/lib/merchant-bot/bot_data.json /root/merchant-bot-backup.json
sudo systemctl start merchant-bot
```

Храните резервные копии за пределами Git и ограничьте к ним доступ. Для восстановления остановите службу, замените JSON-файл, установите владельца `merchant-bot:merchant-bot` и права `0600`, затем запустите службу.

## Безопасность и раскрытые данные

- Не публикуйте `.env`, рабочий JSON-файл, VPN-ключи, пользовательские данные или логи.
- Используйте бота в личном чате: обработка сообщений и callback-ов из групп отключена, чтобы ключи подписок не попадали в групповые сообщения.
- Если секрет был опубликован, немедленно отзовите его у соответствующего провайдера и выпустите новый. Простое удаление файла не делает старый токен недействительным.
- Если опубликованные записи базы содержали активные VPN-ключи, отзовите эти ключи в панели VPN и выдайте новые владельцам.
- Удаление файла новым коммитом не очищает историю Git. Для очистки публичной истории используйте контролируемую перепись истории и согласуйте force-push со всеми, кто клонировал репозиторий.
- Ограничьте доступ к файлу базы и резервным копиям. Данные в JSON не шифруются приложением.
- Не включайте `ALLOW_TEST_PAYMENTS` на рабочем боте: в этом режиме пользователь получает ключ без оплаты.
- CryptoPay webhook не содержит отдельного timestamp/nonce; защита от повторной обработки основана на проверке HMAC подписи и атомарном статусе invoice/order в хранилище. Используйте только один экземпляр бота: JSON-файл не обеспечивает межпроцессные транзакции.
- Изменяйте токены в защищённом конфигурационном файле службы, не добавляя их в Git.

## Устранение неполадок

- **Нет `BOT_TOKEN` или `ADMIN_IDS`:** задайте значения в окружении или `.env` для разработки; production-файл должен находиться в `/etc/merchant-bot/merchant-bot.env`.
- **Не удаётся подключиться к Telegram:** проверьте актуальность токена, исходящий HTTPS-доступ и настройки `proxy.config`.
- **CryptoPay invoice не создаётся:** проверьте `CRYPTOPAY_TOKEN`, `CRYPTOPAY_WEBHOOK_URL`, доступность `https://pay.crypt.bot` и журналы службы. URL должен быть HTTPS и иметь путь `/webhooks/cryptopay`.
- **Платёж не обрабатывается:** убедитесь, что reverse proxy направляет webhook на локальный `127.0.0.1:8081`, сертификат TLS действителен, а идентификатор и сумма invoice соответствуют заказу.
- **AI не отвечает:** проверьте `OPENROUTER_API_KEY`, лимиты провайдера и доступность OpenRouter.
- **Ошибка записи базы:** проверьте `DATA_PATH`, свободное место и права пользователя службы на каталог.
- **Служба перезапускается:** изучите `journalctl -u merchant-bot -n 100`; не публикуйте журнал с диагностикой.

## Структура проекта

```text
.
├── App/
│   ├── BotApplication.cs          — запуск Telegram-клиента, сервисов и маршрутизатора
│   ├── BotCommands.cs             — имена команд и callback-данных
│   ├── CallbackHandlers.cs        — маршрутизация inline-кнопок
│   ├── CommandHandlers.cs         — регистрация команд и обработчик AI
│   ├── CryptoPayWebhookHost.cs    — HTTP endpoint и проверка входящих webhook
│   └── UpdateRouter.cs            — маршрутизация входящих сообщений и callback-ов
├── Configuration/
│   └── BotConfig.cs               — загрузка и проверка переменных окружения
├── Handlers/
│   ├── AboutHandler.cs            — экран «О боте»
│   ├── AdminHandler.cs            — общая логика и панель администратора
│   ├── AdminHandler.Ban.cs        — команды бана и разбана
│   ├── AdminHandler.Grant.cs      — ручная выдача подписки
│   ├── AdminHandler.RemoveSub.cs  — удаление подписок
│   ├── AdminHandler.State.cs      — состояния административного диалога
│   ├── AdminHandler.Stats.cs      — статистика
│   ├── CryptoPayPaymentProcessor.cs — идемпотентная выдача ключей после webhook
│   ├── LanguageHandler.cs         — выбор языка
│   ├── MessageHandler.cs          — профиль, поддержка и сообщения
│   ├── PaymentHandler.cs          — выбор тарифа и обработка тестовой выдачи
│   ├── ReferralHandler.cs         — реферальное меню и регистрация
│   └── SubscriptionHandler.cs     — просмотр и заморозка подписок
├── Locale/
│   ├── Loc.Admin.cs               — тексты администратора
│   ├── Loc.Buttons.cs             — тексты кнопок
│   ├── Loc.Misc.cs                — общие тексты
│   ├── Loc.Payment.cs             — тексты оплаты
│   ├── Loc.Support.cs             — тексты поддержки
│   ├── Loc.Welcome.cs             — приветствие и описание
│   └── UserLanguage.cs            — доступные языки
├── Models/
│   ├── KeyModel.cs                — модель подписочного ключа
│   ├── CryptoPayInvoice.cs        — созданный CryptoPay invoice
│   ├── CryptoPayInvoiceUpdate.cs  — проверенные поля webhook-события
│   ├── PendingPayment.cs          — проекция ожидающего/обработанного платежа
│   ├── TariffModel.cs             — модели и список тарифов
│   ├── TransactionModel.cs        — модель транзакции
│   └── UserModel.cs               — модель пользователя
├── Referral/
│   ├── IReferralService.cs        — контракт реферального сервиса
│   ├── ReferralModels.cs          — модели реферальной статистики
│   └── ReferralService.cs         — регистрация рефералов и учёт бонусов
├── Services/
│   ├── DatabaseManager.cs         — загрузка, сохранение и автосохранение JSON
│   ├── HttpClientFactory.cs       — создание HTTP-клиента с поддержкой proxy.config
│   ├── KeyService.cs              — создание и изменение подписок
│   ├── OpenRouterService.cs       — интеграция с OpenRouter
│   ├── PaymentService.cs          — создание CryptoPay invoice-ссылок
│   ├── TransactionService.cs      — учёт транзакций
│   ├── UIService.cs               — клавиатуры Telegram
│   └── UserService.cs             — пользователи, языки и баны
├── Utils/
│   ├── DatabaseExtensions.cs      — JSON-сериализация базы
│   └── UpdateRateLimiter.cs       — глобальный и пользовательский лимит обновлений
├── Core-program.cs                — точка входа приложения
├── Data_Models.cs                 — модели данных JSON-хранилища
├── Data_base.cs                   — корневой объект базы данных
├── dotnet-tools.json              — локальные инструменты .NET
├── Global_Usings.cs               — глобальные using-директивы
├── JsonSetup.cs                   — контекст сериализации System.Text.Json
├── KeyBot.csproj                  — настройки .NET-проекта и зависимости
├── ProxySettings.cs               — чтение конфигурации прокси
├── README.md                      — документация по проекту и развёртыванию
├── env.пример                     — безопасный пример переменных окружения
├── install.sh                     — установка Linux/systemd
└── proxy.config                   — настройки HTTP-прокси
```
