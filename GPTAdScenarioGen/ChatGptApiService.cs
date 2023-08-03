using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace GPTAdScenarioGen
{
    /// <summary>
    /// Менеджер запросов и ответов к ChatGPT
    /// </summary>
    public class ChatGptApiService
    {
        public readonly string? FrontendTemplate = null;

        private readonly ILogger _logger;
        private readonly string? _queryTemplate = null;
        private readonly float _temperature = 0.8f;
        private readonly string _apiKey = "OPENAI_API_KEY";
        private readonly string _model = "gpt-3.5-turbo";
        private readonly int _maxTokens = 500;
        private readonly int? _maxRequestLength = null;
        private readonly bool _isDebugPrompt = false;
        private readonly int _responseThrottlingMs = 0;
        private readonly int _responseMinLength = 1;

        public ChatGptApiService(RequestLimiter limiter, IConfiguration config, ILogger<ChatGptApiService> logger)
        {
            if (!limiter.ClockInRequest())
                throw new AppOutOfRequestsException();
            _logger = logger;

            string? templateConfig;
            var templateConfigPath = config["QueryTemplatePath"];
            if (!string.IsNullOrWhiteSpace(templateConfigPath)) 
                templateConfig = File.ReadAllText(templateConfigPath);
            else
                templateConfig = config["QueryTemplate"];
            if (string.IsNullOrWhiteSpace(templateConfig))
                _logger.LogWarning("Не найден или пустой шаблон запроса (параметры \"QueryTemplate\" или \"QueryTemplatePath\". Пользовательские запросы будут отправлены как есть.");
            else
                _queryTemplate = templateConfig;

            string? frontendTemplateConfig;
            var frontendTemplateConfigPath = config["QueryFrontendPath"];
            if (!string.IsNullOrWhiteSpace(frontendTemplateConfigPath))
                frontendTemplateConfig = File.ReadAllText(frontendTemplateConfigPath);
            else
                frontendTemplateConfig = config["QueryFrontend"];
            if (string.IsNullOrWhiteSpace(frontendTemplateConfig))
                _logger.LogWarning("Не найден или пустой шаблон запроса (параметры \"QueryTemplate\" или \"QueryTemplatePath\". Пользовательские запросы будут отправлены как есть.");
            else
                FrontendTemplate = frontendTemplateConfig;

            var temperatureConfig = config.GetSection("Temperature");
            if (string.IsNullOrWhiteSpace(temperatureConfig.Value))
                _logger.LogTrace("Не найден параметр Temperature. Используется значение по умолчанию.");
            else
                _temperature = temperatureConfig.Get<float>();
            if (_temperature < 0) 
                throw new ArgumentException("Температура не может быть ниже 0 (параметр Temperature).");
            if (_temperature > 1) 
                throw new ArgumentException("Температура не может быть выше 1 (параметр Temperature).");

            var apiKeyConfig = config["ApiKeyPath"];
            if (string.IsNullOrWhiteSpace(apiKeyConfig))
                _logger.LogTrace("Не найдено или пустое название переменной ключа к ChatGPT API. Используется значение по умолчанию.");
            else
                _apiKey = File.ReadAllText(apiKeyConfig);

            var maxTokensConfig = config.GetSection("MaxResponseTokens");
            if (string.IsNullOrWhiteSpace(maxTokensConfig.Value))
                _logger.LogTrace("Не найден параметр MaxResponseTokens. Используется значение по умолчанию.");
            else
                _maxTokens = maxTokensConfig.Get<int>();
            if (_maxTokens < 1)
                throw new ArgumentException("Максимальное количество токенов ответа не может быть ниже 1 (параметр MaxResponseTokens).");
            if (_maxTokens > 4096)
                throw new ArgumentException("Максимальное количество токенов ответа не может быть выше 4096 (параметр MaxResponseTokens).");

            var maxRequestLength = config.GetSection("MaxRequestLength");
            if (!string.IsNullOrWhiteSpace(maxRequestLength.Value))
                _maxRequestLength = maxRequestLength.Get<int>();

            _isDebugPrompt = config.GetValue<bool?>("DebugResponse") ?? false;
            _responseThrottlingMs = config.GetValue<int?>("ResponseThrottling") ?? 0;
            _responseMinLength = config.GetValue<int?>("ResponseMinChunkSize") ?? 1;
        }

        /// <summary>
        /// Метод, делающий запрос к ChatGPT и возвращающий результат в виде асинхронного потока
        /// </summary>
        /// <param name="queries">Список передаваемых подзапросов, предназначенных для вставки в основной запрос или для использования как запрос</param>
        /// <returns>Асинхронный поток строк, которые возвращает ChatGPT</returns>
        public async IAsyncEnumerable<string> QueryChatGptAsAsyncStream(string[] queries, [EnumeratorCancellation] CancellationToken token = default)
        {
            if (_isDebugPrompt)
            {
                var debugPrompt = File.ReadAllText(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "debugprompt.txt");
                string chunk = string.Empty;
                foreach (var symbol in debugPrompt)
                {
                    chunk += symbol;
                    if (chunk.Length >= _responseMinLength)
                    {
                        await Task.Delay(_responseThrottlingMs);
                        yield return chunk;
                        chunk = string.Empty;
                    }
                }
                if (chunk.Length > 0)
                {
                    await Task.Delay(_responseThrottlingMs);
                    yield return chunk;
                }
                yield break;
            }

            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = _apiKey
            });

            string completeQuery;
            try
            {
                completeQuery = _queryTemplate != null
                    ? string.Format(_queryTemplate, queries)
                    : string.Join(", ", queries);
            }
            catch (FormatException ex)
            {
                throw new FormatException("Количество подзапросов не совпадает с количеством переменных в шаблоне!", ex);
            }

            if (_maxRequestLength != null && completeQuery.Length > _maxRequestLength)
                throw new ArgumentException($"Получено {completeQuery.Length} символов, максимум {_maxRequestLength} символов");

            openAiService.SetDefaultModelId(_model);
            StringBuilder completeResult = new($"Запрос к API: \"{completeQuery}\"\nОтвет API: \"");
            if (_model == Models.ChatGpt3_5Turbo || _model == Models.ChatGpt3_5Turbo0301)
            {
                var completionResults = openAiService.ChatCompletion.CreateCompletionAsStream(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                            ChatMessage.FromSystem("You are a helpful assistant."),
                            ChatMessage.FromUser(completeQuery)
                        },
                        Temperature = _temperature,
                        MaxTokens = _maxTokens
                    },
                    cancellationToken: token);

                string responseChunk = string.Empty;
                await foreach (var completionResult in completionResults)
                    if (!token.IsCancellationRequested && completionResult.Successful)
                    {
                        var resultString = string.Concat(completionResult.Choices.Select(c => c.Message.Content).ToList());
                        completeResult.Append(resultString);
                        if (_responseMinLength == 0)
                            yield return resultString;
                        else
                        {
                            responseChunk += resultString;
                            if (responseChunk.Length >= _responseMinLength)
                            {
                                await Task.Delay(_responseThrottlingMs, token);
                                yield return responseChunk;
                                responseChunk = string.Empty;
                            }
                        }
                    }
                if (!string.IsNullOrEmpty(responseChunk))
                {
                    await Task.Delay(_responseThrottlingMs, token);
                    yield return responseChunk;
                }
            }
            else
            {
                var completionResults = openAiService.Completions.CreateCompletionAsStream(
                    new CompletionCreateRequest()
                    {
                        Prompt = completeQuery,
                        Temperature = _temperature,
                        MaxTokens = _maxTokens
                    },
                    cancellationToken: token);

                string responseChunk = string.Empty;
                await foreach (var completionResult in completionResults)
                    if (!token.IsCancellationRequested && completionResult.Successful)
                    {
                        var resultString = string.Concat(completionResult.Choices.Select(c => c.Text).ToList());
                        completeResult.Append(resultString);
                        if (_responseMinLength == 0)
                            yield return resultString;
                        else
                        {
                            responseChunk += resultString;
                            if (responseChunk.Length >= _responseMinLength)
                            {
                                await Task.Delay(_responseThrottlingMs, token);
                                yield return responseChunk;
                                responseChunk = string.Empty;
                            }
                        }
                    }
                if (!string.IsNullOrEmpty(responseChunk))
                {
                    await Task.Delay(_responseThrottlingMs, token);
                    yield return responseChunk;
                }
            }
            completeResult.Append('"');
            _logger.LogInformation("Завершён запрос. {info}", completeResult.ToString());
        }
    }

    public class AppMissingApiKeyException : AppException
    {
        public AppMissingApiKeyException() : base("Ключ для ChatGPT API не найден в переменных среды") { }
    }

    public class AppInvalidQueryException : AppException
    {
        public AppInvalidQueryException(int current, int max) : base($"Длина запроса слишком велика. Длина запроса: {current}, максимум: {max}") { }
    }
}
