# HTTP-клиент Mirai.Space — предложение по API

Статус: **design proposal + реализация**. Код в `Mirai.Space.Extensions.Http*` уже написан;
документ ниже фиксирует исходный дизайн, а актуальные отличия после ревью перечислены в
разделе «Реализовано после ревью» сразу после этого абзаца.

Источники:
- переписка `chat-Http Client Abstraction Design (1).txt` (5383 строки, 32 хода);
- текущее состояние репозитория `Z:\Work\Mirai.Space\Mirai.Space` (проверено на месте).

Всё, что в разделах «Проверено», проверено локально компиляцией/чтением SDK-пакетов, а не по памяти.

---

## Реализовано после ревью

Документ ниже — исходное предложение. Код после ревью отличается в следующем:

- JSON — часть базового пакета: `Mirai.Space.Extensions.Http.Json` удалён, `JsonRequestSerializer`
  и его DI-расширения живут в `Mirai.Space.Extensions.Http`. JSON зарегистрирован по умолчанию;
  `ConfigureJson(...)` нужен только для настройки (перегрузки с options/context/configure); `ConfigureJson()` без
  параметров и все `AddJsonSerializer(...)` удалены — JSON уже включён по умолчанию, настройка идёт через
  `ConfigureJson(...)` на конкретном клиенте (глобальный fallback заменяется регистрацией `IRequestSerializer` в DI).
- Публичные типы приведены к семейству `Request*`: `RequestResponse`, `RequestResponse<T>`,
  `RequestException`, `IRequestErrorHandler`, `DefaultRequestErrorHandler`. `Api*`-типов в HTTP-слое нет.
- Владение потоком: `StreamedApiResponse` удалён. Потоковые методы принимают `Stream destination`
  (владелец — вызывающий код), копируют тело и возвращают `RequestResponse` со статусом/заголовками;
  `HttpResponseMessage` диспозится внутри сервиса.
- Ошибка одна: `RequestException` несёт `StatusCode`/`Method`/`RequestUri`; тела ответа в исключении
  нет. `IRequestErrorHandler.HandleAsync(RequestException, ct)` только обрабатывает (может
  залогировать/бросить); исключение создаёт `RequestService`. Разбор ProblemDetails отложен
  (m02134): `RequestProblemDetails`, `IRequestProblemDetailsReader` и `AddProblemDetailsReader`
  удалены до следующего ревью API.
- Авторизация удалена из v1 полностью: нет `IApiAuthorizationProvider`, `ApiAuthorizationHandler`,
  `IApiReauthenticator`, `IApiReauthenticationHandler`, `IApiSession`.
- `IRequestSerializer.ContentType` (не `MediaType`); сериализаторы не проверяют media type ответа.
  JSON/MessagePack выбираются на конкретном клиенте через `ConfigureJson(...)`/`UseMessagePack()`; глобальных
  `AddJsonSerializer(...)`/`AddRequestErrorHandler(...)` нет — JSON включён по умолчанию, fallback
  заменяется прямой регистрацией `IRequestSerializer`/`IRequestErrorHandler` в DI. `UseSerializer<TSerializer>()`/
  `UseSerializer(instance)` остаётся общим хуком для кастомного сериализатора; `ConfigureJson`/`UseMessagePack` —
  удобные обёртки над ним. Отдельного глобального `AddMessagePackSerializer()` нет: MessagePack всегда
  opt-in на конкретном клиенте.
- DI: один `ServiceCollectionExtensions` на сборку (extension-блоки для `IServiceCollection`
  и `IHttpClientBuilder`); `AddRequestService<TClient>()` возвращает `IHttpClientBuilder`, регистрирует
  keyed `IRequestService` и transient `TClient`. `AddHttpExtensions` удалён.
- `RequestService` разбит на partial-файлы по глаголам: `Get`, `Head`, `Options`, `Post`, `Put`,
  `Patch`, `Delete`, `Send`; флага `disposeRequest` нет — высокоуровневые методы диспозят свой
  request, escape hatches оставляют `HttpRequestMessage` вызывающему. `using var request = ...` стоит
  в публичном методе, рядом с объявлением запроса, а не в core; сырая отправка инлайнится
  (`_httpClient.SendAsync(..., HttpCompletionOption.ResponseHeadersRead, ct)`), без промежуточного
  `SendCoreAsync`; чтение — `ReadResponseAsync<T>`; `CreateRequest` создаёт запрос без тела,
  `CreateRequestWithBody<TBody>` сериализует тело, поэтому вложенного `CreateContent(body)` нет.
- `IRequestService` содержит 25 методов: добавлены `PostResponseAsync<TResponse>`,
  `PostResponseAsync<TResponse, TBody>`, `PutResponseAsync<TResponse, TBody>`,
  `PatchResponseAsync<TResponse, TBody>`, `DeleteResponseAsync<T>`.
- Тесты лежат в `tests/Mirai.Space.Extensions.Http.Tests/Tests/`; `InternalsVisibleTo` не используется:
  фабрики `RequestResponse`/`RequestException` живут в `Mirai.Space.Extensions.Http`, а Abstractions
  не ссылается на `HttpResponseMessage` (кроме escape-hatch `HttpRequestMessage` в `IRequestService`).

---

## 1. Цели и не-цели

### Цели

1. Заменить статический `RequestHelper` (статика + `ApiClientFactory.GetClient()` + статический
   `LogProvider`) на **DI-сервис**: `IRequestService`, который инжектится в доменные сервисы
   (`AboutRequestService`, `FilesRequestService`).
2. Одна абстракция сериализации — `IRequestSerializer`, две реализации: System.Text.Json и MessagePack,
   в **отдельных сборках**. Ядро про форматы не знает.
3. Честное владение ответом:
   - обычный запрос — тело прочитано, ответ задиспозен внутри, наружу уходят данные + снимок заголовков;
   - поток — наружу уходит владеющая обёртка (`ApiStreamResponse`), `Dispose` закрывает и поток, и ответ;
   - `IAsyncEnumerable<T>` — ответ живёт внутри итератора и диспозится при завершении, `break` и отмене.
4. Стриминг без промежуточного буфера: JSON-массив, NDJSON, SSE (`text/event-stream`), MessagePack
   (последовательность структур). Формат определяется по `Content-Type` ответа, а не по имени метода.
5. Полноценная кастомная обработка ошибок: `IApiErrorHandler` получает живой `HttpResponseMessage`
   и решает сам (это ровно то, чего не хватало в Refit).
6. Низкоуровневый выход: `SendAsync(HttpRequestMessage)` без утечки владения ответом; «сборка
   запроса руками» — через `ApiRequest`/`HttpRequestMessage`.
7. Всё остальное — стандартный `AddHttpClient`: именованные клиенты, `BaseAddress`, `Timeout`,
   `DelegatingHandler`, resilience-политики. Обёртка не подменяет `AddHttpClient`, а использует его.
8. Ноль рефлексии в горячем пути: поведение определяется типом возврата и перегрузкой метода,
   а не `MakeGenericMethod`.

### Не-цели

- Свой HTTP-стек, свой пул соединений, свой retry. Всё, что умеет `HttpClient`/`IHttpClientFactory`/
  `Microsoft.Extensions.Http.Resilience`, используется как есть.
- Refit как движок и интерфейсы-атрибуты (`[Get("/about")]`, `DispatchProxy`, source-gen прокси).
  Идеи Refit (тип возврата решает поведение, `IAsyncEnumerable` по Content-Type) взяты, реализация — нет.
- Генерация клиентов, codegen, AOT-прокси. Отдельная тема, если понадобится.
- Автоматический reconnect для SSE, `Last-EventId`, backoff. Это логика приложения; библиотека
  отдаёт `SseItem<T>`-метаданные, если они нужны.
- Политики resiliency внутри библиотеки: только примеры конфигурации, не дефолты.
- Логирование бизнес-событий внутри ядра. Логируют хендлеры через `ILogger<T>` из DI.
- Поддержка .NET Framework/старых TFM: целевой `net10.0` (репозиторий уже там).

---

## 2. Итоговые решения из переписки (что зафиксировано)

1. **`IRequestService` — конкретный набор методов**, без fluent-билдера и без `HttpClient` в
   публичных сигнатурах высокого уровня. Доменный сервис (`AboutRequestService`) форвардит в него
   строку URL и тело.
2. **`IRequestSerializer` — одна абстракция** (не `IHttpContentSerializer` + `IStreamingContentSerializer`):
   `Serialize<T>` → `ReadOnlyMemory<byte>`, `DeserializeAsync<T>` → `ValueTask<T?>`,
   `DeserializeAsyncEnumerable<T>` → `IAsyncEnumerable<T>` + `mediaType` для выбора формата.
