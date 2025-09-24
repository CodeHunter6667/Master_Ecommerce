using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using System.Text;

namespace MasterEcommerce.Services;

public interface IMessageBusService
{
    void Publish<T>(string queueName, T message) where T : class;
    void Subscribe<T>(string queueName, Func<T, Task> handler) where T : class;
    void CreateQueue(string queueName);
}

public class RabbitMQService : IMessageBusService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMQService> _logger;

    public RabbitMQService(ILogger<RabbitMQService> logger)
    {
        _logger = logger;
        
        var factory = new ConnectionFactory
        {
            HostName = "localhost", // Para desenvolvimento local
            UserName = "guest",
            Password = "guest"
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
    }

    public void CreateQueue(string queueName)
    {
        _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null).GetAwaiter().GetResult();
    }

    public void Publish<T>(string queueName, T message) where T : class
    {
        CreateQueue(queueName);
        
        var json = JsonConvert.SerializeObject(message);
        var body = Encoding.UTF8.GetBytes(json);

        _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            body: body).GetAwaiter().GetResult();

        _logger.LogInformation("Message published to queue {QueueName}: {Message}", queueName, json);
    }

    public void Subscribe<T>(string queueName, Func<T, Task> handler) where T : class
    {
        CreateQueue(queueName);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonConvert.DeserializeObject<T>(json);

                if (message != null)
                {
                    await handler(message);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsumeAsync(queueName, false, consumer);
        _logger.LogInformation("Started consuming messages from queue {QueueName}", queueName);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}