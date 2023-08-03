# ChatGPTComplexQueryClient

Этот API является промежуточным клиентом ChatGPT API. Он формирует запросы к ChatGPT API на основе предустановленного в appsettings.json шаблона и запроса (или набора запросов)

## Использование:
1. Зарегистрироваться [здесь](https://platform.openai.com)
2. Создать новый ключ API [здесь](https://platform.openai.com/account/api-keys)
3. Поместить сгенерированный ключ API в файл `key.txt` в корне проекта
4. Запустить приложение (http/https порты по умолчанию)
5. Совершить запрос GET http://localhost:5001/api/ChatGptApi/Query?query=puppies
```
curl -X 'GET' \
  'http://localhost:5001/api/ChatGptApi/Query?query=puppies' \
  -H 'accept: */*'
```

## Эндпойнты
У этого приложения есть шесть эндпойнтов:
1. POST /api/ChatGptApi/QueriesAsStream
Получает массив строковых подзапросов, делает на их основе запрос к ChatGPT и возвращает ответ асинхронным потоком текста.
Входные данные: body, массив текстовых строк.
2. GET /api/ChatGptApi/QueryAsStream
Получает строковый запрос, делает на их основе запрос к ChatGPT и возвращает ответ асинхронным потоком текста.
Входные данные: url параметр, строка `query`.

3. POST /api/ChatGptApi/Queries
Получает массив строковых подзапросов, делает на их основе запрос к ChatGPT и возвращает ответ одной строкой, когда он будет готов.
Входные данные: body, массив текстовых строк.
4. GET /api/ChatGptApi/Query
Получает строковый запрос, делает на их основе запрос к ChatGPT и возвращает ответ одной строкой, когда он будет готов.
Входные данные: url параметр, строка `query`.

5. GET (CONNECT) /api/ChatGptApi/QueriesAsWebsocket
Открывает вебсокет, получает через него массив строк, делает на их основе запрос к ChatGPT, и возвращает ответ через вебсокет.
Входные данные: Вебсокет, массив текстовых строк.

Эти методы выполняют одну и ту же работу, и различаются только форматами принимаемых и возвращаемых данных.

6. GET /api/ChatGptApi/GetQueryTemplate
Выдаёт шаблон строки фронтенду для генерации полей ввода


## Запросы и шаблоны
Шаблоны должны выглядеть так: `Расскажи историю про {0} и то как у {0} выросли {1}`.

Методы, принимающие массивы строк, поместят первую строку вместо `{0}`, а вторую вместо `{1}`. Например, если будет передан массив `["паука", "уши"]`, то лингвистической модели будет передан следующий запрос: `Расскажи историю про паука и то как у паука выросли уши`.

[Дополнительно про форматирование строк](https://www.c-sharpcorner.com/UploadFile/mahesh/format-string-in-C-Sharp/)

Если не указано ни QueryTemplatePath, ни QueryTemplate, запрос отправляется как есть, без вставления в шаблоны. При отправлении массива подзапросов они соединяются в одну строку, с разделителем `, `. Например, подзапросы `["lorem","ipsum"]` превратятся в запрос `"lorem, ipsum"`.

## Конфигурация
### appsettings.json
Настройка приложения происходит через параметры `appsettings.json`.

На данный момент приложение поддерживает следующие параметры:
- `QueryTemplatePath, nullable string` - Путь к файлу, содержащему шаблон запроса. Имеет приоритет выше, чем QueryTemplate.
- `QueryTemplate, nullable string` - Базовый шаблон запроса. Имеет приоритет ниже, чем QueryTemplatePath.
- `MaxResponseTokens, int, default 500` - Максимальное количество жетонов, которые вернёт лингвистическая модель в ответ на запрос. Один жетон - одно часто встречающееся слово, знак пунктуации или в среднем 0.75 слова на английском языке. Внимание! Почти все модели не поддерживают запросы, где длина запроса и ответа суммарнно превышает 2048 жетонов. Они стоят денег.
- `MaxRequestLength, nullable int, default null` - Максимальная длина итогового запроса. При null неограничена.
- `MaxRequestsPerHour, int, default 500` - Максимальное количество запросов в час, Количество текущих запросов сбрасывается каждый час.
- `Temperature, float, default 0.8` - Температура модели. Минимум 0, максимум 1. Влияет на "воображение" модели - чем выше значение, тем больше шансов получить другой результат на одинаковый запрос.
- `ApiKeyVariableName, string, default "OPENAI_API_KEY"` - Название переменной среды, в которой хранится API-ключ от ChatGPT.
- `Model, string, default "gpt-3.5-turbo"` - Код используемой модели.

Помимо этого там можно указать стандартные настройки сервера Kestrel

### Поддерживаемые модели
Найти больше поддерживаемых моделей можно [здесь](https://github.com/betalgo/openai/wiki/Models)
Поддерживаемые на момент разработки модели: 
```
ada
babbage
gpt-3.5-turbo
gpt-3.5-turbo-0301
code-cushman-001
code-davinci-001
code-davinci-002
code-davinci-edit-001
code-search-ada-code-001
code-search-ada-text-001
code-search-babbage-code-001
code-search-babbage-text-001
curie
curie-instruct-beta
curie-similarity-fast
davinci
davinci-instruct-beta
text-ada-001
text-babbage-001
text-curie-001
text-davinci-001
text-davinci-002
text-davinci-003
text-davinci-edit-001
text-embedding-ada-002
text-search-ada-doc-001
text-search-ada-query-001
text-search-babbage-doc-001
text-search-babbage-query-001
text-search-curie-doc-001
text-search-curie-query-001
text-search-davinci-doc-001
text-search-davinci-query-001
text-similarity-ada-001
text-similarity-babbage-001
text-similarity-curie-001
text-similarity-davinci-001
whisper-1
```

### Логирование
Для логирования используется [NLog](https://nlog-project.org). Настройки хранятся в `NLog.config`
