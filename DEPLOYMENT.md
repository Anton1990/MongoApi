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

## Полезные ссылки

- Swagger UI: `http://localhost:30080/swagger`
- GitHub Actions: `github.com/Anton1990/MongoApi/actions`
- GHCR образы: `github.com/Anton1990?tab=packages`
- Runner статус: `github.com/Anton1990/MongoApi/settings/actions/runners`
