## Секреты и конфиги (как правильно хранить)

### Что НЕ коммитим
- Пароли/строки подключения к БД
- JWT key (подпись токенов)
- Сертификаты (`.pfx`, `.pem`, `.key`)
- `PhotoSite.Api/appsettings.json` (в нём секреты)

Это уже учтено в `.gitignore`.

### Примеры конфигов

#### API
- Шаблон: `PhotoSite.Api/appsettings.example.json`
- Реальный файл: `PhotoSite.Api/appsettings.json` (создаёшь у себя локально/на сервере, **в git не попадает**)

Минимально нужные значения:
- `ConnectionStrings:DefaultConnection`
- `Jwt:Key` (длинный секрет)
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpireMinutes`

#### Web
- Шаблон: `PhotoSite.Web/appsettings.example.json`
- Реальный файл: `PhotoSite.Web/appsettings.json`

Нужно:
- `Api:BaseUrl`

### Рекомендованный способ (ENV)
В проде задавайте значения через переменные окружения:
- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__ExpireMinutes`
- `Api__BaseUrl` (для Web)

### Генерация JWT Key
Используйте длинную случайную строку (например 32–64 байта), храните как secret.

