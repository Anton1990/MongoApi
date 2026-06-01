# MongoApi — Архитектура и деплой

## Стек технологий

| Компонент | Технология | Версия |
|---|---|---|
| API | ASP.NET Core Web API | .NET 8 |
| База данных | MongoDB Replica Set | 7.x |
| Контейнеризация | Docker | - |
| Оркестрация | Kubernetes (Docker Desktop) | - |
| CI/CD | GitHub Actions | - |
| Реестр образов | GitHub Container Registry (GHCR) | - |

---

## Архитектура

```
GitHub Repository
       │
       │  git push → master
       ▼
GitHub Actions Pipeline
  ├── Job 1: Build & Test     (ubuntu-latest)
  ├── Job 2: Docker Build     (ubuntu-latest → GHCR)
  └── Job 3: Deploy           (self-hosted runner → K8s)
                                        │
                                        ▼
                            Kubernetes (Docker Desktop)
                            namespace: mongoapi
                            ├── Deployment: mongo-api
                            │     └── Pod: ghcr.io/anton1990/mongo-api:sha
                            ├── StatefulSet: mongo (5 реплик)
                            │     ├── Pod: mongo-0 (Primary, priority=2)
                            │     ├── Pod: mongo-1 (Secondary)
                            │     ├── Pod: mongo-2 (Secondary)
                            │     ├── Pod: mongo-3 (Secondary)
                            │     └── Pod: mongo-4 (Secondary)
                            ├── Service: mongo-headless (ClusterIP: None)
                            ├── Service: mongo-api (NodePort :30080)
                            ├── ConfigMap: api-config
                            └── CronJob: mongo-backup (каждую ночь в 02:00)
```

---

## Почему такие решения

### MongoDB Replica Set (5 нод), а не standalone
- **Отказоустойчивость**: при падении Primary — автоматические выборы нового
- **Читающие реплики**: Secondary ноды можно использовать для read-only запросов
- **Транзакции**: multi-document ACID транзакции требуют replica set
- **Минимум для кворума**: нечётное число (3, 5) — нет split-brain при сетевых разделениях
- **Optimistic Concurrency**: в проекте реализован через поле `Version` на документах

### StatefulSet, а не Deployment для MongoDB
- `Deployment` создаёт поды с случайными именами → MongoDB не может стабильно адресовать ноды
- `StatefulSet` даёт: стабильные имена (`mongo-0..4`), стабильные DNS, упорядоченный старт/стоп
- Каждый под получает свой `PersistentVolumeClaim` → данные не теряются при рестарте

### Headless Service (clusterIP: None)
- Обычный Service балансирует трафик между подами → неприемлемо для БД
- Headless Service только создаёт DNS-записи для каждого пода
- Позволяет MongoDB Driver подключаться к конкретным нодам: `mongo-0.mongo-headless`, `mongo-1.mongo-headless`

### GHCR вместо Docker Hub
- Встроен в GitHub — не нужен отдельный аккаунт
- `GITHUB_TOKEN` создаётся автоматически при каждом run — нет необходимости хранить секреты
- Приватные образы бесплатно
- Лимиты pull отсутствуют для собственных образов

### Self-hosted Runner для деплоя
- Kubernetes (Docker Desktop) работает локально на `127.0.0.1`
- GitHub-hosted runner (ubuntu-latest) не имеет доступа к локальной машине
- Self-hosted runner запускается на той же машине → имеет доступ к kubeconfig и kubectl

---

## Структура проекта

```
MongoApi/
├── .github/
│   └── workflows/
│       └── ci-cd.yml              # CI/CD pipeline
├── k8s/
│   ├── 00-namespace.yaml          # Namespace: mongoapi
│   ├── 02-mongo-headless-svc.yaml # Headless Service для StatefulSet
│   ├── 03-mongo-statefulset.yaml  # MongoDB 5 нод + PVC
│   ├── 04-mongo-init-job.yaml     # Инициализация replica set rs0
│   ├── 05-api-configmap.yaml      # Connection string + настройки
│   ├── 06-api-deployment.yaml     # API Deployment
│   ├── 07-api-service.yaml        # NodePort :30080
│   └── 08-mongo-backup-cronjob.yaml # Бэкап по расписанию
├── Controllers/
├── Models/
├── Services/
├── Infrastructure/
│   ├── MongoDbContext.cs
│   ├── DatabaseInitializer.cs     # Создание индексов при старте
│   └── ConcurrencyException.cs    # Optimistic concurrency exception
├── Dockerfile                     # Multi-stage build
└── docker-compose.yml             # Альтернативный запуск без K8s
```

