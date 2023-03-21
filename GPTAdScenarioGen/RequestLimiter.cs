namespace GPTAdScenarioGen
{
    public class RequestLimiter
    {
        private readonly int _limit = 500;
        private int _count = 0;
        private object _lock = new object();

        public RequestLimiter(IConfiguration config, ILogger<RequestLimiter> logger, CancellationToken token = default)
        {
            var limitConfig = config.GetSection("MaxRequestsPerHour");
            if (string.IsNullOrWhiteSpace(limitConfig.Value))
                logger.LogWarning("Не найден параметр MaxRequestsPerHour. Используется значение по умолчанию.");
            else
                _limit = limitConfig.Get<int>();

            _ = ResetTimerForeverAsync(token);
        }

        public bool ClockInRequest()
        {
            if (_count >= _limit) //Эта проверка не будет блокировать работу ResetTimer
                return false;

            lock (_lock)
            {
                if (_count >= _limit) //Эта проверка - на случай двух одновременных запросов
                    return false;
                _count++;
                return true;
            }
        }

        private async Task ResetTimerForeverAsync(CancellationToken token = default)
        {
            while(!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(1));
                lock(_lock)
                {
                    _count = 0;
                }
            }
        }
    }

    public class AppOutOfRequestsException : Exception 
    {
        public AppOutOfRequestsException() : base("Закончились запросы") { }
    }
}
