using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text;

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
            return Ok(_service.QueryChatGptAsAsyncStream(queries, token));
        }

        [HttpGet("[action]")]
        public Task<IActionResult> QueryAsStreamAsync([Required] string query, CancellationToken token = default)
            => QueriesAsStreamAsync(new[] { query }, token);

        [HttpPost("[action]")]
        public async Task<IActionResult> QueriesAsync([Required] string[] queries, CancellationToken token = default)
        {
            LogRequest("QueriesAsync");
            StringBuilder resultBuilder = new();
            await foreach (var subresult in _service.QueryChatGptAsAsyncStream(queries, token))
                resultBuilder.Append(subresult);
            return Ok(resultBuilder.ToString());
        }

        [HttpGet("[action]")]
        public Task<IActionResult> QueryAsync([Required] string query, CancellationToken token = default)
            => QueriesAsync(new[] { query }, token);

        [HttpPost("[action]")]
        public async Task<IActionResult> QueriesAsWebsocketAsync([Required] string[] queries, CancellationToken token = default)
        {
            LogRequest("QueriesAsWebsocketAsync");
            if (!HttpContext.WebSockets.IsWebSocketRequest)
                return BadRequest("Запрос не является Websocket запросом");

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            try
            {
                await foreach (var subresult in _service.QueryChatGptAsAsyncStream(queries, token))
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(Encoding.ASCII.GetBytes(subresult)), 
                        System.Net.WebSockets.WebSocketMessageType.Text, 
                        true, 
                        token);
            }
            catch (Exception ex)
            {
                await webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.InternalServerError, "Ошибка обработки запроса", token);
                throw;
            }
            await webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Весь ответ от модели был передан", token);
            return Ok();
        }

        [HttpGet("[action]")]
        public Task<IActionResult> QueryAsWebsocketAsync([Required] string query, CancellationToken token = default)
            => QueriesAsWebsocketAsync(new[] { query }, token);

        private void LogRequest(string requestName)
        {
            _logger.LogInformation("Поступил запрос на метод {name}", requestName);
        }
    }
}
