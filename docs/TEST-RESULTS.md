# Результаты тестирования MarketFlow

## 1. Локальное тестирование

**Команда запуска:**

```bash
dotnet test MarketFlow.slnx --configuration Release
```

**Среда тестирования:**
- .NET SDK 10
- .NET Runtime 10.0.12
- Конфигурация: Release
- Тестовый фреймворк: xUnit

**Результаты:**

| Показатель | Результат |
|---|---:|
| Всего тестов | 49 |
| Успешно | 49 |
| С ошибками | 0 |
| Пропущено | 0 |
| Длительность тестов | 3,3 секунды |

**Статус: УСПЕШНО**

## 2. Предупреждения сборки

При сборке обнаружены два предупреждения об устаревших API:

- ASPDEPR004 — использование WebHostBuilder.
- ASPDEPR008 — использование TestServer(IWebHostBuilder).

Предупреждения относятся к `OrdersHubIntegrationTests.cs` и не повлияли на успешное выполнение тестов.

## 3. GitHub Actions CI

Успешный запуск CI:

https://github.com/Salvatore1864s/MarketFlow/actions/runs/43864289445