3. **Сериализатор не выставляет `Content-Type`.** Это делает ядро (`RequestService`), беря
   `serializer.MediaType`. Причина: один сериализатор может отдавать разные media type позже
   (см. `application/x-msgpack` c LZ4), а `Content-Type` — часть HTTP-запроса, а не сериализации.
4. **`MediaType` остаётся** (в отличие от первого раунда): это единственное место, где ядро узнаёт,
   чем помечать тело. `Accept` по умолчанию **не** выставляется — это конфигурация именованного клиента.
5. **Никакого `object` в телах**: `PostAsync<TResponse, TBody>(url, TBody body)` — два generic-параметра
   выбраны явно вызывающим (`PostAsync<AuthModel, LoginModel>(...)`), ибо частичная подстановка типов
   в C# запрещена. Это осознанная цена за типизированное тело.
6. **`Task<T?>` везде, где есть тело** — никаких `null!`/`!` в публичном контракте.
7. **`ApiResponse` — класс, не интерфейс, и не `IDisposable`**: тело уже прочитано, наружу уходит
   снимок статуса и заголовков. `ApiResponse<T> : ApiResponse` добавляет `Content`.
8. **Поток — `ApiStreamResponse`** (`IDisposable` + `IAsyncDisposable`), а не голый `Stream`:
   прошлая ошибка `RequestHelper.Download`/`UploadGet` — никто не диспозил ответ.
9. **`IAsyncEnumerable<T>` сам владеет ответом** — `using var response` внутри итератора;
   отдельная обёртка не нужна. `break`/отмена диспозят корректно.
10. **`ApiException` не несёт тело ответа** (в финале чата `ResponseBody` убрали). Тело — забота
    конкретного `IApiErrorHandler`; JSON-проект даёт `ApiProblemException` с телом и problem details.
11. **Ошибки не «съедаются»**: не-2xx → `IApiErrorHandler.HandleAsync(response, ct)` → исключение;
    ответ диспозится ядром в `finally` до `throw`. `OperationCanceledException` и `HttpRequestException`
    не превращаются в `ApiException`.
12. **Формат стриминга — по `Content-Type`** (`application/json` → корневой массив,
    `application/x-ndjson`/`jsonl`/`x-jsonlines` → построчно, `text/event-stream` → SSE,
    `application/x-msgpack` → последовательность структур). Enum форматов в ядре **отвергнут**:
    JSON и MessagePack — разные сборки.
13. **SSE — `System.Net.ServerSentEvents` из .NET 10**, свой record не пишем (проверено: сборка
    `System.Net.ServerSentEvents.dll` лежит в `Microsoft.NETCore.App` 10.0.14 и в ref-pack).
14. **`ApiRequest` — фабрика `HttpRequestMessage`** вместо `new HttpRequestMessage(...)` в каждом
    методе; она же — точка сборки multipart и кастомных запросов.
15. **DI**: `AddRequestService<TClient>()` внутри вызывает настоящий `services.AddHttpClient(name)` и
    возвращает `IHttpClientBuilder`; сериализатор и обработчик ошибок регистрируются **keyed по имени
    клиента** (`AddKeyedSingleton<IRequestSerializer>(name, ...)`) с fallback на глобальную регистрацию.
16. **Именованные клиенты**: `BaseAddress`, `Timeout`, `AddHttpMessageHandler`, `AddStandardResilienceHandler`
    доступны как обычно. Для SSE-клиента — `Timeout.InfiniteTimeSpan` и отмена только через `CancellationToken`.
17. **`ConfigureAwait(false)` во всей библиотеке**; `[EnumeratorCancellation]` в реализациях итераторов.
18. **Отвергнуто явно** и почему:
    - `MediaType` на сериализаторе в первом раунде — отвергнуто, потом возвращено в виде `MediaType`
      как «чем помечать тело» (разные вещи: не `Accept`);
    - `HttpContent`/`MultipartFormDataContent` в высокоуровневых сигнатурах — утечка HTTP-типов в домен;
    - `Task<Stream>` (без владения) — утечка ответа;
    - `IApiResponse<T>` как интерфейс — незачем, реализация одна;
    - `StreamingContentFormat` в ядре — смешивает форматы разных пакетов;
    - `IOptionsMonitor<RequestServiceOptions>` с сервисами внутри — «опции = данные, сервисы = DI»;
    - `IRequestServiceFactory`/service locator — лишний слой, `ActivatorUtilities` в фабрике хватает;
    - Refit целиком — «много ненужного», нет низкого уровня, ломается на кастомных ошибках;
    - fluent (`api.Post(url).WithBody(x).ReadAsync<T>()`) — «не нужен такой Fluent стиль»;
    - `OwningStream : Stream` — наследование потока ради владения; вместо этого свойство `Stream Content`.

---

## 3. Границы проектов

```
Mirai.Space.Extensions.Http.Abstractions    ← контракты, ноль зависимостей
        ▲
        │
Mirai.Space.Extensions.Http                 ← движок + DI + auth-хендлеры
        ▲                    ▲
        │                    │
Mirai.Space.Extensions.Http.Json      Mirai.Space.Extensions.Http.MessagePack
```

Направление зависимостей: `Http → Abstractions`, `Json → Http`, `MessagePack → Http`,
**никогда наоборот**. Ядро не знает ни про `System.Text.Json`, ни про `MessagePack` (в его коде
нет ни одного `using` этих библиотек — это проверяемо на code review).

| Проект | Содержимое | PackageReference |
|---|---|---|
| `Mirai.Space.Extensions.Http.Abstractions` | `IRequestService`, `IRequestSerializer`, `IApiErrorHandler`, `IApiSession`, `IApiReauthenticator`, `ApiRequest`, `ApiResponse`/`ApiResponse<T>`, `ApiStreamResponse`, `ApiException` | нет (только BCL) |
| `Mirai.Space.Extensions.Http` | `RequestService`, `DefaultApiErrorHandler`, `ApiSession`, `ApiSessionHandler`, `ApiReauthenticationHandler`, `NoopApiReauthenticator`, DI-extensions | `Mirai.Space.Extensions.Http.Abstractions`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` |
| `Mirai.Space.Extensions.Http.Json` | `JsonRequestSerializer`, `ApiProblemDetails`, `ApiProblemException`, `ProblemDetailsApiErrorHandler`, `ConfigureJson`, `AddJsonSerializer` | `Mirai.Space.Extensions.Http` (STJ и `System.Net.Http.Json` — in-box в net10.0) |
| `Mirai.Space.Extensions.Http.MessagePack` | `MessagePackRequestSerializer`, `UseMessagePack` | `Mirai.Space.Extensions.Http`, `MessagePack` 3.1.12 |

Почему отдельный `Extensions.Http.Abstractions` (решение владельца, m00562):

- контракты не тянут `Microsoft.Extensions.Http` и DI: слой Application/Domain может ссылаться
  только на Abstractions и не знать про инфраструктуру;
- Json/MessagePack зависят от контрактов через ядро, а не наоборот; ядро — единственное место,
  где живут `RequestService` и DI;
- namespace у всех проектов остаётся единым — `Mirai.Space.Extensions.Http`, поэтому перенос
  файлов не меняет `using` у потребителей;
- преобразования — C# 14 extension-методы `response.ToRequestResponse()` / `response.ToRequestResponse(content)` /
  `response.ToRequestException(request)` в `RequestResponseExtensions` и `RequestExceptionExtensions`, живут в `Mirai.Space.Extensions.Http`;
  `RequestResponse` имеет публичный конструктор от готовых словарей заголовков, Abstractions не знает
  `HttpResponseMessage`; `InternalsVisibleTo` не нужен.

### Что делать с существующими проектами

- `Mirai.Space.Extensions.Http.Abstractions` — **создан** (m00562): контракты перенесены из
  `Extensions.Http/Abstractions/`, у проекта нет ни одной `PackageReference`.
- `Mirai.Space.Extensions.Http.Json` / `.MessagePack` — уже есть, `Class1.cs` удалён, есть
  `ProjectReference` на `Mirai.Space.Extensions.Http` (контракты приходят транзитивно).
- `Mirai.Space.Request` — **прикладной проект**: здесь будут жить конечные сервисы (например,
  `AboutRequestService`), которые принимают `IRequestService` и описывают конкретные endpoint'ы.
  Инфраструктура (`RequestService`, сериализаторы, DI) остаётся в `Extensions.Http*`; проект
  не удаляется.
- `Mirai.Space.slnx` — **устаревший**: ссылается на `Mirai.Space.Android`, `Mirai.Space.iOS`,
  `Mirai.Space/Mirai.Space.csproj`, которых в репозитории нет. Собирать и обновлять нужно `Mirai.Space.sln`.
  Предложение: удалить `.slnx` либо перегенерировать (решение за владельцем).
- `Directory.Build.props` в репозитории **отсутствует** (как и `global.json`, `.editorconfig`):
  `TargetFramework`/`ImplicitUsings`/`Nullable` дублируются в каждом `.csproj`. Предложение —
  создать общий `Directory.Build.props` (см. §10, шаг 0).

---

## 4. Публичный API

### 4.1 `Mirai.Space.Extensions.Http` — сервис

```csharp
namespace Mirai.Space.Extensions.Http;

