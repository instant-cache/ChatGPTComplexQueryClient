using Microsoft.AspNetCore.Mvc;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GPTAdScenarioGen.Controllers
{
    /// <summary>
    /// Управление командами запросов к АПИ
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ChatGptApiController : Controller
    {
        private readonly ChatGptApiService _service;
        private readonly ILogger _logger;
        public ChatGptApiController(ChatGptApiService service, ILogger<ChatGptApiController> logger) 
        { 
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Получает массив подзапросов, делает на их основе запрос к ChatGPT и возвращает ответ асинхронным потоком текста
        /// </summary>
        /// <param name="queries">Массив подзапросов для вставления в шаблон запроса или использования самостоятельно</param>
        /// <param name="token">Жетон отмены задачи</param>
        [HttpPost("[action]")]
        public async Task<IActionResult> QueriesAsStreamAsync([Required] string[] queries, CancellationToken token = default)
        {
            LogRequest("QueriesAsStreamAsync");
            try
            {
                return Ok(_service.QueryChatGptAsAsyncStream(queries, token));
            }
            catch (AppException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("[action]")]
        public Task<IActionResult> QueryAsStreamAsync([Required] string query, CancellationToken token = default)
            => QueriesAsStreamAsync(new[] { query }, token);

        [HttpPost("[action]")]
        public async Task<IActionResult> QueriesAsync([Required] string[] queries, CancellationToken token = default)
        {
            LogRequest("QueriesAsync");
            StringBuilder resultBuilder = new();
            try
            {
                await foreach (var subresult in _service.QueryChatGptAsAsyncStream(queries, token))
                    resultBuilder.Append(subresult);
                return Ok(resultBuilder.ToString());
            }
            catch (AppException ex)
            {
                _logger.LogError(ex.ToString());
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("[action]")]
        public Task<IActionResult> QueryAsync([Required] string query, CancellationToken token = default)
            => QueriesAsync(new[] { query }, token);

        [HttpPost("[action]")]
        public async Task<IActionResult> QueriesAsWebsocketAsync(string[] queries, CancellationToken token = default)
        {
            LogRequest("QueriesAsWebsocketAsync");
            if (!HttpContext.WebSockets.IsWebSocketRequest)
                return BadRequest("Запрос не является Websocket запросом");

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            try
            {
                await SendQueriesToWebsocketAsync(webSocket, queries, token);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Весь ответ от модели был передан", token);
            }
            catch (AppException ex)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, ex.Message, default);
                _logger.LogError("Некорректный запрос, закрываю соединение. Ошибка: {ex}", ex);
            }
            catch (Exception ex)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, $"Ошибка обработки запроса: {ex.Message}", default);
                _logger.LogError("Ошибка при обработке запроса: {ex}", ex);
            }
            return new EmptyResult();
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> QueriesAsWebsocketAsync(CancellationToken token = default)
        {
            LogRequest("QueriesAsWebsocketAsync");
            if (!HttpContext.WebSockets.IsWebSocketRequest)
                return BadRequest("Запрос не является Websocket запросом");

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            try
            {
                var queries = await ReceiveQueriesFromWebsocketAsync(webSocket, token);
                await SendQueriesToWebsocketAsync(webSocket, queries, token);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Весь ответ от модели был передан", token);
            }
            catch (AppException ex)
            {
                _logger.LogError("Некорректный запрос, закрываю соединение. Ошибка: {ex}", ex);
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, ex.Message, default);
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при обработке запроса: {ex}", ex);
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, ex.Message, default);
            }
            return new EmptyResult();
        }

        [HttpGet("[action]")]
        public Task<IActionResult> QueryAsWebsocketAsync([Required] string query, CancellationToken token = default)
            => QueriesAsWebsocketAsync(new[] { query }, token);

        private void LogRequest(string requestName)
        {
            _logger.LogInformation("Поступил запрос на метод {name}", requestName);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetQueryTemplateAsync(CancellationToken token = default)
        {
            return Ok(_service.FrontendTemplate);
        }

        private async Task<string[]> ReceiveQueriesFromWebsocketAsync(WebSocket webSocket, CancellationToken token = default)
        {
            using (MemoryStream stream = new())
            {
                WebSocketReceiveResult received;
                byte[] buffer = new byte[1024 * 4];

                do
                {
                    received = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (received.Count == 0)
                    {
                        _logger.LogWarning("Внимание! Передано сообщение размером 0 байт. Состояние сокета: {state}", webSocket.State);
                        break;
                    }
                    else
                    {
                        _logger.LogDebug("Принято сообщение длиной {count} байт", received.Count);
                        _logger.LogTrace("Сообщение:\n{msg}\n(в байтах:)\n{bytes}", Encoding.UTF8.GetString(buffer), BitConverter.ToString(buffer, 0, received.Count));
                    }
                    stream.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);

                var data = Encoding.UTF8.GetString(stream.ToArray());
                _logger.LogDebug("Принятое сообщение (в кодировке UTF8): {msg}", data);

                string[] queries = JsonSerializer.Deserialize<string[]>(data);
                if (queries == null)
                    throw new ArgumentNullException(nameof(queries));
                _logger.LogDebug("Разобрано {count} запросов в сообщении", queries.Length);

                return queries;
            }
        }

        private async Task SendQueriesToWebsocketAsync(WebSocket webSocket, string[] queries, CancellationToken token = default)
        {
            await foreach (var subresult in _service.QueryChatGptAsAsyncStream(queries, token))
                await webSocket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(subresult)),
                    WebSocketMessageType.Text,
                    true,
                    token);
        }
    }
}