---

## Предварительные требования

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) с включённым Kubernetes
- [kubectl](https://kubernetes.io/docs/tasks/tools/)
- [Git](https://git-scm.com/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [gh CLI](https://cli.github.com/) (для работы с GitHub)
- GitHub аккаунт + репозиторий

---

## Шаг 1 — Включить Kubernetes в Docker Desktop

```
Docker Desktop → Settings → Kubernetes → Enable Kubernetes → Apply & Restart
```

Проверить:
```bash
kubectl cluster-info
# Kubernetes control plane is running at https://kubernetes.docker.internal:6443
```

---

## Шаг 2 — Клонировать и настроить проект

```bash
git clone https://github.com/Anton1990/MongoApi.git
cd MongoApi
```

Заменить `anton1990` на свой GitHub username в файле `k8s/06-api-deployment.yaml`:
```yaml
image: ghcr.io/YOUR_USERNAME/mongo-api:latest
```

---

## Шаг 3 — Настроить self-hosted GitHub Actions Runner

Перейти: `github.com/YOUR_USERNAME/MongoApi → Settings → Actions → Runners → New self-hosted runner`

Выбрать **Windows x64**, выполнить команды из страницы GitHub в PowerShell:

```powershell
# Создать директорию
mkdir C:\actions-runner-mongoapi; cd C:\actions-runner-mongoapi

# Скачать runner (версию берём со страницы GitHub)
curl -L -o actions-runner.zip https://github.com/actions/runner/releases/download/v2.XXX.X/actions-runner-win-x64-2.XXX.X.zip

# Распаковать
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory("$PWD/actions-runner.zip", "$PWD")

# Настроить (токен берётся со страницы GitHub, живёт ~1 час)
./config.cmd --url https://github.com/YOUR_USERNAME/MongoApi --token YOUR_TOKEN --unattended --name "mongoapi-runner"

# Запустить (оставить терминал открытым или настроить как сервис)
./run.cmd
```

Проверить что runner появился на GitHub: статус **Idle** (зелёный).

---

## Шаг 4 — Первый деплой MongoDB вручную

CI/CD деплоит только API. MongoDB StatefulSet применяется один раз вручную:

```bash
# Применяем по порядку
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/02-mongo-headless-svc.yaml
kubectl apply -f k8s/03-mongo-statefulset.yaml

# Ждём пока все 5 подов поднимутся (может занять 1-2 минуты)
kubectl wait --for=condition=ready pod -l app=mongo --namespace mongoapi --timeout=180s

# Инициализируем replica set
kubectl apply -f k8s/04-mongo-init-job.yaml
kubectl wait --for=condition=complete job/mongo-init --namespace mongoapi --timeout=60s

# Применяем ConfigMap
kubectl apply -f k8s/05-api-configmap.yaml

# Запускаем бэкапы
kubectl apply -f k8s/08-mongo-backup-cronjob.yaml
```

---

## Шаг 5 — Первый деплой API через CI/CD

Сделать любой коммит и запушить в `master`:

```bash
git add .
git commit -m "Initial deployment"
git push origin master
```

Pipeline запустится автоматически. Наблюдать за выполнением:
```
github.com/YOUR_USERNAME/MongoApi → Actions
```

Три job выполнятся последовательно:
1. **Build & Test** — `dotnet build` + `dotnet test`
2. **Docker Build & Push** — сборка образа → push в `ghcr.io/YOUR_USERNAME/mongo-api:SHA`
3. **Deploy to Kubernetes** — `kubectl apply` + `kubectl set image` + rollout status

---

## Шаг 6 — Проверить деплой

```bash
# Статус всех ресурсов в namespace
kubectl get all -n mongoapi

# Логи API
kubectl logs -n mongoapi -l app=mongo-api --tail=50

# Открыть Swagger UI
# http://localhost:30080/swagger
```

---

## CI/CD Pipeline — подробно

```yaml
# Триггер: push в master или PR
on:
  push:    { branches: [master] }
  pull_request: { branches: [master] }
```

| Job | Runner | Триггер | Действие |
|---|---|---|---|
| Build & Test | ubuntu-latest | push + PR | dotnet restore → build → test |
| Docker Build & Push | ubuntu-latest | только push | docker buildx → push в GHCR |
| Deploy | self-hosted (Windows) | только master push | kubectl apply → set image → rollout |

**Важно**: PR запускает только Build & Test — деплой не происходит.

---

## Конфигурация через ConfigMap

Настройки MongoDB переопределяются через переменные окружения (не appsettings.json).

.NET читает `MongoDbSettings__ConnectionString` как `MongoDbSettings:ConnectionString` — двойной `__` является разделителем секций.

```yaml
# k8s/05-api-configmap.yaml
MongoDbSettings__ConnectionString: >-
  mongodb://mongo-0.mongo-headless:27017,.../?replicaSet=rs0
```

Это позволяет иметь разные connection string для:
- Локальной разработки (`appsettings.json` → `localhost:27017,...`)
- Kubernetes (`ConfigMap` → `mongo-0.mongo-headless:27017,...`)

---

## Резервное копирование

CronJob запускает `mongodump` каждую ночь в 02:00:

```
# k8s/08-mongo-backup-cronjob.yaml
schedule: "0 2 * * *"   # prod
schedule: "* * * * *"   # тест — каждую минуту
```

Бэкап пишется в `/mnt/mongo-backups` (hostPath на машине с K8s).

**Восстановление:**
```bash
# Скопировать бэкап внутрь пода
kubectl cp ./backup_20240601_020000.gz mongoapi/mongo-0:/tmp/

# Восстановить
kubectl exec -n mongoapi mongo-0 -- \
  mongorestore --gzip --archive=/tmp/backup_20240601_020000.gz
```

---

## Ресурсы (requests/limits)

| Pod | Memory request | Memory limit | CPU request | CPU limit |
|---|---|---|---|---|
| mongo (x5) | 256Mi | 512Mi | 100m | 500m |
| mongo-api | 128Mi | 256Mi | 100m | 500m |
| **Итого** | **1.4Gi** | **2.8Gi** | **600m** | **3 cores** |

Docker Desktop по умолчанию выделяет 2Gi RAM — при нехватке увеличить:
```
Docker Desktop → Settings → Resources → Memory: 4GB+
```

---

## Типичные команды

```bash
# Статус всех ресурсов
kubectl get all -n mongoapi

# Перезапустить API (rolling update без изменения образа)
kubectl rollout restart deployment/mongo-api -n mongoapi

# Зайти в MongoDB pod
kubectl exec -it -n mongoapi mongo-0 -- mongosh

# Статус replica set
kubectl exec -n mongoapi mongo-0 -- mongosh --eval "rs.status()"

# Логи бэкапа
kubectl logs -n mongoapi -l job-name=mongo-backup --tail=20

# Удалить всё (namespace + все ресурсы внутри)
kubectl delete namespace mongoapi
```

---

## Optimistic Concurrency (особенность проекта)

Модель `Product` содержит поле `Version`:
```csharp
[BsonElement("version")]
public int Version { get; set; } = 0;
```

При UPDATE клиент передаёт версию которую получил при чтении. Если версия в БД не совпадает — кто-то обновил документ раньше → `409 Conflict`.

Это защищает от lost update в конкурентной среде без пессимистичных блокировок.

---

## Разбор реальных ошибок при деплое

### 1. `error CS1061: 'Product' does not contain a definition for 'Category'`

**Где:** GitHub Actions → Job: Build & Test
**Причина:** `DatabaseInitializer.cs` обращался к `p.Category`, а поле в модели называется `p.CategoryId`. Ошибка не видна локально если проект не собирался после переименования поля.
**Урок:** CI/CD выявляет такие ошибки раньше, чем они попадают в прод. Именно для этого существует Job "Build & Test".
**Исправление:** `p.Category` → `p.CategoryId`

---

### 2. `warning CS8618: Non-nullable property 'Payload'`

**Где:** GitHub Actions → Job: Build & Test
**Причина:** `BsonDocument Payload` объявлен как non-nullable, но не инициализирован в конструкторе. В `Nullable` режиме компилятор требует явного указания nullable типов.
**Урок:** Включённый `<Nullable>enable</Nullable>` в .csproj заставляет явно думать о null-safety на уровне типов.
**Исправление:** `BsonDocument Payload` → `BsonDocument? Payload`

---

### 3. `buildx failed: Cache export is not supported for the docker driver`

**Где:** GitHub Actions → Job: Docker Build & Push
**Причина:** `type=gha` кэш требует `docker-container` driver для buildx. GitHub Actions runner по умолчанию использует `docker` driver, который не поддерживает экспорт кэша.
**Урок:** `docker buildx` имеет несколько драйверов (`docker`, `docker-container`, `kubernetes`). `docker-container` — изолированный BuildKit daemon в контейнере, он поддерживает все возможности включая кэш.
**Исправление:** Добавить шаг `docker/setup-buildx-action@v3` до сборки — он создаёт builder с `docker-container` driver.

---

### 4. `Unable to connect to the server: dial tcp 127.0.0.1:57012`

**Где:** GitHub Actions → Job: Deploy
**Причина:** Self-hosted runner использовал `kubectl` с активным контекстом `minikube` (порт 57012), но minikube был остановлен. Kubernetes реально работал в Docker Desktop на другом порту (6443).
**Урок:** `kubectl config get-contexts` показывает все доступные кластеры. Активный контекст (`*`) определяет куда идут команды. Разные инструменты (minikube, Docker Desktop, k3d) создают разные контексты в `~/.kube/config`.
**Исправление:** Добавить `kubectl config use-context docker-desktop` в начало deploy job.

---

### 5. `ParserError: Missing expression after unary operator '--'`

**Где:** GitHub Actions → Job: Deploy
**Причина:** Self-hosted runner на Windows использует PowerShell как shell по умолчанию. В PowerShell `\` не является символом переноса строки (это разделитель путей). Команды `kubectl` с многострочным `\` синтаксисом не парсятся.
**Урок:** GitHub Actions runner наследует shell операционной системы. Linux → bash, Windows → PowerShell. Bash-синтаксис (`\` для переноса) несовместим с PowerShell.
**Исправление:** `defaults: run: shell: bash` на уровне job — принудительно использует Git Bash на Windows.

---

### 6. `deployments.apps "mongo-api" not found`

**Где:** GitHub Actions → Job: Deploy → шаг `kubectl set image`
**Причина:** Шаг `kubectl apply` не включал `06-api-deployment.yaml` и `07-api-service.yaml`. `kubectl set image` пытался обновить deployment который никогда не был создан.
**Урок:** `kubectl set image` — это команда обновления СУЩЕСТВУЮЩЕГО ресурса. Если ресурс не создан через `kubectl apply`, `set image` упадёт с NotFound. Правильный порядок: сначала `apply` (idempotent создание/обновление), потом `set image`.
**Исправление:** Добавить `kubectl apply -f k8s/06-api-deployment.yaml` и `07-api-service.yaml` в шаг apply.

---

### 7. `unauthorized: HEAD ghcr.io/...` + `invalid reference format: repository name must be lowercase`

**Где:** Kubernetes → Pod → ImagePullBackOff
**Причина 1:** GHCR пакет приватный по умолчанию. Kubernetes не имеет credentials для его скачивания.
**Причина 2:** `${{ github.repository_owner }}` возвращает `Anton1990` с заглавной буквой. Docker требует lowercase имена образов: `ghcr.io/Anton1990/...` → невалидно.
**Урок:** Docker image reference регламентирован OCI Distribution Spec — имя репозитория должно быть lowercase. `github.repository_owner` возвращает отображаемое имя пользователя, которое может иметь любой регистр.
**Исправление 1:** Создать `imagePullSecret` в K8s с `GITHUB_TOKEN` и прописать `imagePullSecrets` в Deployment.
**Исправление 2:** Хардкод lowercase: `IMAGE_NAME: anton1990/mongo-api`.

---

### 8. `error: timed out waiting for the condition` (rollout status)

**Где:** GitHub Actions → Job: Deploy → шаг `kubectl rollout status`
**Причина:** Новый под API не мог стать Ready в течение 120 секунд. Причина — контейнер падал при старте потому что `DatabaseInitializer.InitializeAsync()` бросал исключение при попытке подключиться к MongoDB (которая ещё не была готова).
**Урок:** Приложения в Kubernetes должны быть устойчивы к тому, что зависимости (БД, другие сервисы) могут быть недоступны при старте. Это называется "graceful degradation at startup". Kubernetes перезапустит под, но падение из-за недоступной зависимости — антипаттерн.
**Исправление:** Обернуть `DatabaseInitializer.InitializeAsync()` в `try/catch` — приложение стартует даже если MongoDB недоступна, индексы создадутся при следующем рестарте.

---

### 9. MongoDB поды `0/1 Running` — StatefulSet rolling update застрял

**Где:** Kubernetes → StatefulSet mongo
**Причина:** Readiness probe `mongosh --eval "db.adminCommand('ping')"` падала на всех подах. При изменении spec StatefulSet делает rolling update начиная с наибольшего ordinal (mongo-4). Правило: следующий под обновляется только после того как предыдущий стал Ready. Если mongo-4 не становится Ready — вся цепочка останавливается. Классическая проблема "chicken-and-egg".
**Урок:** StatefulSet rolling update имеет важное отличие от Deployment: строгий порядок и ожидание Ready перед следующим шагом. Неправильная readiness probe может заблокировать rolling update навсегда. Всегда проверяй probe локально перед деплоем.
**Исправление:** Принудительное удаление всех подов (`kubectl delete pod`) — StatefulSet пересоздаёт их сразу с новым spec. Поменять probe с `mongosh exec` на `tcpSocket:27017` — более надёжна, не зависит от состояния replica set.

---

### 10. `Name or service not known: mongo-0.mongo-headless`

**Где:** API pod → MongoDB Driver
**Причина:** Поды MongoDB были `0/1 Running` (не Ready). Для headless Service DNS регистрирует только Ready поды в endpoints. Пока ни один под не прошёл readiness probe — DNS имена не резолвились.
**Урок:** В Kubernetes DNS для headless Service (`clusterIP: None`) возвращает IP только Ready подов. Для StatefulSet подов есть исключение — индивидуальные DNS записи (`pod.svc.namespace`) создаются независимо от готовности. Но это работает только если поды ВООБЩЕ запущены. Если probe блокирует переход в Ready и приложение пытается подключиться к DNS имени — получаем "Name not known".
**Исправление:** После исправления readiness probe и пересоздания подов — DNS заработал.

---

### 11. Replica set не инициализирован → API не может писать в MongoDB

**Где:** API → MongoDB Driver → CreateIndexes
**Причина:** MongoDB запущена в режиме replica set (`--replSet rs0`), но `rs.initiate()` не был вызван. В неинициализированном replica set нет Primary ноды. MongoDB Driver требует Primary для операций записи. `DatabaseInitializer` пытается создать индексы (write operation) → timeout.
**Урок:** `mongod --replSet rs0` только запускает MongoDB в режиме ожидания replica set конфигурации. Без `rs.initiate()` кластер не функционирует. Init Job — это одноразовая операция которую нужно выполнить один раз после первого деплоя StatefulSet.
**Исправление:** Запустить `04-mongo-init-job.yaml` вручную после первого деплоя MongoDB.

---

## Полезные ссылки

- Swagger UI: `http://localhost:30080/swagger`
- GitHub Actions: `github.com/Anton1990/MongoApi/actions`
- GHCR образы: `github.com/Anton1990?tab=packages`
- Runner статус: `github.com/Anton1990/MongoApi/settings/actions/runners`