/// <summary>Низкоуровневый HTTP-сервис поверх именованного HttpClient.</summary>
/// <remarks>
/// Все методы: <list type="bullet">
/// <item>выставляют Content-Type из <see cref="IRequestSerializer.MediaType"/> для тел с полезной нагрузкой;</item>
/// <item>читают ответ с <see cref="HttpCompletionOption.ResponseHeadersRead"/>;</item>
/// <item>любой не-2xx отдают в <see cref="IApiErrorHandler"/> и бросают полученное исключение;</item>
/// <item>диспозят <see cref="HttpResponseMessage"/> сами, кроме методов, возвращающих
/// <see cref="ApiStreamResponse"/> (там владение переходит вызывающему).</item>
/// </list>
/// </remarks>
public interface IRequestService
{
    // ---------- GET ----------
    /// <summary>GET + десериализация тела. Пустое тело → <c>null</c> (для ссылочных типов).</summary>
    Task<T?> GetAsync<T>(string url, CancellationToken ct = default);

    /// <summary>GET + тело + снимок статуса/заголовков.</summary>
    Task<ApiResponse<T>> GetResponseAsync<T>(string url, CancellationToken ct = default);

    /// <summary>GET, тело не читается: поток отдаётся как есть. Dispose закрывает и поток, и ответ.</summary>
    Task<ApiStreamResponse> DownloadAsync(string url, CancellationToken ct = default);

    /// <summary>GET + потоковая десериализация по Content-Type (JSON-массив / NDJSON / SSE / MessagePack).</summary>
    IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url, CancellationToken ct = default);

    // ---------- HEAD / OPTIONS ----------
    /// <summary>HEAD: только статус и заголовки, тело никогда не читается.</summary>
    Task<ApiResponse> HeadAsync(string url, CancellationToken ct = default);

    /// <summary>OPTIONS: только статус и заголовки.</summary>
    Task<ApiResponse> OptionsAsync(string url, CancellationToken ct = default);

    // ---------- POST ----------
    Task<T?> PostAsync<T>(string url, CancellationToken ct = default);
    Task<TResponse?> PostAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);
    Task PostAsync(string url, CancellationToken ct = default);
    Task PostAsync<TBody>(string url, TBody body, CancellationToken ct = default);
    Task<ApiResponse<TResponse>> PostResponseAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);
    Task<ApiStreamResponse> PostToStreamAsync(string url, CancellationToken ct = default);
    Task<ApiStreamResponse> PostToStreamAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    // ---------- PUT ----------
    Task<TResponse?> PutAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);
    Task PutAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    // ---------- PATCH ----------
    Task<TResponse?> PatchAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);
    Task PatchAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    // ---------- DELETE ----------
    /// <summary>DELETE без тела запроса (RFC 9110: тело у DELETE не определено).</summary>
    Task DeleteAsync(string url, CancellationToken ct = default);

    /// <summary>DELETE, читающий тело ответа (200 + тело / 202 / 204 → <c>null</c>).</summary>
    Task<T?> DeleteAsync<T>(string url, CancellationToken ct = default);

    // ---------- escape hatch: запрос собран вызывающим ----------
    /// <summary>Полный контроль над запросом. Ответ не читается, заголовки снимаются, ответ диспозится ядром.</summary>
    Task<ApiResponse> SendAsync(HttpRequestMessage request, CancellationToken ct = default);

    /// <summary>То же, но тело читается сериализатором клиента.</summary>
    Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default);

    /// <summary>То же, но тело отдаётся потоком (владение — у вызывающего).</summary>
    Task<ApiStreamResponse> SendForStreamAsync(HttpRequestMessage request, CancellationToken ct = default);

    /// <summary>То же, но тело отдаётся потоковым перечислением по Content-Type.</summary>
    IAsyncEnumerable<T> SendAsyncEnumerable<T>(HttpRequestMessage request, CancellationToken ct = default);
}
```

Замечания к контракту (это же — текст XML-doc):

- **`[EnumeratorCancellation]`** ставится в реализациях итераторов, не в интерфейсе: только
  компиляторно-сгенерированный итератор умеет смёржить токен из `WithCancellation` с параметром.
- **`Task<T?>` для `T`-структур** — это не `Nullable<T>`: 204 No Content даст `default(T)`.
- **Пустое тело** (`204`, `205`, `Content-Length: 0`) в `GetAsync<T>`/`PostAsync<T>` даёт `null`
  (ссылочный `T`) или `default(T)`; `DownloadAsync` даёт `Stream.Null` (кейс `UploadGet`, где
  раньше возвращался `null`).
- **`SendAsync`** не бросает на не-2xx мимо обработчика: обработчик вызывается всегда.
- **`ApiStreamResponse.Content`** владеет соединением; после `Dispose` читать его нельзя.
- **Отмена**: `ct` пробрасывается во все слои; `OperationCanceledException` не заворачивается.

### 4.2 `Mirai.Space.Extensions.Http` — запрос, ответ, ошибки, сериализация

```csharp
namespace Mirai.Space.Extensions.Http;

/// <summary>Фабрика HttpRequestMessage: одно место, где собираются запросы библиотеки.</summary>
public static class ApiRequest
{
    public static HttpRequestMessage Get(string url);
    public static HttpRequestMessage Head(string url);
    public static HttpRequestMessage Options(string url);
    public static HttpRequestMessage Delete(string url);
    public static HttpRequestMessage Post(string url, HttpContent? content = null);
    public static HttpRequestMessage Put(string url, HttpContent? content = null);
    public static HttpRequestMessage Patch(string url, HttpContent? content = null);
}

/// <summary>Снимок ответа без тела: статус + заголовки. Не IDisposable — ответ уже задиспозен.</summary>
public class ApiResponse
{
    public HttpStatusCode StatusCode { get; }

    /// <summary>Заголовки ответа. Ключи — case-insensitive. Значения — копии, живут дольше ответа.</summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; }

    /// <summary>Заголовки тела (Content-Type, Content-Length, Content-Disposition…).</summary>
    public IReadOnlyDictionary<string, string[]> ContentHeaders { get; }
}

/// <summary>Снимок ответа + десериализованное тело.</summary>
public sealed class ApiResponse<T> : ApiResponse
{
    /// <summary>Тело или default, если тела не было.</summary>
    public T? Content { get; }
}

/// <summary>Ответ, тело которого отдаётся потоком. Владеет HttpResponseMessage.</summary>
public sealed class ApiStreamResponse : ApiResponse, IDisposable, IAsyncDisposable
{
    /// <summary>Поток тела. Dispose обёртки закрывает поток и ответ; повторный Dispose — no-op.</summary>
    public Stream Content { get; }

    public void Dispose();
    public ValueTask DisposeAsync();
}

/// <summary>Сериализация тела запроса/ответа. Реализации: JsonRequestSerializer, MessagePackRequestSerializer.</summary>
public interface IRequestSerializer
{
    /// <summary>Media type, которым ядро помечает тело запроса (Content-Type) и по которому выбирает реализацию.</summary>
    string MediaType { get; }

    /// <summary>Сериализация в буфер. Синхронно: это CPU, сеть пишет HttpClient.</summary>
    ReadOnlyMemory<byte> Serialize<T>(T value);

    /// <summary>Десериализация одного значения из потока ответа.</summary>
    ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken ct = default);

    /// <summary>
    /// Потоковая десериализация. <paramref name="mediaType"/> — Content-Type ответа (может быть null);
    /// реализация сама решает, что это: массив, NDJSON, SSE или последовательность структур.
    /// Возвращаемый поток не содержит null-элементов.
    /// </summary>
    IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(Stream stream, string? mediaType, CancellationToken ct = default);
}

/// <summary>Преобразование не-2xx ответа в исключение. Вызывается до чтения тела.</summary>
public interface IApiErrorHandler
{
    /// <summary>
    /// Читает ответ как считает нужным (тело, заголовки, Retry-After) и возвращает исключение,
    /// которое ядро бросит после того, как задиспозит ответ. Бросать внутри реализации нельзя —
    /// вернуть исключение. Возврат <c>null</c> приведёт к NullReferenceException в ядре (контракт
    /// non-nullable).
    /// </summary>
    ValueTask<Exception> HandleAsync(HttpResponseMessage response, CancellationToken ct = default);
}

