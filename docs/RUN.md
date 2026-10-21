# Запуск: локально, тест, прод

Две части, одна БД (Postgres):

| | Папка / репозиторий | Локальный адрес | Docker-образ |
|---|---|---|---|
| Публичная (ЕСИ, инциденты) | `01 публичная часть/data_registry_public` · DPA_dataRegisty_public | http://localhost:5201 | `sanjar883448/dpa_registry_public:test` |
| Локальная (админка) | `02 локальная часть/data_registry_local` · DPA_dataRegisty_local | http://localhost:5202 | `sanjar883448/dpa_registry_local:test` |

## Режимы и настройки

Режим задаёт переменная `ASPNETCORE_ENVIRONMENT`:

| Режим | Где | Что особенного |
|---|---|---|
| `Development` | Visual Studio / `dotnet run` (выставляется из `launchSettings.json`) | тестовая БД; тестовый вход без пароля / без ЕСИ; эмулятор Минюста |
| `Test` | тестовый сервер, docker | тестовая БД; реальный Минюст; тестовый вход ЕСИ включён, вход в админку без пароля выключен |
| `Production` | прод, docker (значение по умолчанию в контейнере) | пока как `Test`; тестовые входы выключены всегда |

Файлы настроек в корне каждой части:

* `appsettings.json` — общее для всех режимов (адреса ЕСИ/Минюста, LDAP, флаги, логи).
* `appsettings.Development.json` / `appsettings.Test.json` / `appsettings.Production.json` — отличия режима
  (строка подключения, redirectUrl ЕСИ, флаги тестового входа).

Любое значение перекрывается переменной окружения через `__`, например
`-e ConnectionStrings__DefaultConnection="Host=...;Password=..."`. Секреты на сервере удобнее задавать так.
Если строки подключения нет, приложение не стартует и пишет, что именно не задано.

## Локально

```bash
cd "02 локальная часть/data_registry_local"
npm install
npm run build:css
dotnet run --launch-profile http
```

Публичная часть запускается так же из своей папки. Из Visual Studio — F5, профиль `http`.
Обе части можно держать запущенными одновременно (порты 5201 и 5202).

* Админка: на странице входа есть кнопка «Тестовый вход» (под пользователем `Features:DevLoginUserId`).
* Публичная: в шапке есть «Тестовый вход», организация берётся из эмулятора Минюста.
* Логи пишутся в `{папка проекта}/logs/registry_local|registry_public/`, материалы — в `{папка проекта}/data/`.

## Сервер (Test / Production)

CI собирает образы при push в ветку `test` (GitHub Actions → Docker Hub). На сервере (порты `-p` — пример, под вашу схему nginx):

```bash
sudo mkdir -p /logs/registry_local /logs/registry_public /srv/dpa/data
sudo chown -R 1654 /logs/registry_local /logs/registry_public /srv/dpa/data    # 1654 — пользователь приложения в образе

docker pull sanjar883448/dpa_registry_local:test
docker run -d --name dpa_registry_local --restart unless-stopped \
  -e ASPNETCORE_ENVIRONMENT=Test \
  -p 8082:8080 \
  -v /logs:/logs \
  -v /srv/dpa/data:/app/data \
  --log-opt max-size=20m --log-opt max-file=5 \
  sanjar883448/dpa_registry_local:test

docker pull sanjar883448/dpa_registry_public:test
docker run -d --name dpa_registry_public --restart unless-stopped \
  -e ASPNETCORE_ENVIRONMENT=Test \
  -p 8081:8080 \
  -v /logs:/logs \
  --log-opt max-size=20m --log-opt max-file=5 \
  sanjar883448/dpa_registry_public:test
```

Для прода: `-e ASPNETCORE_ENVIRONMENT=Production` (или переменную не задавать вовсе).

Что где лежит на сервере:

| Путь на хосте | Что |
|---|---|
| `/logs/registry_local/{auth,integration,system,errors}` | логи админки (см. `docs/LOGGING.md`) |
| `/logs/registry_public/{auth,integration,system,errors}` | логи публичной части |
| `/srv/dpa/data/inspection_materials` | файлы материалов проверок |
| `/srv/dpa/data/sanction_materials` | файлы материалов протоколов |

* Перед контейнерами стоит nginx с TLS. Приложение берёт схему и IP клиента из `X-Forwarded-*`
  (`Hosting:ForwardedHeaders`); редиректа на https внутри контейнера нет.
* Время в приложении и логах бишкекское (`TZ=Asia/Bishkek` в образе).
* Обновление: `docker pull ...`, затем `docker rm -f <имя>` и тот же `docker run`. Данные и логи лежат в томах и сохраняются.

## ЕСИ (публичная часть)

Секция `Esi` в `appsettings.{режим}.json`: адреса `/connect/authorize|token|userinfo`, `ClientId`, `ClientSecret`, `RedirectUrl`, `Scope`.
Поток: Authorization Code + PKCE (S256), секрет передаётся в Basic, `state` проверяется (защита от подмены ответа).

* Test и Production сейчас ходят в DEV-контур `https://dev-esia.tunduk.kg`, клиент `Record_Owners`.
* `RedirectUrl` должен совпадать с адресом, зарегистрированным в ЕСИ для клиента, символ в символ (`https://<домен публичной части>/esi/auth`).
* Боевой контур: заменить адреса на `https://esia.tunduk.kg/...`, `ClientId`/`ClientSecret` на боевые
  (лучше переменными `Esi__ClientId`, `Esi__ClientSecret`) и `RedirectUrl`.
* Если ЕСИ не передаёт `organization_tin`, добавить в `Esi:Scope` значение, которое ЕСИ согласует для данных организации (например `organizations`).
* Ход входа виден в `/logs/registry_public/integration` (токен, userinfo, ошибки ЕСИ) и `/logs/registry_public/auth`.
* DEV-контур ЕСИ доступен не из любой сети: с рабочей машины разработчика он отвечает 403, проверять вход нужно с сервера.

## SQL

Скрипты изменений БД лежат в `02 локальная часть/data_registry_local/sql/` и применяются вручную (`psql -f`) по дате в имени.
Публичная часть при старте сама добавляет недостающие колонки и справочник секторов (это безопасно при повторном запуске).
