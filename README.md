# PhotoSite

Веб-портфолио фотографа с публичной галереей и административной панелью. Проект состоит из серверного приложения на Blazor, REST API и PostgreSQL; фотографии хранятся в файловом хранилище.

## Возможности

- Публичные страницы: главная, портфолио, категории и альбомы, услуги, информация о фотографе и отзывы.
- Управление альбомами, категориями, обложками и фотографиями из панели администратора.
- Загрузка фотографий и управление изображениями главной страницы и раздела «Обо мне».
- Публикация и модерация отзывов.
- JWT-аутентификация для защищённых операций API.

## Технологии

- **Backend:** ASP.NET Core Web API, .NET 8, Entity Framework Core, PostgreSQL.
- **Frontend:** Blazor Server, Razor Components, MudBlazor.
- **Развёртывание:** Docker Compose; для VPS — Nginx и Certbot.

## Быстрый запуск через Docker

Нужны Docker и Docker Compose.

1. Создайте корневой `.env` на основе шаблона:

   ```powershell
   Copy-Item deploy/.env.example .env
   ```

   В Bash используйте `cp deploy/.env.example .env`. Перед запуском измените значения `POSTGRES_PASSWORD` и `JWT_KEY` на собственные длинные случайные секреты.

2. Соберите и запустите приложение:

   ```console
   docker compose up -d --build
   ```

3. Откройте сайт: <http://localhost:5080>. API доступен на <http://localhost:5081>.

Остановить контейнеры, сохранив данные:

```console
docker compose down
```

PostgreSQL и загруженные изображения сохраняются в Docker volumes `postgres_data` и `api_storage`.

## Локальная разработка

Нужны .NET 8 SDK и PostgreSQL. Для API задайте строку подключения и параметры JWT через переменные окружения или .NET User Secrets:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
Jwt__Issuer
Jwt__Audience
Jwt__ExpireMinutes
```

Пример структуры API-конфигурации приведён в [`PhotoSite.Api/appsettings.example.json`](PhotoSite.Api/appsettings.example.json). Для веб-приложения задайте `Api__BaseUrl`; шаблон находится в [`PhotoSite.Web/appsettings.example.json`](PhotoSite.Web/appsettings.example.json).

В режиме Development API предоставляет Swagger UI по адресу `/swagger`. Миграции базы данных автоматически применяются при запуске API в Production; в Development схему базы данных нужно подготовить отдельно.

## Структура проекта

```text
PhotoSite.Api/    REST API, модели, контроллеры и миграции EF Core
PhotoSite.Web/    Blazor Server интерфейс и административная панель
deploy/           Инструкции для VPS и конфигурация Nginx
docker-compose.yml
```

## Конфигурация и безопасность

- Не добавляйте реальные пароли, JWT-ключи, `.env` и локальные `appsettings*.json` в Git. Используйте шаблоны конфигурации и задавайте секреты локально или через окружение сервера.
- Перед публикацией задайте собственные значения `POSTGRES_PASSWORD` и `JWT_KEY`; значения из `.env.example` предназначены только для примера.
- В текущей модели веб-интерфейс считает пользователя администратором, если API принимает его JWT; отдельная проверка роли администратора в клиенте не выполняется. Учитывайте это при настройке доступа и создании учётных записей.

Подробные инструкции по VPS, Nginx, HTTPS и настройке секретов: [руководство по развёртыванию](deploy/README.md) и [заметки по конфигурации секретов](README.secrets.md).