/// <summary>Дефолт: статус + метод + URI, без чтения тела. Ничего не логирует.</summary>
public sealed class DefaultApiErrorHandler : IApiErrorHandler
{
    public ValueTask<Exception> HandleAsync(HttpResponseMessage response, CancellationToken ct = default);
}

/// <summary>HTTP-ошибка (не-2xx). Тела не несёт: за тело отвечает конкретный обработчик.</summary>
public class ApiException : Exception
{
    public ApiException(string message, HttpStatusCode statusCode, HttpMethod? method, Uri? requestUri, Exception? innerException = null);

    public HttpStatusCode StatusCode { get; }
    public HttpMethod? Method { get; }
    public Uri? RequestUri { get; }
}
```

### 4.3 `Mirai.Space.Extensions.Http` — сессия, авторизация, переавторизация

```csharp
namespace Mirai.Space.Extensions.Http;

/// <summary>Текущая авторизация. Singleton, потокобезопасная реализация.</summary>
public interface IApiSession
{
    AuthenticationHeaderValue? Authorization { get; }
    void Set(AuthenticationHeaderValue authorization);
    void Clear();

    /// <summary>Смена сессии (логин/логаут/обновление токена) — для UI и SignalR-переподключения.</summary>
    event EventHandler? Changed;
}

public sealed class ApiSession : IApiSession;

/// <summary>Ставит Authorization из IApiSession перед отправкой. Наследуемый — чтобы снимать заголовки ответа.</summary>
public class ApiSessionHandler : DelegatingHandler
{
    public ApiSessionHandler(IApiSession session);

    /// <summary>Хук после ответа: сервер обновил токен в заголовках — наследник обновляет сессию.</summary>
    protected virtual ValueTask OnResponseAsync(HttpResponseMessage response, CancellationToken ct) => default;
}

/// <summary>Реактивная переавторизация: показать окно логина, обновить IApiSession.</summary>
public interface IApiReauthenticator
{
    /// <summary>true — сессия обновлена и запрос можно повторить один раз.</summary>
    Task<bool> TryReauthenticateAsync(CancellationToken ct = default);
}

/// <summary>401 → TryReauthenticateAsync → один повтор (если тело запроса воспроизводимо).</summary>
public sealed class ApiReauthenticationHandler : DelegatingHandler
{
    public ApiReauthenticationHandler(IApiReauthenticator reauthenticator);
}
```

### 4.4 `Mirai.Space.Extensions.Http.Json`

```csharp
namespace Mirai.Space.Extensions.Http.Json;

/// <summary>System.Text.Json. MediaType = "application/json" (без charset — его ставит сервер).</summary>
public sealed class JsonRequestSerializer : IRequestSerializer
{
    /// <summary>Опции копируются (snapshot), исходный объект не мутируется и не замораживается.</summary>
    public JsonRequestSerializer(JsonSerializerOptions options);

    /// <summary>Source-gen режим: TypeInfoResolver = context (reflection-resolver не подмешивается).</summary>
    public JsonRequestSerializer(JsonSerializerContext context);

    /// <summary>Дефолт: JsonSerializerDefaults.Web.</summary>
    public JsonRequestSerializer();
}

/// <summary>Problem details (RFC 9457) как тело ошибки.</summary>
public sealed record ApiProblemDetails(string? Type, string? Title, int? Status, string? Detail, string? Instance);

/// <summary>ApiException + тело + разобранный problem details.</summary>
public sealed class ApiProblemException : ApiException
{
    public string? ResponseBody { get; }
    public ApiProblemDetails? Problem { get; }
}

/// <summary>Читает тело ошибки, пытается разобрать problem details, иначе отдаёт сырой текст.</summary>
public sealed class ProblemDetailsApiErrorHandler : IApiErrorHandler
{
    public ProblemDetailsApiErrorHandler(JsonSerializerOptions? options = null, int maxBodyLength = 8192);
}
```

### 4.5 `Mirai.Space.Extensions.Http.MessagePack`

```csharp
namespace Mirai.Space.Extensions.Http.MessagePack;

/// <summary>MessagePack. MediaType = "application/x-msgpack".</summary>
public sealed class MessagePackRequestSerializer : IRequestSerializer
{
    public MessagePackRequestSerializer(MessagePackSerializerOptions options);

    /// <summary>Дефолт: MessagePackSerializerOptions.Standard.</summary>
    public MessagePackRequestSerializer();
}
```

### 4.6 DI-extensions

```csharp
namespace Microsoft.Extensions.DependencyInjection;   // намеренно: рядом с AddHttpClient

public static class RequestServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует именованный HttpClient (имя = typeof(TClient).FullName) и transient TClient,
    /// в конструктор которого подставляется IRequestService поверх этого клиента.
    /// Возвращает IHttpClientBuilder: ConfigureHttpClient / AddHttpMessageHandler / resilience — как обычно.
    /// </summary>
    /// <remarks>
    /// TClient создаётся через ActivatorUtilities, поэтому его конструктор может принимать
    /// IRequestService, HttpClient и любые другие DI-сервисы (ILogger&lt;TClient&gt;, IApiSession…).
    /// </remarks>
    public static IHttpClientBuilder AddRequestService<TClient>(this IServiceCollection services)
        where TClient : class;

    /// <summary>Глобальный обработчик ошибок (используется, если у клиента нет keyed-переопределения).</summary>
    public static IServiceCollection AddApiErrorHandler<THandler>(this IServiceCollection services)
        where THandler : class, IApiErrorHandler;
}

public static class RequestServiceBuilderExtensions
{
    /// <summary>Переопределить сериализатор для конкретного клиента (экземпляр из DI, singleton).</summary>
    public static IHttpClientBuilder UseSerializer<TSerializer>(this IHttpClientBuilder builder)
        where TSerializer : class, IRequestSerializer;

    /// <summary>Переопределить обработчик ошибок для конкретного клиента.</summary>
    public static IHttpClientBuilder UseErrorHandler<THandler>(this IHttpClientBuilder builder)
        where THandler : class, IApiErrorHandler;
}

// .Http.Json
public static class JsonHttpClientBuilderExtensions
{
    /// <summary>Сериализатор для этого клиента. Опции создаются и замораживаются здесь же.</summary>
    public static IHttpClientBuilder ConfigureJson(this IHttpClientBuilder builder, Action<JsonSerializerOptions>? configure = null);
    public static IHttpClientBuilder ConfigureJson(this IHttpClientBuilder builder, JsonSerializerOptions options);
    public static IHttpClientBuilder ConfigureJson(this IHttpClientBuilder builder, JsonSerializerContext context);
}

public static class JsonServiceCollectionExtensions
{
    /// <summary>Глобальный сериализатор по умолчанию (fallback для клиентов без ConfigureJson).</summary>
    public static IServiceCollection AddJsonSerializer(this IServiceCollection services, Action<JsonSerializerOptions>? configure = null);
    public static IServiceCollection AddJsonSerializer(this IServiceCollection services, JsonSerializerOptions options);
    public static IServiceCollection AddJsonSerializer(this IServiceCollection services, JsonSerializerContext context);

    /// <summary>Глобальный ProblemDetailsApiErrorHandler.</summary>
    public static IServiceCollection AddProblemDetailsErrorHandler(this IServiceCollection services);
}

// .Http.MessagePack
public static class MessagePackHttpClientBuilderExtensions
{
    /// <summary>Standard options.</summary>
    public static IHttpClientBuilder UseMessagePack(this IHttpClientBuilder builder);

    /// <summary>Опции MessagePack иммутабельны — принимаем готовый объект.</summary>
    public static IHttpClientBuilder UseMessagePack(this IHttpClientBuilder builder, MessagePackSerializerOptions options);

    /// <summary>
    /// Иммутабельные опции конфигурируются трансформером: configure(Standard) → With*(...).
    /// Ровно как в первом раунде обсуждения.
    /// </summary>
    public static IHttpClientBuilder UseMessagePack(this IHttpClientBuilder builder, Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure);
}
```

### 4.7 Как это выглядит в приложении

```csharp
// Application/Presentation: доменный сервис ничего не знает про HTTP
public sealed class AboutRequestService(IRequestService requests)
{
    public Task<AboutModel?> GetAsync(CancellationToken ct = default)
        => requests.GetAsync<AboutModel>(Urls.AboutGet, ct);

    public Task PostUpdateAsync(AboutModel model, CancellationToken ct = default)
        => requests.PostAsync(Urls.AboutUpdate, model, ct);
}

// Composition root
services.AddSingleton<IApiSession, ApiSession>();
services.AddTransient<ApiSessionHandler>();
services.AddTransient<ApiReauthenticationHandler>();
services.AddTransient<IApiReauthenticator, AvaloniaReauthenticator>();   // app-логика
services.AddApiErrorHandler<SputnikApiErrorHandler>();                   // бывший ThrowException

services.AddRequestService<AboutRequestService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBase))
    .ConfigureJson(o => o.AddJsonContexts())                 // source-gen контексты
    .AddHttpMessageHandler<ApiReauthenticationHandler>()   // внешний: 401 → reauth → retry
    .AddHttpMessageHandler<ApiSessionHandler>();           // внутренний: свежий Authorization

services.AddRequestService<FilesRequestService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBase))
    .UseMessagePack(MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray))
    .AddHttpMessageHandler<ApiSessionHandler>();

// SSE: отдельный клиент без таймаута, отмена — только через ct
services.AddRequestService<NotificationsStreamService>()
    .ConfigureHttpClient(c => { c.BaseAddress = new Uri(apiBase); c.Timeout = Timeout.InfiniteTimeSpan; })
    .ConfigureJson(_ => { });

// Использование
var about = await aboutRequests.GetAsync(ct);                          // AboutModel?

await foreach (var item in requests.GetAsyncEnumerable<ItemModel>(Urls.Items, ct))
    Items.Add(item);                                                   // тело цикла — на UI-потоке

var auth = await requests.PostResponseAsync<AuthModel, LoginModel>(Urls.Login, model, ct);
var token = auth.Headers["X-Auth-Token"][0];                           // снимок живёт после dispose

await using var file = await requests.DownloadAsync($"{Urls.Files}/{id}", ct);
await file.Content.CopyToAsync(target, ct);                            // Dispose закроет ответ

// multipart + низкий уровень (escape hatch)
using var upload = ApiRequest.Post(Urls.Files, new MultipartFormDataContent
{
    { new StreamContent(stream), "file", fileName },
    { new StringContent(folderId.ToString()), "folderId" },
});
var created = await requests.SendAsync<FileModel>(upload, ct);
```

---

## 5. Владение ответом и потоки данных

| Сценарий | Кто владеет `HttpResponseMessage` | Когда освобождается | Что видит вызывающий |
|---|---|---|---|
| `GetAsync<T>`, `PostAsync<T>`, … | ядро | сразу после десериализации (`using`) | `T?` |
| `GetResponseAsync<T>`, `PostResponseAsync<T,TBody>` | ядро | сразу после чтения тела | `ApiResponse<T>` (статус + снимок заголовков + тело) |
| `HeadAsync`, `OptionsAsync`, `SendAsync` | ядро | сразу после снятия заголовков | `ApiResponse` |
| `DownloadAsync`, `PostToStreamAsync`, `SendForStreamAsync` | **вызывающий** | `Dispose`/`await using` обёртки | `ApiStreamResponse.Content` |
| `GetAsyncEnumerable<T>`, `SendAsyncEnumerable<T>` | итератор | конец перечисления, `break`, отмена, исключение | элементы `T` |
| не-2xx (любой метод) | ядро | `finally` до `throw` | `ApiException` (или наследник) |

Снимок заголовков делается **до** диспоза ответа, поэтому `ApiResponse` переживает `using`
и может храниться в поле.

---

## 6. DI, времена жизни, именованные клиенты

### 6.1 Времена жизни

| Сервис | Время жизни | Почему |
|---|---|---|
| `IHttpClientFactory` / именованный `HttpClient` | синглтон / на фабрике | стандарт: ротация хендлеров, DNS |
| `TClient` (доменный сервис) | **transient** | иначе «поймает» один `HttpClient` навсегда; дешёвый объект |
| `RequestService` | **transient** | держит `HttpClient`; создаётся в фабрике `TClient` |
| `IRequestSerializer` | **singleton** (keyed по имени клиента) | иммутабельный (опции заморожены/скопированы при регистрации); дешёвая регистрация |
| `IApiErrorHandler` | **singleton** (keyed или глобальный) | должен быть stateless; если нужны scoped-зависимости — per-client override через `UseErrorHandler<T>` и **не** singleton |
| `IApiSession` | **singleton** | одно состояние авторизации на процесс |
| `DelegatingHandler` | transient (регистрирует `AddHttpMessageHandler`) | требование `IHttpClientFactory` |
| `IApiReauthenticator` | зависит от app (UI-поток → transient/scoped) | показывает окно |

### 6.2 Как устроена регистрация

```csharp
public static IHttpClientBuilder AddRequestService<TClient>(this IServiceCollection services)
    where TClient : class
{
    var name = typeof(TClient).FullName!;
    var builder = services.AddHttpClient(name);                 // ← настоящий AddHttpClient

    services.TryAddSingleton<IApiErrorHandler, DefaultApiErrorHandler>();

    services.TryAddTransient<TClient>(sp =>
    {
        var http       = sp.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        var serializer = sp.GetKeyedService<IRequestSerializer>(name) ?? sp.GetRequiredService<IRequestSerializer>();
        var errors     = sp.GetKeyedService<IApiErrorHandler>(name) ?? sp.GetRequiredService<IApiErrorHandler>();

        var requests = new RequestService(http, serializer, errors);
        return ActivatorUtilities.CreateInstance<TClient>(sp, requests);
    });

    return builder;                                             // ConfigureHttpClient / handlers / resilience
}
```

Проверено компиляцией на `Microsoft.Extensions.Http` 11.0.0 / DI 11.0.0: `builder.Name`,
`builder.Services`, `GetKeyedService<T>(name)`, `AddKeyedSingleton<T>(name, instance)`,
`ActivatorUtilities.CreateInstance<TClient>(sp, requests)` с конструктором `(IRequestService)` — всё
собирается.

Почему keyed, а не `IOptions`/фабрика:

- сериализатор — это **сервис с поведением**, а не данные; «опции = данные, сервисы = DI» (правило из переписки);
- keyed-регистрация даёт ровно то, что нужно: **разные сериализаторы для разных клиентов**, глобальный —
  fallback;
- никакого `IServiceProvider`-локатора внутри `RequestService`: зависимости приходят в конструктор.

### 6.3 Сессия и переавторизация

- `ApiSessionHandler` (внутренний) ставит `Authorization` из `IApiSession` перед каждым запросом.
  Наследник переопределяет `OnResponseAsync` и снимает обновлённый токен из заголовков ответа —
  это кейс «логин посреди работы, сервер вернул новый токен».
- `ApiReauthenticationHandler` (внешний) на `401` вызывает `IApiReauthenticator.TryReauthenticateAsync`:
  тот на UI-потоке показывает окно, логинит, кладёт креды в `IApiSession`; запрос повторяется
  **один раз** через `base.SendAsync(clone)` — `ApiSessionHandler` подставит уже свежий заголовок.
- Повтор возможен только для **воспроизводимого тела** (весь высокоуровневый API отправляет
  `ReadOnlyMemoryContent` — оно воспроизводимо; `MultipartFormDataContent` со `StreamContent` — нет,
  такие запросы не повторяются, в лог уходит предупреждение).
- Порядок добавления: **первым добавляется внешний** хендлер (`AddHttpMessageHandler` вставляет
  в начало цепочки), т.е. `AddHttpMessageHandler<ApiReauthenticationHandler>()` затем
  `AddHttpMessageHandler<ApiSessionHandler>()` — как в примере §4.7.
- Логин на старте — это `PostResponseAsync<AuthModel, LoginModel>` + `session.Set(...)`; окно логина
  не знает про HTTP.

### 6.4 Resilience

`AddStandardResilienceHandler()` доступен, потому что возвращается `IHttpClientBuilder`
(`Microsoft.Extensions.Http.Resilience` 11.0.0 уже в `Directory.Packages.props`). Правила:

- для обычных клиентов — стандартный handler, но `HttpRetryStrategyOptions` с `ShouldHandle`,
  исключающим неидемпотентные POST;
- для стриминговых/SSE-клиентов retry **после начала чтения невозможен** — либо не подключать
  handler, либо ограничить попытки `AttemptTimeout`/`TotalRequestTimeout` и допустимым
  `ShouldHandle` (только `HttpRequestException` до получения заголовков);
- `HttpClient.Timeout` для SSE — `Timeout.InfiniteTimeSpan`, отмена — `CancellationToken`.

---

## 7. Модель ошибок и отмена

1. **Не-2xx** (включая 3xx, если редирект не отработал) → `IApiErrorHandler.HandleAsync(response, ct)`
   → ядро диспозит ответ в `finally` → бросает возвращённое исключение.
   Обработчик внутри **не бросает** — возвращает `Exception` (иначе `throw` в `try` с `finally`
   ломает стек-трейс и порядок диспоза).
2. `DefaultApiErrorHandler` **не читает тело** (расхождение с последней версией в чате, где
   `ReadAsStringAsync` читался и выбрасывался: это лишний I/O на каждый 4xx/5xx). Тело читает
   только тот обработчик, которому оно нужно.
3. **JSON-проект** даёт `ProblemDetailsApiErrorHandler` → `ApiProblemException` с `ResponseBody`
   и `ApiProblemDetails`. Ограничение размера тела (`maxBodyLength`) — чтобы не съесть память на
   гигантской HTML-ошибке.
4. **Приложение** переопределяет обработчик (`SputnikApiErrorHandler`) и сохраняет прежние
   сообщения: 404 — «Запрашиваемый адрес не найден. ({uri})», 401 — «Неверный логин или пароль.»,
   403 — «Доступ запрещен», `type == "CustomException"` → `ClientException(title)`. Логирование —
   через `ILogger<T>` из DI вместо статического `LogProvider`.
5. **Отмена**: `OperationCanceledException` (в т.ч. `TaskCanceledException`) не перехватывается и
   не превращается в `ApiException`; `HttpClient.Timeout` в .NET даёт `TaskCanceledException`
   с `InnerException is TimeoutException` — документируем, обёртку не вводим (вызывающий и так
   различает по `ct.IsCancellationRequested`).
6. `HttpRequestException` (DNS, TLS, обрыв) проходит наверх как есть; `ApiException` — только
   про HTTP-статус. Это позволяет отличать «сеть» от «сервер сказал 500».
7. Тело ошибки после диспоза недоступно — поэтому обработчик обязан прочитать его **до** возврата,
   а не отдавать исключение с ленивой ссылкой на `HttpResponseMessage`. Контракт описан в XML-doc.

---

## 8. Стриминг, SSE, multipart, MessagePack

### 8.1 `IAsyncEnumerable<T>` (JSON-массив, NDJSON, SSE, MessagePack)

```csharp
public async IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    using var request  = ApiRequest.Get(url);
    using var response = await SendAsync(request, ct).ConfigureAwait(false);
    var stream    = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    var mediaType = response.Content.Headers.ContentType?.MediaType;

    await foreach (var item in _serializer
        .DeserializeAsyncEnumerable<T>(stream, mediaType, ct)
        .ConfigureAwait(false))
    {
        yield return item;
    }
}
```

- Ответ живёт внутри итератора: `break`, исключение, отмена → `Dispose` через `using`.
- `[EnumeratorCancellation]` смёржит токен из `await foreach (... .WithCancellation(ct))`.
- Никакого `.ToList()` внутри; буфера нет (кроме случая MessagePack с одним top-level массивом — §8.4).

### 8.2 SSE

`SseParser<T>` из `System.Net.ServerSentEvents` (in-box). Парсер получает **`ReadOnlySpan<byte>`**
payload, а не строку (проверено по ref-assembly: `SseItemParser<out T>(string eventType, ReadOnlySpan<byte> data)`).
Внутри `JsonRequestSerializer.DeserializeAsyncEnumerable` для `text/event-stream`:

```csharp
if (mediaType is "text/event-stream")
{
    var parser = SseParser.Create<T>(stream, (eventType, data) =>
        JsonSerializer.Deserialize<T>(data, _options)
        ?? throw new JsonException($"SSE event '{eventType}' has a payload that is not a {typeof(T)}."));

    await foreach (var item in parser.EnumerateAsync(ct).ConfigureAwait(false))
        yield return item.Data;

    yield break;
}
```

Решения:

- **возвращаем `IAsyncEnumerable<T>` (не `SseItem<T>`)**: метаданные (`EventId`, `EventType`,
  `ReconnectionInterval`) нужны редко, а `SseParser.LastEventId` доступен, если понадобится
  reconnect. При необходимости позже добавляется `GetSseItemsAsync<T>` — без ломающих изменений;
- **null-payload → исключение**, а не `!`: событие без валидного JSON — нарушение протокола,
  молча глотать его хуже. См. §11, вопрос Q1;
- **никакого авто-reconnect** внутри библиотеки (как и в Refit): это политика приложения.

### 8.3 Поток ответа

`DownloadAsync` использует `HttpCompletionOption.ResponseHeadersRead` и отдаёт
`ApiStreamResponse`, который владеет `HttpResponseMessage`. Пустое тело → `Stream.Null`
(обёртка всё равно диспозится). `ApiStreamResponse` реализует и `IDisposable`, и `IAsyncDisposable`
(второй — для `await using` без блокировки на сетевом потоке).

### 8.4 MessagePack

- `MediaType = "application/x-msgpack"`.
- Одиночное значение: `MessagePackSerializer.DeserializeAsync<T>(stream, options, ct)` — читает
  один top-level объект (внутри читает поток в память, для больших файлов не годится — для этого поток).
- Стриминг: `MessagePackStreamReader` + `ReadAsync(ct)` → `ReadOnlySequence<byte>` на структуру,
  `MessagePackSerializer.Deserialize<T>(seq, options, ct)`. Проверено компиляцией на MessagePack 3.1.12.
- **Ограничение протокола**: один top-level MessagePack-массив стримить нельзя — `ReadAsync`
  вернёт его целиком как одно сообщение, и `Deserialize<T>` прочитает его как один объект `T`.
  Отличить «массив объектов» от array-encoded объекта (`[MessagePackObject]` по умолчанию
  сериализуется в массив) по первому байту нельзя, поэтому `NotSupportedException` не бросаем:
  сервер шлёт последовательность структур (рекомендуется), а для массива ошибка десериализации
  штатная — буферизуйте через `DownloadAsync`.
- **Source-gen резолверов в MessagePack 3.1.12 нет.** Атрибут `[GeneratedMessagePackResolver]`
  присутствует в `MessagePack.dll`, но генератор в пакет **не входит** (в пакете только `lib/*`,
  без `analyzers/`), а отдельный пакет `MessagePack.SourceGenerator` существует лишь в версиях
  `2.6.x-alpha` и к v3 не относится. Вывод: пишем `MessagePackSerializerOptions.Standard.WithResolver(...)`
  с runtime-резолверами; AOT-обещаний по MessagePack не даём. Это надо зафиксировать в README проекта.

### 8.5 Multipart

Отдельного типа `ApiMultipart*` не вводим (в переписке от него отказались). Путь:

```csharp
using var form = new MultipartFormDataContent();
form.Add(new StreamContent(stream), "file", fileName);   // name / fileName / content-type
form.Add(new StringContent(folderId.ToString()), "folderId");

using var request = ApiRequest.Post(Urls.FilesUpload, form);
var uploaded = await requests.SendAsync<FileModel>(request, ct);   // SendAsync<T> читает тело сериализатором
```

Плюсы: ноль новых типов, полный доступ к `MultipartFormDataContent`, `Content-Type`/boundary
выставляет сам `HttpContent`. Минус: в этом одном сценарии `System.Net.Http` виден в коде приложения
— это осознанный escape hatch, а не основной путь.

`ApiRequest.Post/Put/Patch(url, HttpContent?)` — единственное место публичного API, где фигурирует
`HttpContent`, и только для «запрос собран вызывающим».

---

## 9. Тест-план

Всё — xUnit v3 (`xunit.v3` 4.0.3, MTP v2). На .NET 10 SDK `dotnet test` работает с MTP нативно,
но требует `global.json`:

```json
{
  "sdk": { "version": "10.0.403", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

Тестовый проект — **exe** (`<OutputType>Exe</OutputType>`), без `Microsoft.NET.Test.Sdk` и
`xunit.runner.visualstudio` (в v3 MTP-раннер входит в пакет; `Microsoft.NET.Test.Sdk` даже мешает).
Проверено по nuspec `xunit.v3/4.0.3` (зависимость `xunit.v3.mtp-v2`) и по документации .NET 10 SDK.

### 9.1 `Mirai.Space.Extensions.Http.Tests` (unit)

Инфраструктура: `StubHttpMessageHandler : HttpMessageHandler` — записывает `HttpRequestMessage`,
отдаёт заранее заданный `HttpResponseMessage`/`Func<HttpRequestMessage, HttpResponseMessage>`;
`TestRequestService` — сборка `RequestService` поверх `new HttpClient(stub)`, без DI.

| # | Что проверяем |
|---|---|
| 1 | `GetAsync<T>`: метод GET, URL, `Accept` не выставляется, тело десериализуется |
| 2 | `PostAsync<T,TBody>`: `Content-Type == serializer.MediaType`, тело = байты сериализатора |
| 3 | 204/пустое тело → `null` (класс) / `default` (структура), без исключения |
| 4 | Не-2xx: обработчик вызван ровно один раз, получил живой ответ, ответ диспозен до `throw` |
| 5 | `ApiResponse.Headers`: case-insensitive, значения — копии; после `Dispose` ответа снимок читается |
| 6 | `HeadAsync`/`OptionsAsync`: тело не читается (`Content` не трогали — проверка через stub) |
| 7 | `DownloadAsync`: `ResponseHeadersRead`, `Dispose` обёртки диспозит поток и ответ; повторный `Dispose` — no-op |
| 8 | `GetAsyncEnumerable<T>`: ответ диспозится при `break`, при исключении в теле цикла и при отмене |
| 9 | `ct` доходит до `HttpMessageHandler.SendAsync` (отмена до и во время чтения) |
| 10 | `ApiRequest.*` выставляют метод/URL; `Post(url, null)` не создаёт content |
| 11 | `DefaultApiErrorHandler`: не читает тело, статус/метод/URI попадают в `ApiException` |
| 12 | `ApiSessionHandler`: `Authorization` подставляется/не подставляется при пустой сессии; `OnResponseAsync` вызывается |
| 13 | `ApiReauthenticationHandler`: 401 → reauth(true) → ровно один повтор с новым заголовком; reauth(false) → исключение; 401 на повторном ответе не зацикливается |
| 14 | `ApiReauthenticationHandler`: тело-`StreamContent` не повторяется (один запрос), логируется предупреждение |
| 15 | DI: `AddRequestService<T>()` резолвит `TClient` с `IRequestService`; `ConfigureJson` переопределяет сериализатор только для своего клиента; fallback на глобальный; отсутствие сериализатора → понятный `InvalidOperationException` |
| 16 | DI: `ConfigureHttpClient` и `AddHttpMessageHandler` реально применяются (BaseAddress, кастомный хедер) |
| 17 | `ApiSession`: `Set/Clear` + `Changed` (потокобезопасность — параллельные `Set`) |

### 9.2 `Mirai.Space.Extensions.Http.Json.Tests`

| # | Что проверяем |
|---|---|
| 1 | Round-trip: `Serialize` → `DeserializeAsync` (Web-дефолты, camelCase, enum as string) |
| 2 | `JsonSerializerContext` (source-gen): сериализация/десериализация без reflection-резолвера |
| 3 | `JsonRequestSerializer(options)` не мутирует переданный объект (snapshot) |
| 4 | `application/json` + корневой массив → стрим по элементам (без буфера) |
| 5 | `application/x-ndjson` / `jsonl` / `x-jsonlines` → построчно (через `PipeReader` + `topLevelValues: true`) |
| 6 | `text/event-stream` → `SseParser`: одно- и многострочный `data:`, `event:`, `id:`, комментарии-keepalive игнорируются |
| 7 | SSE с payload `null` → `JsonException` (осознанное решение, §8.2) |
| 8 | null-элементы в массиве отфильтрованы (контракт «поток без null») |
| 9 | `ProblemDetailsApiErrorHandler`: `application/problem+json` → `ApiProblemDetails`; HTML → сырой текст, обрезанный по лимиту; пустое тело |
| 10 | `MediaType == "application/json"`, `Content-Type` без charset |

### 9.3 `Mirai.Space.Extensions.Http.MessagePack.Tests`

| # | Что проверяем |
|---|---|
| 1 | Round-trip `Serialize` → `DeserializeAsync` (включая LZ4-опции) |
| 2 | Последовательность структур в одном потоке → все элементы по порядку |
| 3 | Один top-level массив объектов → ошибка десериализации (array-encoded объект неотличим; ограничение задокументировано) |
| 4 | `MediaType == "application/x-msgpack"` |
| 5 | `UseMessagePack(Func<...>)`: трансформер применяется к `Standard` |
| 6 | Кастомный resolver (runtime) подхватывается через `WithResolver` |

### 9.4 `Mirai.Space.Extensions.Http.IntegrationTests`

Реальный HTTP по loopback, без моков: в тестовом проекте поднимается минимальный
`WebApplication` (`<FrameworkReference Include="Microsoft.AspNetCore.App" />`,
`WebApplication.CreateBuilder`, `app.Urls.Add("http://127.0.0.1:0")`, порт из
`IServerAddressesFeature`), эндпоинты: JSON, NDJSON, SSE (несколько событий + задержка),
файл-даунлоад, 401 → 200 после «логина», 404 problem+json, multipart-приём.

| # | Что проверяем |
|---|---|
| 1 | Полный цикл GET/POST/PUT/PATCH/DELETE на реальном сервере |
| 2 | SSE: события приходят инкрементально (первое — до закрытия соединения), `break` рвёт соединение |
| 3 | NDJSON-стрим, отмена на середине (`OperationCanceledException`, соединение закрыто) |
| 4 | Даунлоад большого файла: `ResponseHeadersRead`, память не растёт (грубая проверка), `Dispose` закрывает |
| 5 | `ApiReauthenticationHandler` end-to-end: 401 → окно-заглушка → повтор → 200 |
| 6 | `ApiSessionHandler` + `IApiSession.Changed`: после логина все последующие запросы авторизованы |
| 7 | Multipart upload: сервер видит имя файла, content-type, поля формы |
| 8 | ProblemDetails 4xx/5xx через `ProblemDetailsApiErrorHandler` |
| 9 | MessagePack-клиент против сервера, отдающего `application/x-msgpack` (в том же host) |

`Microsoft.AspNetCore.Mvc.Testing` / `WebApplicationFactory` **не нужны**, пока в репозитории нет
настоящего ASP.NET Core host-проекта; `WebApplication` в тестовой сборке даёт то же покрытие и
меньше связности.

### 9.5 Что не тестируем

- Реальный DNS/TLS (внешние зависимости) — только smoke вручную.
- Политики resilience — это `Microsoft.Extensions.Http.Resilience`, не наш код.
- UI-поток и `SynchronizationContext` — ответственность потребителя (в библиотеке везде
  `ConfigureAwait(false)`; проверяется ревью, а не тестом).

---

## 10. План реализации (по шагам, с чекпоинтами)

Шаг 0 — гигиена репозитория (**требует согласия: меняет solution-файлы**)

- создать `Directory.Build.props`:
  ```xml
  <Project>
    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
      <LangVersion>latest</LangVersion>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
      <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
      <AnalysisLevel>latest</AnalysisLevel>
      <IsPackable>false</IsPackable>
    </PropertyGroup>
  </Project>
  ```
  (и убрать дубли из `.csproj` — это правка существующих файлов, поэтому отдельным шагом и с согласия);
- создать `global.json` (SDK 10.0.403, `test.runner = Microsoft.Testing.Platform`);
- добавить в `Directory.Packages.props`: `MessagePack` 3.1.12, `xunit.v3` 4.0.3;
- `Mirai.Space.Request` остаётся прикладным слоем (см. §3), решение переведено на `Mirai.Space.slnx`, старый `.sln` удалён;
- **чекпоинт**: `dotnet build Mirai.Space.slnx` зелёный, `git status` показывает только ожидаемые файлы.

Шаг 1 — контракты `Extensions.Http` + минимальный GET

- `IRequestSerializer`, `IApiErrorHandler`, `ApiException`, `DefaultApiErrorHandler`, `ApiResponse`,
  `ApiResponse<T>`, `ApiStreamResponse`, `ApiRequest`, `IRequestService` (только `GetAsync<T>`), `RequestService`.
- **чекпоинт**: `Extensions.Http.Tests` (каркас + 3 теста: GET, 204, не-2xx) зелёные.

Шаг 2 — JSON-сериализатор

- `JsonRequestSerializer` (массив + NDJSON + SSE), `ConfigureJson`, `AddJsonSerializer`, `ProblemDetailsApiErrorHandler`.
- **чекпоинт**: `Http.Json.Tests` зелёные; SSE-тест на многострочный `data:`; NDJSON-тест.

Шаг 3 — полный набор методов

- POST/PUT/PATCH/DELETE/HEAD/OPTIONS, `*ResponseAsync`, `*StreamAsync`, `GetAsyncEnumerable<T>`, escape hatch.
- **чекпоинт**: тесты владения (диспоз при `break`/исключении/отмене) + `ApiResponse`-снимок зелёные.

Шаг 4 — DI

- `AddRequestService<TClient>`, `UseSerializer<T>`, `UseErrorHandler<T>`, keyed-сериализаторы, fallback.
- **чекпоинт**: DI-тесты (15–16 из §9.1) и пример `AboutRequestService` в тестах резолвятся и работают.

Шаг 5 — сессия и переавторизация

- `IApiSession`/`ApiSession`, `ApiSessionHandler`, `IApiReauthenticator`, `ApiReauthenticationHandler`.
- **чекпоинт**: тесты 12–14 (§9.1) + end-to-end 401→повтор (§9.4) зелёные.

Шаг 6 — MessagePack

- `MessagePackRequestSerializer`, `UseMessagePack`; `PackageVersion` для `MessagePack`.
- **чекпоинт**: `Http.MessagePack.Tests` зелёные, ограничение по массиву задокументировано в README.

Шаг 7 — интеграционные тесты + README

- `Http.IntegrationTests` (loopback-хост), README по каждому проекту (включая «чего мы не умеем»:
  MessagePack-массив, source-gen резолверы, retry после начала стрима).
- **чекпоинт**: `dotnet test Mirai.Space.slnx` зелёный целиком.

Шаг 8 — миграция потребителей (вне библиотеки)

- `AboutRequestService`, `FilesRequestService`, `AuthRequestService` в Application/Presentation,
  `SputnikApiErrorHandler` вместо `ThrowException`, `ApiSession` вместо статического состояния,
  удаление `RequestHelper`/`ApiClientFactory`.
- **чекпоинт**: ни одного `new HttpClient` в UI-проектах; ручная проверка логина и ленты.

---

## 11. Открытые вопросы и рекомендации

**Q1. SSE: `!` в лямбде или `IAsyncEnumerable<T?>`?**
Рекомендация: **`IAsyncEnumerable<T>`, без `!`** — null-payload выбрасывает `JsonException`
(событие без валидного JSON — нарушение протокола). Плюс: вызывающий код и `await foreach` не
загрязняются `T?`/проверками, контракт «элемент всегда есть» сохраняется. Альтернатива
(`IAsyncEnumerable<T?>`) отвергнута: SSE-элемент по определению не null, а nullable-шум придётся
тащить в каждый VM. Третий вариант — молча пропускать null-события — отвергнут: тихая потеря
сообщений хуже исключения.

**Q2. Снимок заголовков в `ApiResponse` (аллокация словаря на ответ) или владение/диспоз ответа?**
Рекомендация: **оставить снимок**. Аргументы: (а) `HEAD`/`OPTIONS` отдают только заголовки — без
снимка их не вернуть; (б) `ApiResponse` должен переживать `using` (его кладут в поля/кэши);
(в) цена — 2 `Dictionary` (case-insensitive) + `string[]` на ответ, что на фоне десериализации
JSON/MessagePack и сетевого I/O шум; (г) владение `HttpResponseMessage` наружу — это ровно та
ошибка, из-за которой в `RequestHelper` никто не диспозил ответ. Если профилирование когда-нибудь
покажет этот словарь в топе (маловероятно) — можно сделать ленивый снимок: хранить
`HttpResponseHeaders` (он остаётся читаемым после `Dispose`) и строить словарь по первому обращению.
Это изменение внутреннее, API не ломает.

**Q3. Отдельный проект `Extensions.Http.Abstractions`?**
Решение владельца (m00562): **да**, проект создан, контракты вынесены (см. §3). Изначальная
рекомендация была «нет, три проекта», но контракты без зависимостей от `Microsoft.Extensions.Http`
полезнее для слоёв, которые не должны знать про инфраструктуру.

**Q4. `Mirai.Space.Request` и `Mirai.Space.slnx`.**
`Mirai.Space.slnx` — актуальный solution (сгенерирован из `.sln` через `dotnet sln migrate`, дерево папок сохранено), старый `.sln` удалён. `Mirai.Space.Request` — не легаси, а прикладной проект для конечных сервисов (`AboutRequestService` и т.п.), которые используют `IRequestService`; удалять его не нужно.

**Q5. Multipart: отдельные методы или escape hatch?**
Рекомендация: **escape hatch** (`ApiRequest.Post(url, MultipartFormDataContent)` +
`SendAsync<T>`) — ноль новых типов, `MultipartFormDataContent` не протекает в основные сигнатуры.
Если multipart станет массовым — добавить `PostAsync<T>(url, ApiMultipartContent form, ct)` с
билдером (`AddFile(name, fileName, stream)`, `AddField(name, value)`), без `HttpContent` в публичной
сигнатуре.

**Q6. `PUT`/`PATCH` без `*ResponseAsync` — асимметрия?**
Рекомендация: оставить как есть (в чате зафиксировано). Добавлять `PutResponseAsync`/`PatchResponseAsync`
только при первом реальном кейсе — публичный API без потребителя дороже, чем одна строка позже.

**Q7. Где живёт `IApiSession`?**
Рекомендация: в `Extensions.Http` (singleton, без UI-зависимостей). `IApiReauthenticator` — тоже
контракт в библиотеке, реализация — в приложении (UI-поток, окно логина, переподключение SignalR).

**Q8. `ApiException` без тела — достаточно?**
Рекомендация: да, ядро без тела (не тянет STJ и не читает ответ зря). Тело/`ProblemDetails` —
в `ApiProblemException` (JSON-проект) или в собственном обработчике приложения.

**Q9. MessagePack 3.1.12 или 4.0.0-rc.1?**
Рекомендация: **3.1.12** (последний стабильный). 4.0.0-rc — не берём до GA; source-gen резолверов
в 3.1.12 нет (см. §8.4), это надо принять осознанно.

**Q10. `Accept`-заголовок.**
Рекомендация: библиотека его не выставляет (решение из чата). Клиент, которому нужен
`Accept: application/x-msgpack`, настраивает `DefaultRequestHeaders` в `ConfigureHttpClient`.
Альтернатива — выставлять из `serializer.MediaType` — отвергнута: ломает SSE/NDJSON-клиенты одного
сериализатора.

---

## 12. Риски и как их снимаем

| Риск | Снятие |
|---|---|
| `ActivatorUtilities` не находит конструктор `TClient` | правило: `TClient` — `sealed class` с одним публичным конструктором `(IRequestService …)`; тест DI на эталонном сервисе |
| Двойная регистрация `TClient`/сериализатора | `TryAddTransient` для `TClient`, `TryAddKeyedSingleton` для serializer/handler (первая регистрация выигрывает) — задокументировать |
| Забытый `ConfigureJson` → клиент без сериализатора | явный `InvalidOperationException` с именем клиента и подсказкой («вызовите ConfigureJson/AddJsonSerializer») |
| Стриминг и retry-политики несовместимы | в README и в примерах: для SSE/stream — без retry, `Timeout.InfiniteTimeSpan` |
| SSE-клиент «висит» при недоступном сервере | `ConnectTimeout`/`AttemptTimeout` в resilience или отдельный `HttpClient.Timeout` для не-SSE части |
| MessagePack-массив не стримится | документированное ограничение: `ReadAsync` отдаёт массив одним сообщением; буферизация через `DownloadAsync`, без `NotSupportedException` |
| `ApiStreamResponse` забыли задиспозить | анализатор/ревью: `await using`; `Content` не отдаём как `Stream` напрямую из методов |
| `JsonSerializerOptions` мутируют после регистрации | копия в `JsonRequestSerializer(options)`; `MakeReadOnly()` не вызываем — без `TypeInfoResolver` он бросает `InvalidOperationException` |
| Публичный API разрастается | правило: каждый метод добавлен только с тестом и сценарием; `IRequestService` — 19 методов + 4 escape hatch, дальше — обоснование |

---

## 13. Маппинг старого `RequestHelper`

| `RequestHelper` | Новое |
|---|---|
| `Get<T>(url)` / `GetPageModel<T>(url)` | `GetAsync<T>(url)` / `GetAsync<PageModel<T>>(url)` |
| `Get<T>(url, Dictionary<string,string> headers)` | `SendAsync<T>(ApiRequest.Get(url)…)` или `DelegatingHandler` |
| `Add<TModel>` → `Task<Guid>` | `PostAsync<Guid, TModel>(url, model)` |
| `Update(url)`, `Delete(url)`, `Restore(url)` (все POST) | `PostAsync(url)` |
| `Update<TModel>(url, model)` | `PostAsync(url, model)` |
| `Post<T>(url)` / `Post<T,TModel>` | `PostAsync<T>(url)` / `PostAsync<T, TModel>(url, model)` |
| `Upload` / `Upload<T>` | `SendAsync(ApiRequest.Post(url, form))` / `SendAsync<T>(…)` |
| `UploadGet` / `Download` | `PostToStreamAsync(url, body)` / `DownloadAsync(url)` |
| `Download<TModel>` | `PostToStreamAsync(url, model)` |
| `ThrowException` | `IApiErrorHandler` (DI), сообщения сохраняет `SputnikApiErrorHandler` |
| статический `LogProvider` | `ILogger<T>` из DI |
| `JsonSerializerOptions` + `AddJsonContexts()` | `ConfigureJson(o => o.AddJsonContexts())` (source-gen контексты) |
| — (новое) | `GetAsyncEnumerable<T>`, SSE, `ApiResponse<T>`, `IApiSession` + переавторизация |
